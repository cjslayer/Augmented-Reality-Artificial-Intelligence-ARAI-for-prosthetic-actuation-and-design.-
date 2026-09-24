"""2c / 2d: BO Part B campaign on the articulated hand with the deployed 012 policy through the Editor harness.

Objective: success rate at the chosen perturbation level (deterministic head, mu 1.0, K = 50, seeds 4001-4100, one pass
per theta). Arms: 'random' = 100 theta from the training distribution (seed 4242); 'bo' = 30 initial Sobol points over the
feasible box (seed 4243; mask bits Bernoulli(0.8) from the same stream, infeasible masks recorded and redrawn) then 70
sequential points from a GP (ARD Matern-5/2 on standardized theta, fitted noise variance with floor 0.002, mean-centered;
tools/bo/gp.py) with feasibility-weighted expected improvement over 4096 random feasible candidates + 512 local
perturbations of the current best. Feasibility = tools/bo/feasibility.feasible(theta, oracle=False) (bounds + mask rule;
the scripted oracle is not used, see feasibility.py). Drift control: the reference hand is evaluated before the campaign
and after every 20 evaluations; a drift of more than 0.10 from the first value stops the campaign.
Log: results/bo/partB/campaign.csv (resumable), drift.csv, gp_hparams_<n>.json every 10 BO points.
usage: python -m tools.bo.campaign --perturb S [--mass-range a b]
"""
import argparse, csv, json, os, sys, time
import numpy as np
import torch
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_space, feasibility
from tools.bo.evaluate_theta import evaluate, throwaway
from tools.bo.gp import GP, expected_improvement

OUT = "results/bo/partB"; LOG = os.path.join(OUT, "campaign.csv"); DRIFT = os.path.join(OUT, "drift.csv")
SEEDS = (4001, 4100); N_RANDOM, N_INIT, N_BO = 100, 30, 70; DRIFT_EVERY, DRIFT_LIMIT = 20, 0.10; N_CAND, N_LOCAL = 4096, 512
RANDOM_SEED, DESIGN_SEED = 4242, 4243
FIELDS = ["arm", "idx", "feasible", "objective", "lo", "hi", "n", "reach_failure", "lifted", "palm", "contacts", "hold", "hash", "wall_s", "csv", "acq", "ei", "gp_mean", "gp_sd", "time", "unit", "theta"]


class Drift(RuntimeError):
    pass


def read_log():
    return list(csv.DictReader(open(LOG, newline=""))) if os.path.exists(LOG) else []


def append(path, fields, row):
    new = not os.path.exists(path)
    with open(path, "a", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields); (w.writeheader() if new else None); w.writerow(row)


def unit_str(v): return ";".join(f"{x:.6f}" for x in v)


