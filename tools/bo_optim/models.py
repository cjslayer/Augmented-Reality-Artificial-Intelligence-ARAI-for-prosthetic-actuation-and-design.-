"""Surrogates for the mixed space: an exact GP (Matern-5/2 with one lengthscale per parameter block on the normalized
continuous dims, times an exponential Hamming kernel on the mask) and a logistic feasibility model. torch only.

GP:  k((x,m),(x',m')) = s^2 * Matern52(r) * exp(-H(m,m') / l_m),  r^2 = sum_blocks (1/l_b^2) sum_{i in b} (x_i - x'_i)^2
Hyperparameters (log space) are fitted by maximizing the log marginal likelihood with Adam under weak log-normal priors;
targets are standardized. Acquisition: expected improvement times the feasibility probability.
"""
import math

import torch

from .space import GROUP_SLICES, N_CONT

torch.set_default_dtype(torch.float64)
BLOCKS = list(GROUP_SLICES.values())
SQRT5 = math.sqrt(5.0)


def _scaled_sqdist(X1, X2, log_ls):
    d2 = torch.zeros(X1.shape[0], X2.shape[0])
    for b, sl in enumerate(BLOCKS):
        a, c = X1[:, sl], X2[:, sl]
        d2 = d2 + torch.cdist(a, c).pow(2) / torch.exp(2.0 * log_ls[b])
    return d2


def _hamming(M1, M2):
    return torch.cdist(M1, M2, p=1)   # 0/1 entries: L1 = Hamming


def kernel(X1, M1, X2, M2, params):
    r = torch.sqrt(_scaled_sqdist(X1, X2, params["log_ls"]).clamp_min(1e-12))
    matern = (1.0 + SQRT5 * r + 5.0 * r * r / 3.0) * torch.exp(-SQRT5 * r)
    km = torch.exp(-_hamming(M1, M2) / torch.exp(params["log_lm"]))
    return torch.exp(2.0 * params["log_s"]) * matern * km


