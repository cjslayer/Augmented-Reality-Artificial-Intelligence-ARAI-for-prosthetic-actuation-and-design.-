"""Gaussian process for BO Part B: ARD Matern-5/2 on standardized inputs, constant (mean-centered) mean, fitted noise
variance with a floor, hyperparameters by L-BFGS on the log marginal likelihood. torch (CPU, float64) from the ml-agents
venv; no scipy / scikit-learn (not in the venv). Expected improvement for maximization.
"""
import math
import numpy as np
import torch

torch.set_default_dtype(torch.float64)
NOISE_FLOOR = 0.002


class GP:
    def __init__(self, X, y, noise_floor=NOISE_FLOOR):
        X = np.asarray(X, dtype=float); y = np.asarray(y, dtype=float)
        self.x_mean, self.x_std = X.mean(0), X.std(0); self.x_std[self.x_std < 1e-6] = 1.0   # standardized inputs (constant columns left as is)
        self.y_mean = float(y.mean())
        self.X = torch.tensor((X - self.x_mean) / self.x_std); self.y = torch.tensor(y - self.y_mean); self.n, self.d = self.X.shape
        self.noise_floor = noise_floor
        var = max(float(np.var(y)), 1e-4)
        # log length-scales (per dim), log signal variance, raw noise (noise = floor + softplus(raw))
        self.log_ls = torch.zeros(self.d, requires_grad=True); self.log_sf2 = torch.tensor(math.log(var), requires_grad=True); self.raw_noise = torch.tensor(math.log(math.expm1(0.01)), requires_grad=True)

    # ---- kernel
    def _K(self, A, B):
        ls = torch.exp(self.log_ls); a, b = A / ls, B / ls
        r2 = (a * a).sum(1)[:, None] + (b * b).sum(1)[None, :] - 2 * a @ b.T; r = torch.sqrt(torch.clamp(r2, min=1e-12)); s5 = math.sqrt(5.0) * r
        return torch.exp(self.log_sf2) * (1 + s5 + 5 * r2 / 3) * torch.exp(-s5)

    def noise(self): return self.noise_floor + torch.nn.functional.softplus(self.raw_noise)

    def _nll(self):
        K = self._K(self.X, self.X) + (self.noise() + 1e-8) * torch.eye(self.n); L = torch.linalg.cholesky(K)
        a = torch.cholesky_solve(self.y[:, None], L)
        return 0.5 * (self.y[:, None] * a).sum() + torch.log(torch.diag(L)).sum() + 0.5 * self.n * math.log(2 * math.pi) + 0.05 * (self.log_ls ** 2).sum()   # weak prior on log length-scales (log ls ~ N(0, 10))

    def fit(self, iters=150):
        params = [self.log_ls, self.log_sf2, self.raw_noise]; opt = torch.optim.LBFGS(params, lr=0.5, max_iter=iters, line_search_fn="strong_wolfe")
        def closure():
            opt.zero_grad(); loss = self._nll(); loss.backward(); return loss
        try: opt.step(closure)
        except RuntimeError: pass   # a failed Cholesky during a line search leaves the last good parameters
        with torch.no_grad():
            self.log_ls.clamp_(-4.0, 6.0)
            K = self._K(self.X, self.X) + (self.noise() + 1e-8) * torch.eye(self.n); self.L = torch.linalg.cholesky(K); self.alpha = torch.cholesky_solve(self.y[:, None], self.L)
        return float(self._nll().detach())

    def predict(self, Xs):
        Xs = torch.tensor((np.asarray(Xs, dtype=float) - self.x_mean) / self.x_std)
        with torch.no_grad():
            Ks = self._K(Xs, self.X); mu = (Ks @ self.alpha)[:, 0] + self.y_mean
            v = torch.cholesky_solve(Ks.T, self.L); var = torch.exp(self.log_sf2) - (Ks * v.T).sum(1); var = torch.clamp(var, min=1e-10)
        return mu.numpy(), np.sqrt(var.numpy())

    def hyperparameters(self):
        return {"length_scales": torch.exp(self.log_ls).detach().numpy().tolist(), "signal_variance": float(torch.exp(self.log_sf2)), "noise_variance": float(self.noise()), "y_mean": self.y_mean, "n": self.n}


def expected_improvement(mu, sd, best, xi=0.0):
    mu, sd = np.asarray(mu), np.asarray(sd); z = (mu - best - xi) / np.maximum(sd, 1e-12)
    Phi = 0.5 * (1 + np.vectorize(math.erf)(z / math.sqrt(2))); phi = np.exp(-0.5 * z * z) / math.sqrt(2 * math.pi)
    return (mu - best - xi) * Phi + sd * phi
