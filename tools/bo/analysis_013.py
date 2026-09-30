"""Brief 3b: Part C campaign analysis (read-only) -> results/bo/partC/ANALYSIS.md and best_so_far.csv.

Best-so-far per arm (observed J, and the per-mode values of the best point), GP relevance (top-10 dimensions by inverse length-scale from
the last gp_hparams file), best theta of each arm against the reference in natural units, drift record.
"""
import csv, glob, json, os, statistics as st, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_reduced as TR
from tools.bo.campaign_013 import LOG, DRIFT, OUT, uparse
from tools.bo.gates_runner import REPO


def main():
    rows = list(csv.DictReader(open(os.path.join(REPO, LOG), newline=""))); md = ["# Part C campaign analysis", ""]
    arms = {a: sorted([r for r in rows if r["arm"] == a], key=lambda r: int(r["idx"])) for a in ("random", "bo")}
    curve = []
    for a, rs in arms.items():
        best = -1
        for i, r in enumerate(rs, 1): best = max(best, float(r["J"])); curve.append({"arm": a, "n": i, "J": r["J"], "best_so_far": f"{best:.4f}"})
    with open(os.path.join(REPO, OUT, "best_so_far.csv"), "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["arm", "n", "J", "best_so_far"]); w.writeheader(); w.writerows(curve)
    md += ["## Best-so-far (observed J, 128 episodes per point)", "", "| evaluations | random | BO |", "|---|---|---|"]
    bs = {a: [float(c["best_so_far"]) for c in curve if c["arm"] == a] for a in arms}
    for n in (1, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100):
        md.append(f"| {n} | " + " | ".join(f"{bs[a][n - 1]:.3f}" if len(bs[a]) >= n else "-" for a in ("random", "bo")) + " |")
    for a in ("random", "bo"):
        v = [float(r["J"]) for r in arms[a]]
        if v: md.append(f"\n{a}: mean J {st.mean(v):.3f}, SD {st.stdev(v):.3f}, min {min(v):.3f}, max {max(v):.3f}" + (f"; BO sequential points 31-100 mean {st.mean(v[30:]):.3f} vs Sobol 1-30 mean {st.mean(v[:30]):.3f}" if a == "bo" and len(v) > 30 else ""))
    hp = sorted(glob.glob(os.path.join(REPO, OUT, "gp_hparams_*.json")))
    if hp:
        h = json.load(open(hp[-1])); ls = np.array(h["length_scales"]); order = np.argsort(ls)
        md += ["", f"## GP relevance ({os.path.basename(hp[-1])}: n {h['n']}, signal variance {h['signal_variance']:.4f}, noise variance {h['noise_variance']:.4f}, noise floor {h['noise_floor']})", "",
               "Length scales are in standardized-input units (inputs z-scored per dimension); smaller = more relevant.", "", "| rank | dimension | length scale | inverse |", "|---|---|---|---|"]
        for k, j in enumerate(order[:10], 1): md.append(f"| {k} | {TR.DIM_NAMES[j]} | {ls[j]:.2f} | {1 / ls[j]:.3f} |")
        md += ["", "Length-scale history (top-5 dimensions of the last fit):", "", "| file | " + " | ".join(TR.DIM_NAMES[j] for j in order[:5]) + " |", "|---|" + "---|" * 5]
        for f in hp:
            l2 = json.load(open(f))["length_scales"]; md.append(f"| {os.path.basename(f)} | " + " | ".join(f"{l2[j]:.2f}" for j in order[:5]) + " |")
    ref = TR.natural(TR.reference_u())
    best = {a: max(arms[a], key=lambda r: float(r["J"])) for a in arms if arms[a]}
    md += ["", "## Best theta per arm vs reference (natural units; k in N m/rad, thumb k = thumb-base group, the thumb-end group takes the same quantile of its range)", "",
           "| dimension | reference | " + " | ".join(f"{a} best (#{best[a]['idx']}, J {float(best[a]['J']):.3f})" for a in best) + " |", "|---|---|" + "---|" * len(best)]
    nat = {a: TR.natural(uparse(best[a]["unit"])) for a in best}
    for n in TR.DIM_NAMES: md.append(f"| {n} | {ref[n]:.3f} | " + " | ".join(f"{nat[a][n]:.3f}" for a in best) + " |")
    d = list(csv.DictReader(open(os.path.join(REPO, DRIFT), newline=""))) if os.path.exists(os.path.join(REPO, DRIFT)) else []
    if d:
        md += ["", "## Reference drift (J of the reference theta during the campaign)", "", "| after evaluations | J | canonical | random stable | hash (canonical / random stable) |", "|---|---|---|---|---|"]
        md += [f"| {r['after_evaluations']} | {float(r['J']):.3f} | {float(r['J_canonical']):.3f} | {float(r['J_randomStable']):.3f} | {r['hash_canonical'][:8]} / {r['hash_randomStable'][:8]} |" for r in d]
        v = [float(r["J"]) for r in d]; md.append(f"\nmax |J - first| = {max(abs(x - v[0]) for x in v):.3f} (limit 0.10); SD {st.stdev(v) if len(v) > 1 else float('nan'):.3f}")
    open(os.path.join(REPO, OUT, "ANALYSIS.md"), "w", encoding="utf-8").write("\n".join(md) + "\n"); sys.stdout.buffer.write(("\n".join(md) + "\n").encode("utf-8"))


if __name__ == "__main__":
    main()
