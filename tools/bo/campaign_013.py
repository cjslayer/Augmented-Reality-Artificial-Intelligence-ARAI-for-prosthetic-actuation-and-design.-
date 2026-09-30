"""Part C (run-013 brief 3a): BO over the 20-dim reduced morphology space against a random baseline, on the chosen substrate policy.

Objective: tools/bo/objective_013.J (128 episodes, seeds 11001-11128, one realization per theta).
Arms (both in the reduced unit box, tools/bo/theta_reduced):
  random: 100 scrambled Sobol points, seed 4242.
  bo:     30 scrambled Sobol points, seed 4243, then 70 sequential points: GP (ARD Matern-5/2 on standardized inputs, fitted noise with the
          floor from 2c, tools/bo/gp.py) + expected improvement over 4096 uniform candidates + 512 Gaussian perturbations (sd 0.05) of the
          incumbent; incumbent = the observed point with the highest GP posterior mean (noisy objective), EI relative to that mean.
Order: the two arms are interleaved (random i, then bo i) so that slow instrument drift affects both arms alike.
Drift: the reference theta is evaluated before the first point and after every 20 evaluations; |J_ref - J_ref(first)| > 0.10 stops.
Logs: results/bo/partC/campaign.csv (resumable), drift.csv, gp_hparams_<n>.json after every 10 BO points.
usage: python -m tools.bo.campaign_013 --model results/013/013E/Prosthetic.onnx --noise-floor 0.002
"""
import argparse, csv, json, os, sys, time
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_reduced as TR
from tools.bo.objective_013 import J
from tools.bo.gp import GP, expected_improvement
from tools.bo.gates_runner import run_pass

OUT = "results/bo/partC"; LOG = os.path.join(OUT, "campaign.csv"); DRIFT = os.path.join(OUT, "drift.csv"); PASSES = os.path.join(OUT, "passes")
N_RANDOM, N_INIT, N_BO = 100, 30, 70; DRIFT_EVERY, DRIFT_LIMIT = 20, 0.10; N_CAND, N_LOCAL, LOCAL_SD = 4096, 512, 0.05
RANDOM_SEED, DESIGN_SEED = 4242, 4243
FIELDS = ["arm", "idx", "J", "n", "J_canonical", "J_randomStable", "reach", "hash_canonical", "hash_randomStable", "wall_s", "ei", "gp_mean", "gp_sd", "incumbent_mean", "time", "unit", "per_family"]
DRIFT_FIELDS = ["after_evaluations", "J", "J_canonical", "J_randomStable", "n", "hash_canonical", "hash_randomStable", "wall_s", "time"]


class Drift(RuntimeError):
    pass


def read(path): return list(csv.DictReader(open(path, newline=""))) if os.path.exists(path) else []