class MixedGP:
    def __init__(self, X, M, y, iters=300, lr=0.05, seed=0):
        """X: n x 53 in [0,1]; M: n x 14 in {0,1}; y: n objective values."""
        torch.manual_seed(seed)
        self.X = torch.as_tensor(X); self.M = torch.as_tensor(M, dtype=torch.float64); y = torch.as_tensor(y)
        self.y_mean, self.y_std = y.mean(), (y.std() if len(y) > 1 and y.std() > 1e-6 else torch.tensor(1.0))
        self.yn = (y - self.y_mean) / self.y_std
        init_ls = [math.log(0.5 * math.sqrt(sl.stop - sl.start)) for sl in BLOCKS]
        self.p = {
            "log_ls": torch.tensor(init_ls, requires_grad=True),
            "log_lm": torch.tensor(math.log(3.0), requires_grad=True),
            "log_s": torch.tensor(0.0, requires_grad=True),
            "log_noise": torch.tensor(math.log(0.1), requires_grad=True),
        }
        self.prior_ls, self.prior_lm = torch.tensor(init_ls), math.log(3.0)
        self._fit(iters, lr)
        self._cache()

    def _nlml(self):
        n = self.X.shape[0]
        K = kernel(self.X, self.M, self.X, self.M, self.p) + (torch.exp(self.p["log_noise"]).clamp_min(1e-4) + 1e-6) * torch.eye(n)
        L = torch.linalg.cholesky(K)
        alpha = torch.cholesky_solve(self.yn.unsqueeze(1), L)
        nll = 0.5 * (self.yn.unsqueeze(1) * alpha).sum() + torch.log(torch.diagonal(L)).sum() + 0.5 * n * math.log(2 * math.pi)
        # weak log-normal priors (sd 1 in log space) keep the hyperparameters sane with few points
        prior = 0.5 * ((self.p["log_ls"] - self.prior_ls) ** 2).sum() + 0.5 * (self.p["log_lm"] - self.prior_lm) ** 2 \
            + 0.5 * self.p["log_s"] ** 2 + 0.5 * ((self.p["log_noise"] - math.log(0.05)) / 1.5) ** 2
        return nll + prior

    def _fit(self, iters, lr):
        opt = torch.optim.Adam(list(self.p.values()), lr=lr)
        best, best_state = float("inf"), None
        for _ in range(iters):
            opt.zero_grad()
            loss = self._nlml()
            if not torch.isfinite(loss):
                break
            loss.backward(); opt.step()
            with torch.no_grad():
                self.p["log_noise"].clamp_(math.log(1e-4), math.log(1.0))
                self.p["log_ls"].clamp_(math.log(0.05), math.log(20.0)); self.p["log_lm"].clamp_(math.log(0.3), math.log(50.0))
            if loss.item() < best:
                best, best_state = loss.item(), {k: v.detach().clone() for k, v in self.p.items()}
        if best_state is not None:
            for k in self.p:
                self.p[k].data.copy_(best_state[k])
        self.nlml = best

    def _cache(self):
        with torch.no_grad():
            n = self.X.shape[0]
            K = kernel(self.X, self.M, self.X, self.M, self.p) + (torch.exp(self.p["log_noise"]).clamp_min(1e-4) + 1e-6) * torch.eye(n)
            self.L = torch.linalg.cholesky(K)
            self.alpha = torch.cholesky_solve(self.yn.unsqueeze(1), self.L)

    def predict(self, Xs, Ms):
        """Posterior mean and standard deviation of the latent objective (original units) at candidate points."""
        with torch.no_grad():
            Xs = torch.as_tensor(Xs); Ms = torch.as_tensor(Ms, dtype=torch.float64)
            Ks = kernel(Xs, Ms, self.X, self.M, self.p)
            mean = (Ks @ self.alpha).squeeze(1)
            v = torch.cholesky_solve(Ks.T, self.L)
            var = torch.exp(2.0 * self.p["log_s"]) - (Ks * v.T).sum(1)
            return mean * self.y_std + self.y_mean, torch.sqrt(var.clamp_min(1e-12)) * self.y_std

    def hyperparameters(self):
        with torch.no_grad():
            return {"lengthscales": {name: float(torch.exp(self.p["log_ls"][i])) for i, name in enumerate(GROUP_SLICES)},
                    "mask_lengthscale": float(torch.exp(self.p["log_lm"])), "outputscale": float(torch.exp(2 * self.p["log_s"])),
                    "noise": float(torch.exp(self.p["log_noise"])), "nlml": self.nlml}


def expected_improvement(mean, std, best, xi=0.0):
    z = (mean - best - xi) / std
    normal = torch.distributions.Normal(0.0, 1.0)
    return (mean - best - xi) * normal.cdf(z) + std * torch.exp(normal.log_prob(z))


class FeasibilityModel:
    """P(oracle feasible | x, mask): L2-regularized logistic regression on [x, mask]; with a single observed class it falls
    back to the Laplace-smoothed rate."""
    def __init__(self, X, M, feasible, l2=0.5, iters=500, lr=0.05):
        y = torch.as_tensor([1.0 if f else 0.0 for f in feasible])
        self.n, self.k = len(y), int(y.sum().item())
        self.constant = None
        if self.k == 0 or self.k == self.n:
            self.constant = (self.k + 1.0) / (self.n + 2.0)
            return
        Z = self._features(X, M)
        self.w = torch.zeros(Z.shape[1], requires_grad=True); self.b = torch.zeros(1, requires_grad=True)
        opt = torch.optim.Adam([self.w, self.b], lr=lr)
        for _ in range(iters):
            opt.zero_grad()
            logits = Z @ self.w + self.b
            loss = torch.nn.functional.binary_cross_entropy_with_logits(logits, y) + l2 * (self.w ** 2).sum() / self.n
            loss.backward(); opt.step()
        self.w = self.w.detach(); self.b = self.b.detach()

    @staticmethod
    def _features(X, M):
        X = torch.as_tensor(X); M = torch.as_tensor(M, dtype=torch.float64)
        return torch.cat([X - 0.5, M - 0.5, (M.sum(1, keepdim=True) / 14.0) - 0.5], dim=1)

    def prob(self, X, M):
        with torch.no_grad():
            if self.constant is not None:
                return torch.full((len(X),), self.constant)
            return torch.sigmoid(self._features(X, M) @ self.w + self.b)