class Campaign:
    def __init__(self, perturb, mass_range):
        self.perturb, self.mass_range = perturb, mass_range; os.makedirs(os.path.join(OUT, "campaign_passes"), exist_ok=True)
        self.rows = read_log(); self.n_eval = sum(1 for r in self.rows if r["objective"] not in ("", None) and r["arm"] in ("bo", "random"))
        self.ref = theta_space.reference_theta(); self.ref_first = None
        for r in csv.DictReader(open(DRIFT, newline="")) if os.path.exists(DRIFT) else []:
            if self.ref_first is None: self.ref_first = float(r["objective"])

    def evaluate_theta(self, arm, idx, theta, acq=None):
        tag = f"{arm}_{idx:03d}"; r = evaluate(theta, SEEDS, perturb=self.perturb, csv=f"{OUT}/campaign_passes/{tag}.csv", mass_range=self.mass_range)
        row = {"arm": arm, "idx": idx, "feasible": 1, "objective": f"{r['success']:.4f}", "lo": f"{r['wilson'][0]:.4f}", "hi": f"{r['wilson'][1]:.4f}", "n": r["n"], "reach_failure": f"{r['reach_failure']:.4f}", "lifted": f"{r['lifted']:.4f}",
               "palm": f"{r['palm']:.4f}", "contacts": f"{r['contacts']:.3f}", "hold": f"{r['hold']:.1f}", "hash": r["hash"], "wall_s": f"{r['wall_s']:.0f}", "csv": r["csv"], "time": time.strftime("%Y-%m-%d %H:%M:%S"),
               "unit": unit_str(theta_space.to_unit(theta)), "theta": json.dumps(theta, separators=(",", ":"))}
        row.update({k: (f"{v:.5f}" if isinstance(v, float) else v) for k, v in (acq or {}).items()})
        append(LOG, FIELDS, row); self.rows.append(row); self.n_eval += 1
        print(f"{arm} #{idx}: success {r['success']:.2f} [{r['wilson'][0]:.2f}, {r['wilson'][1]:.2f}] reach_fail {r['reach_failure']:.2f} ({r['wall_s']:.0f} s)" + (f" acq {acq}" if acq else ""), flush=True)
        if self.n_eval % DRIFT_EVERY == 0: self.drift_check()
        return row

    def record_infeasible(self, arm, idx, theta):
        row = {"arm": arm, "idx": idx, "feasible": 0, "objective": "", "time": time.strftime("%Y-%m-%d %H:%M:%S"), "unit": unit_str(theta_space.to_unit(theta)), "theta": json.dumps(theta, separators=(",", ":"))}
        append(LOG, FIELDS, row); self.rows.append(row)

    def drift_check(self):
        r = evaluate(self.ref, SEEDS, perturb=self.perturb, csv=f"{OUT}/campaign_passes/reference_{self.n_eval:03d}.csv", mass_range=self.mass_range)
        append(DRIFT, ["after_evaluations", "objective", "lo", "hi", "reach_failure", "hash", "time"], {"after_evaluations": self.n_eval, "objective": f"{r['success']:.4f}", "lo": f"{r['wilson'][0]:.4f}", "hi": f"{r['wilson'][1]:.4f}", "reach_failure": f"{r['reach_failure']:.4f}", "hash": r["hash"], "time": time.strftime("%Y-%m-%d %H:%M:%S")})
        print(f"reference after {self.n_eval} evaluations: {r['success']:.3f} (first {self.ref_first})", flush=True)
        if self.ref_first is None: self.ref_first = r["success"]
        elif abs(r["success"] - self.ref_first) > DRIFT_LIMIT: raise Drift(f"reference drift {r['success']:.3f} vs {self.ref_first:.3f}")

    # ---- arms
    def arm_rows(self, arm, feasible_only=True):
        return [r for r in self.rows if r["arm"] == arm and (not feasible_only or str(r["feasible"]) == "1")]

    def run_random(self):
        thetas = theta_space.sample_training_distribution(N_RANDOM, RANDOM_SEED); done = {int(r["idx"]) for r in self.arm_rows("random")}
        for i, th in enumerate(thetas):
            if i in done: continue
            if not feasibility.feasible(th, oracle=False)[0]: self.record_infeasible("random", i, th); continue   # cannot happen for training draws
            self.evaluate_theta("random", i, th)

    def design(self):
        """30 feasible Sobol points over the box (unit coordinates; mask bits thresholded at 0.2 = Bernoulli(0.8)); infeasible draws are recorded"""
        eng = torch.quasirandom.SobolEngine(theta_space.N_DIMS, scramble=True, seed=DESIGN_SEED); pts = []; rejected = []
        while len(pts) < N_INIT:
            u = eng.draw(1).numpy()[0]; th = theta_space.from_unit(np.where([d[0] == "mask" for d in theta_space.DIMS], (u >= 0.2).astype(float), u))
            (pts if feasibility.feasible(th, oracle=False)[0] else rejected).append(th)
        return pts, rejected

    def candidates(self, rng, best_unit):
        u = rng.random((N_CAND, theta_space.N_DIMS)); is_mask = np.array([d[0] == "mask" for d in theta_space.DIMS]); u[:, is_mask] = (u[:, is_mask] < 0.8).astype(float)
        loc = np.clip(best_unit[None, :] + 0.05 * rng.standard_normal((N_LOCAL, theta_space.N_DIMS)), 0, 1); flip = rng.random((N_LOCAL, theta_space.N_DIMS)) < 0.05
        loc[:, is_mask] = np.where(flip[:, is_mask], 1 - best_unit[None, is_mask], best_unit[None, is_mask])
        allc = np.vstack([u, loc]); thetas = [theta_space.from_unit(v) for v in allc]; ok = np.array([feasibility.feasible(t, oracle=False)[0] for t in thetas])
        return allc, thetas, ok

    def run_bo(self):
        done = {int(r["idx"]): r for r in self.arm_rows("bo", feasible_only=False)}
        pts, rejected = self.design()
        for j, th in enumerate(rejected):
            if -(j + 1) not in done: self.record_infeasible("bo", -(j + 1), th)
        for i, th in enumerate(pts):
            if i not in done: self.evaluate_theta("bo", i, th)
        rng = np.random.default_rng(DESIGN_SEED + 1)
        for i in range(N_INIT, N_INIT + N_BO):
            if i in done: continue
            obs = self.arm_rows("bo"); X = np.array([[float(x) for x in r["unit"].split(";")] for r in obs]); y = np.array([float(r["objective"]) for r in obs])
            gp = GP(X, y); nll = gp.fit(); best = y.max(); best_unit = X[y.argmax()]
            if len(obs) % 10 == 0: json.dump(dict(gp.hyperparameters(), nll=nll, dims=theta_space.DIM_NAMES), open(os.path.join(OUT, f"gp_hparams_{len(obs):03d}.json"), "w"), indent=0)
            allc, thetas, ok = self.candidates(rng, best_unit); mu, sd = gp.predict(allc); ei = expected_improvement(mu, sd, best); acq = ei * ok
            j = int(np.argmax(acq)); self.evaluate_theta("bo", i, thetas[j], {"acq": float(acq[j]), "ei": float(ei[j]), "gp_mean": float(mu[j]), "gp_sd": float(sd[j])})
        obs = self.arm_rows("bo"); X = np.array([[float(x) for x in r["unit"].split(";")] for r in obs]); y = np.array([float(r["objective"]) for r in obs]); gp = GP(X, y); nll = gp.fit()
        json.dump(dict(gp.hyperparameters(), nll=nll, dims=theta_space.DIM_NAMES), open(os.path.join(OUT, f"gp_hparams_{len(obs):03d}.json"), "w"), indent=0)

    def run(self):
        throwaway()
        if not os.path.exists(DRIFT): self.drift_check()
        self.run_bo(); self.run_random(); self.drift_check(); print("campaign done", flush=True)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--perturb", type=float, required=True); ap.add_argument("--mass-range", nargs=2, type=float); a = ap.parse_args()
    Campaign(a.perturb, tuple(a.mass_range) if a.mass_range else None).run()