def append(path, fields, row):
    new = not os.path.exists(path)
    with open(path, "a", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        if new: w.writeheader()
        w.writerow(row)


def ustr(v): return ";".join(f"{x:.6f}" for x in v)
def uparse(s): return np.array([float(x) for x in s.split(";")])


def throwaway_cfg(model, csv_path):
    return {"mode": "eval", "modelPath": model, "seedFrom": 6091, "seedTo": 6100, "objectFriction": 1.0, "evalPerturbScale": 1.0, "evalHoldDecisions": 50, "timeScale": 20, "maxTrialSteps": 6000,
            "headMode": "deterministic", "passIndex": 0, "jobWorkers": 1, "legacyNoReseed": False, "csv": csv_path}


class Campaign:
    def __init__(self, model, noise_floor):
        self.model, self.noise_floor = model, noise_floor
        os.makedirs(PASSES, exist_ok=True)
        self.rows = read(LOG); self.n_eval = len(self.rows)
        d = read(DRIFT); self.ref_first = float(d[0]["J"]) if d else None
        self.rng = np.random.default_rng(DESIGN_SEED + 1)

    def eval(self, arm, idx, u, acq=None):
        r = J(TR.to_theta(u), self.model, f"{arm}_{idx:03d}", PASSES)
        pm = r["per_mode"]
        row = {"arm": arm, "idx": idx, "J": f"{r['J']:.4f}", "n": r["n"], "J_canonical": f"{pm['canonical']['success']:.4f}", "J_randomStable": f"{pm['randomStable']['success']:.4f}",
               "reach": r["reach"], "hash_canonical": r["hash"]["canonical"], "hash_randomStable": r["hash"]["randomStable"], "wall_s": r["wall_s"], "time": time.strftime("%Y-%m-%d %H:%M:%S"), "unit": ustr(u),
               "per_family": json.dumps({k: round(v["success"], 4) for k, v in r["per_family"].items()}, separators=(",", ":"))}
        for k, v in (acq or {}).items(): row[k] = f"{v:.5f}"
        append(LOG, FIELDS, row); self.rows.append(row); self.n_eval += 1
        print(f"{arm} #{idx}: J {r['J']:.3f} (canonical {pm['canonical']['success']:.3f}, randomStable {pm['randomStable']['success']:.3f}, n {r['n']}, {r['wall_s']:.0f} s)" + (f" {acq}" if acq else ""), flush=True)
        if self.n_eval % DRIFT_EVERY == 0: self.drift_check()

    def drift_check(self):
        r = J(TR.to_theta(TR.reference_u()), self.model, f"reference_after_{self.n_eval:03d}", PASSES)
        pm = r["per_mode"]
        append(DRIFT, DRIFT_FIELDS, {"after_evaluations": self.n_eval, "J": f"{r['J']:.4f}", "J_canonical": f"{pm['canonical']['success']:.4f}", "J_randomStable": f"{pm['randomStable']['success']:.4f}", "n": r["n"],
                                     "hash_canonical": r["hash"]["canonical"], "hash_randomStable": r["hash"]["randomStable"], "wall_s": r["wall_s"], "time": time.strftime("%Y-%m-%d %H:%M:%S")})
        print(f"DRIFT reference after {self.n_eval}: J {r['J']:.3f} (first {self.ref_first})", flush=True)
        if self.ref_first is None: self.ref_first = r["J"]
        elif abs(r["J"] - self.ref_first) > DRIFT_LIMIT: raise Drift(f"reference drift: {r['J']:.3f} vs first {self.ref_first:.3f}")

    def arm(self, a): return [r for r in self.rows if r["arm"] == a]

    def gp_fit(self):
        """multi-start hyperparameter fit (added after BO point 30, 2026-09-29): a single L-BFGS start from log ls = 0 converged at n = 30 to
        an all-noise local optimum (signal variance 9e-10, NLL -43.73) while other starts reach NLL -45.5 / -47.6 with signal variance
        ~1.3e-3. Starts: log length-scale 0 / 0.7 / 1.4 x (default signal / noise split, signal = 0.6 of the data variance); the fit with the
        lowest NLL is kept."""
        import math, torch
        obs = self.arm("bo"); X = np.array([uparse(r["unit"]) for r in obs]); y = np.array([float(r["J"]) for r in obs]); v = max(float(y.var()), 1e-4)
        best = None
        for ls0 in (0.0, 0.7, 1.4):
            for frac in (None, 0.6):
                gp = GP(X, y, noise_floor=self.noise_floor)
                with torch.no_grad():
                    gp.log_ls.fill_(ls0)
                    if frac is not None: gp.log_sf2.fill_(math.log(v * frac)); gp.raw_noise.fill_(math.log(math.expm1(max(1e-6, v * (1 - frac) - self.noise_floor + 1e-6))))
                try: nll = gp.fit()
                except RuntimeError: continue
                if np.isfinite(nll) and (best is None or nll < best[1]): best = (gp, nll)
        return best[0], best[1], X, y

    def save_hparams(self, gp, nll, n):
        json.dump(dict(gp.hyperparameters(), nll=nll, dims=TR.DIM_NAMES, noise_floor=self.noise_floor), open(os.path.join(OUT, f"gp_hparams_{n:03d}.json"), "w"), indent=0)

    def next_bo(self):
        gp, nll, X, y = self.gp_fit()
        if len(X) % 10 == 0: self.save_hparams(gp, nll, len(X))
        mu_obs, _ = gp.predict(X); inc = X[int(np.argmax(mu_obs))]; best = float(mu_obs.max())
        cand = np.vstack([self.rng.random((N_CAND, TR.N_DIMS)), np.clip(inc[None, :] + LOCAL_SD * self.rng.standard_normal((N_LOCAL, TR.N_DIMS)), 0, 1)])
        mu, sd = gp.predict(cand); ei = expected_improvement(mu, sd, best); j = int(np.argmax(ei))
        return cand[j], {"ei": float(ei[j]), "gp_mean": float(mu[j]), "gp_sd": float(sd[j]), "incumbent_mean": best}

    def run(self, throwaway=True):
        if throwaway: run_pass(throwaway_cfg(self.model, f"{PASSES}/throwaway_{time.strftime('%H%M%S')}.csv"), max_wait=600)
        if self.ref_first is None: self.drift_check()
        R = TR.sobol(N_RANDOM, seed=RANDOM_SEED); B = TR.sobol(N_INIT, seed=DESIGN_SEED)
        done = {(r["arm"], int(r["idx"])) for r in self.rows}
        for i in range(max(N_RANDOM, N_INIT + N_BO)):
            if i < N_RANDOM and ("random", i) not in done: self.eval("random", i, R[i])
            if i < N_INIT + N_BO and ("bo", i) not in done:
                if i < N_INIT: self.eval("bo", i, B[i])
                else:
                    u, acq = self.next_bo(); self.eval("bo", i, u, acq)
        gp, nll, X, y = self.gp_fit(); self.save_hparams(gp, nll, len(X))
        if self.n_eval % DRIFT_EVERY != 0: self.drift_check()
        print("CAMPAIGN DONE", flush=True)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--model", required=True); ap.add_argument("--noise-floor", type=float, required=True); ap.add_argument("--no-throwaway", action="store_true"); a = ap.parse_args()
    Campaign(a.model, a.noise_floor).run(throwaway=not a.no_throwaway)
