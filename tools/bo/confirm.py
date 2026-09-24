"""2f: confirmation. Top-3 BO theta, top-3 random theta and the reference hand on fresh seeds 8001-8300 (n = 300) at the
campaign level and at perturbation 1.0 (the training level), deterministic head, mu 1.0. Table with Wilson intervals;
paired BO-best - reference and BO-best - random-best (same seeds = same theta-independent draws: mass, spawn, pulses).
Writes results/bo/partB/confirm.csv (resumable), confirm.md.   usage: python -m tools.bo.confirm --perturb S [--mass-range a b]
"""
import argparse, csv, json, math, os, sys, statistics as st
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_space
from tools.bo.evaluate_theta import evaluate, throwaway
from tools.eval.summarize_pass import load, warmup_abort

OUT = "results/bo/partB"; LOG = os.path.join(OUT, "confirm.csv"); SEEDS = (8001, 8300)
FIELDS = ["name", "arm", "src_idx", "perturb", "success", "lo", "hi", "n", "reach_failure", "lifted", "palm", "contacts", "hold", "hash", "wall_s", "csv"]


def paired(a_csv, b_csv):
    A = {r["seed"]: int(r["success"]) for r in load(a_csv) if not warmup_abort(r)}; B = {r["seed"]: int(r["success"]) for r in load(b_csv) if not warmup_abort(r)}
    d = [A[s] - B[s] for s in A if s in B]; n = len(d); m = sum(d) / n; se = (st.stdev(d) if n > 1 else 0.0) / math.sqrt(n)
    return m, m - 1.96 * se, m + 1.96 * se, n


def main(perturb, mass_range):
    top = json.load(open(os.path.join(OUT, "top_evaluated.json"))); os.makedirs(os.path.join(OUT, "confirm_passes"), exist_ok=True)
    cands = [("reference", "reference", "-", theta_space.reference_theta())] + [(f"bo_top{k + 1}", "bo", top["bo_top3_idx"][k], t) for k, t in enumerate(top["bo_top3"])] + [(f"random_top{k + 1}", "random", top["random_top3_idx"][k], t) for k, t in enumerate(top["random_top3"])]
    done = {(r["name"], r["perturb"]) for r in csv.DictReader(open(LOG, newline=""))} if os.path.exists(LOG) else set()
    throwaway()
    for level in (perturb, 1.0):
        for name, arm, idx, th in cands:
            if (name, f"{level:.1f}") in done: continue
            r = evaluate(th, SEEDS, perturb=level, csv=f"{OUT}/confirm_passes/{name}_s{level:.1f}.csv", mass_range=mass_range if level == perturb else None)
            new = not os.path.exists(LOG)
            with open(LOG, "a", newline="") as f:
                w = csv.DictWriter(f, fieldnames=FIELDS); (w.writeheader() if new else None)
                w.writerow({"name": name, "arm": arm, "src_idx": idx, "perturb": f"{level:.1f}", "success": f"{r['success']:.4f}", "lo": f"{r['wilson'][0]:.4f}", "hi": f"{r['wilson'][1]:.4f}", "n": r["n"], "reach_failure": f"{r['reach_failure']:.4f}", "lifted": f"{r['lifted']:.4f}", "palm": f"{r['palm']:.4f}", "contacts": f"{r['contacts']:.3f}", "hold": f"{r['hold']:.1f}", "hash": r["hash"], "wall_s": f"{r['wall_s']:.0f}", "csv": r["csv"]})
            print(f"{name} at {level:.1f}: {r['success']:.3f} [{r['wilson'][0]:.3f}, {r['wilson'][1]:.3f}] reach_fail {r['reach_failure']:.2f}", flush=True)
    rows = list(csv.DictReader(open(LOG, newline=""))); md = ["## Confirmation on seeds 8001-8300 (n = 300, deterministic head, mu 1.0)", "", "| theta | arm | campaign idx | perturb | success [95 % Wilson] | reach failure | lifted | palm | contacts | mean hold |", "|---|---|---|---|---|---|---|---|---|---|"]
    for r in rows: md.append(f"| {r['name']} | {r['arm']} | {r['src_idx']} | {r['perturb']} | {float(r['success']):.3f} [{float(r['lo']):.3f}, {float(r['hi']):.3f}] | {float(r['reach_failure']):.2f} | {float(r['lifted']):.3f} | {float(r['palm']):.3f} | {float(r['contacts']):.2f} | {float(r['hold']):.0f} |")
    by = {(r["name"], r["perturb"]): r for r in rows}; md += ["", "### Paired differences (same seeds; normal 95 % CI on the mean paired difference)", "", "| comparison | perturb | mean difference | 95 % CI | n |", "|---|---|---|---|---|"]
    for level in (f"{perturb:.1f}", "1.0"):
        lv = [r for r in rows if r["perturb"] == level]
        if not lv: continue
        bo_best = max((r for r in lv if r["arm"] == "bo"), key=lambda r: float(r["success"]), default=None); rnd_best = max((r for r in lv if r["arm"] == "random"), key=lambda r: float(r["success"]), default=None)
        if bo_best and ("reference", level) in by:
            m, lo, hi, n = paired(by[(bo_best["name"], level)]["csv"], by[("reference", level)]["csv"]); md.append(f"| BO-best ({bo_best['name']}) - reference | {level} | {m:+.3f} | [{lo:+.3f}, {hi:+.3f}] | {n} |")
        if bo_best and rnd_best:
            m, lo, hi, n = paired(by[(bo_best["name"], level)]["csv"], by[(rnd_best["name"], level)]["csv"]); md.append(f"| BO-best ({bo_best['name']}) - random-best ({rnd_best['name']}) | {level} | {m:+.3f} | [{lo:+.3f}, {hi:+.3f}] | {n} |")
    txt = "\n".join(md) + "\n"; open(os.path.join(OUT, "confirm.md"), "w", encoding="utf-8").write(txt); print(txt)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--perturb", type=float, required=True); ap.add_argument("--mass-range", nargs=2, type=float); a = ap.parse_args()
    main(a.perturb, tuple(a.mass_range) if a.mass_range else None)
