"""Evaluate one fixed theta with the deployed policy through the Editor harness (mode eval, thetaFixed): one pass.

evaluate(theta, seeds=(4001, 4100), perturb=1.0, mu=1.0, hold_decisions=50, head="deterministic", model="results/012/Prosthetic.onnx",
         csv="results/bo/partB/passes/<tag>.csv", mass_range=None) -> dict with success, lifted, palm, contacts, hold, reach_failure,
n, pass hash, wall time, csv path. Zero-step rows follow tools/eval/summarize_pass.py (first-episode warm-up abort excluded, other
zero-step rows = reach failures counted as failures). One job worker, per-episode reseed, passIndex 0 (the instrument).

usage: python -m tools.bo.evaluate_theta --theta theta.json [--seeds 4001 4100] [--perturb 1.0] [--csv path]  (or --reference)
"""
import argparse, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_space
from tools.bo.gates_runner import run_pass, pass_hash, REPO
from tools.eval.summarize_pass import summarize, load, reach_failure, warmup_abort

DEFAULT_MODEL = "results/012/Prosthetic.onnx"


def make_config(theta, seeds=(4001, 4100), perturb=1.0, mu=1.0, hold_decisions=50, head="deterministic", model=DEFAULT_MODEL, csv=None, mass_range=None, time_scale=20):
    cfg = {"mode": "eval", "modelPath": model, "seedFrom": int(seeds[0]), "seedTo": int(seeds[1]), "objectFriction": float(mu), "evalPerturbScale": float(perturb),
           "evalHoldDecisions": int(hold_decisions), "timeScale": time_scale, "maxTrialSteps": 6000, "headMode": head, "passIndex": 0, "jobWorkers": 1, "legacyNoReseed": False, "csv": csv}
    if theta is not None: cfg.update(theta_space.harness_fields(theta))
    if mass_range: cfg["massMin"], cfg["massMax"] = float(mass_range[0]), float(mass_range[1])
    return cfg


def evaluate(theta, seeds=(4001, 4100), perturb=1.0, mu=1.0, hold_decisions=50, head="deterministic", model=DEFAULT_MODEL, csv=None, mass_range=None, max_wait=2400):
    if theta is not None and not theta_space.in_bounds(theta): raise ValueError("theta outside the MorphologyManager bounds")
    if csv is None: csv = f"results/bo/partB/passes/pass_{int(time.time())}.csv"
    cfg = make_config(theta, seeds, perturb, mu, hold_decisions, head, model, csv, mass_range)
    path, wall = run_pass(cfg, max_wait=max_wait)
    s = summarize(path); rows = [r for r in load(path) if not warmup_abort(r)]
    exp = theta_space.summary_features(theta) if theta is not None else None
    if exp is not None and rows:   # cross-check the pinned theta against what the harness logged
        got = rows[0]
        for key in ("kFingerMean", "zetaMean", "inertiaScaleMean", "lenMean"):
            if abs(float(got[key]) - exp[key]) > 0.01: raise RuntimeError(f"theta cross-check failed: {key} logged {got[key]} expected {exp[key]:.3f}")
        if int(got["activeGroups"]) != exp["activeGroups"]: raise RuntimeError("theta cross-check failed: activeGroups")
    return {"success": s["success"], "wilson": (s["lo"], s["hi"]), "n": s["n"], "excluded_zero_step": s["excluded_zero_step"], "reach_failure": s["reach_failures"] / s["n"] if s["n"] else float("nan"),
            "lifted": s["lifted"], "palm": s["palm"], "contacts": s["contacts"], "hold": s["meanHold"], "ends": s["ends"], "hash": pass_hash(path), "wall_s": wall, "csv": os.path.relpath(path, REPO).replace("\\", "/"),
            "seeds": list(seeds), "perturb": perturb, "mu": mu, "head": head, "model": model, "mass_range": list(mass_range) if mass_range else None}


def throwaway(seeds=(6091, 6100), csv="results/bo/partB/passes/throwaway.csv"):
    """a discarded short pass (the instrument's throwaway after a recompile or an Editor launch)"""
    run_pass(make_config(None, seeds=seeds, csv=csv), max_wait=600)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0]); ap.add_argument("--theta"); ap.add_argument("--reference", action="store_true")
    ap.add_argument("--seeds", nargs=2, type=int, default=[4001, 4100]); ap.add_argument("--perturb", type=float, default=1.0); ap.add_argument("--mu", type=float, default=1.0)
    ap.add_argument("--head", default="deterministic"); ap.add_argument("--model", default=DEFAULT_MODEL); ap.add_argument("--csv"); ap.add_argument("--mass-range", nargs=2, type=float)
    a = ap.parse_args()
    theta = theta_space.reference_theta() if a.reference else json.load(open(a.theta))
    print(json.dumps(evaluate(theta, tuple(a.seeds), a.perturb, a.mu, head=a.head, model=a.model, csv=a.csv, mass_range=a.mass_range), indent=1))
