"""2e: campaign analysis (read-only on results/bo/partB/campaign.csv and drift.csv): best-so-far tables for the BO and
random arms at equal evaluation budgets, the best theta of each arm, the reference-hand objective, the GP's top-3
predicted theta over a large feasible candidate set, and the top-10 theta dimensions by inverse GP length-scale.
Writes results/bo/partB/analysis.md, best_so_far.csv, top_predicted.json.   usage: python -m tools.bo.analysis
"""
import csv, json, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_space, feasibility
from tools.bo.gp import GP

OUT = "results/bo/partB"


def rows(arm):
    return [r for r in csv.DictReader(open(os.path.join(OUT, "campaign.csv"), newline="")) if r["arm"] == arm and str(r["feasible"]) == "1" and r["objective"]]


def best_so_far(rs):
    best, out = -1.0, []
    for r in rs: best = max(best, float(r["objective"])); out.append(best)
    return out


def describe(theta):
    f = theta_space.summary_features(theta); return f"lenMean {f['lenMean']:.3f}, kFingerMean {f['kFingerMean']:.3f}, zetaMean {f['zetaMean']:.3f}, inertiaMean {f['inertiaScaleMean']:.3f}, active {f['activeGroups']}, mask {''.join(str(b) for b in theta['mask'])}"


def main():
    bo, rnd = rows("bo"), rows("random"); bo.sort(key=lambda r: int(r["idx"])); rnd.sort(key=lambda r: int(r["idx"]))
    b_bo, b_rnd = best_so_far(bo), best_so_far(rnd); n = min(len(b_bo), len(b_rnd))
    with open(os.path.join(OUT, "best_so_far.csv"), "w", newline="") as f:
        w = csv.writer(f); w.writerow(["evaluations", "bo_best", "random_best"]); [w.writerow([i + 1, f"{b_bo[i]:.4f}", f"{b_rnd[i]:.4f}"]) for i in range(n)]
    ref = list(csv.DictReader(open(os.path.join(OUT, "drift.csv"), newline=""))) if os.path.exists(os.path.join(OUT, "drift.csv")) else []
    md = ["## Campaign analysis", "", f"BO arm: {len(bo)} evaluated points (30 Sobol + {max(0, len(bo) - 30)} GP-guided); random arm: {len(rnd)} training-distribution draws. Reference hand: " + ", ".join(f"{float(r['objective']):.2f}" for r in ref) + " over the drift checks.", "",
          "| evaluations | BO best so far | random best so far |", "|---|---|---|"]
    for k in [1, 5, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100]:
        if k <= n: md.append(f"| {k} | {b_bo[k - 1]:.3f} | {b_rnd[k - 1]:.3f} |")
    top_bo = sorted(bo, key=lambda r: -float(r["objective"]))[:3]; top_rnd = sorted(rnd, key=lambda r: -float(r["objective"]))[:3]
    md += ["", "### Best theta per arm (top 3)", "", "| arm | idx | objective [Wilson] | reach failure | description |", "|---|---|---|---|---|"]
    for arm, rs in (("bo", top_bo), ("random", top_rnd)):
        for r in rs: md.append(f"| {arm} | {r['idx']} | {float(r['objective']):.3f} [{float(r['lo']):.3f}, {float(r['hi']):.3f}] | {float(r['reach_failure']):.2f} | {describe(json.loads(r['theta']))} |")
    # GP on the BO data: relevance and predicted optimum
    X = np.array([[float(x) for x in r["unit"].split(";")] for r in bo]); y = np.array([float(r["objective"]) for r in bo]); gp = GP(X, y); nll = gp.fit(); hp = gp.hyperparameters()
    order = np.argsort(hp["length_scales"]); md += ["", f"### GP on the BO arm ({len(bo)} points): noise variance {hp['noise_variance']:.4f}, signal variance {hp['signal_variance']:.4f}, nll {nll:.2f}", "", "Top-10 theta dimensions by inverse length-scale (standardized inputs; a short length-scale = the objective changes along that dimension):", "", "| rank | dimension | length-scale |", "|---|---|---|"]
    for k, i in enumerate(order[:10]): md.append(f"| {k + 1} | {theta_space.DIM_NAMES[i]} | {hp['length_scales'][i]:.3f} |")
    rng = np.random.default_rng(99); u = rng.random((20000, theta_space.N_DIMS)); is_mask = np.array([d[0] == "mask" for d in theta_space.DIMS]); u[:, is_mask] = (u[:, is_mask] < 0.8).astype(float)
    u = np.vstack([u, X]); thetas = [theta_space.from_unit(v) for v in u]; ok = np.array([feasibility.feasible(t, oracle=False)[0] for t in thetas]); mu, sd = gp.predict(u); mu[~ok] = -1
    top = np.argsort(-mu)[:3]; pred = [{"gp_mean": float(mu[j]), "gp_sd": float(sd[j]), "theta": thetas[j], "unit": u[j].tolist()} for j in top]
    json.dump(pred, open(os.path.join(OUT, "top_predicted.json"), "w"), indent=0)
    md += ["", "### GP top-3 predicted theta (mean over 20 000 feasible random candidates plus the evaluated points)", "", "| rank | GP mean | GP sd | description |", "|---|---|---|---|"]
    for k, p in enumerate(pred): md.append(f"| {k + 1} | {p['gp_mean']:.3f} | {p['gp_sd']:.3f} | {describe(p['theta'])} |")
    txt = "\n".join(md) + "\n"; open(os.path.join(OUT, "analysis.md"), "w", encoding="utf-8").write(txt); print(txt)
    json.dump({"bo_top3": [json.loads(r["theta"]) for r in top_bo], "random_top3": [json.loads(r["theta"]) for r in top_rnd], "bo_top3_idx": [r["idx"] for r in top_bo], "random_top3_idx": [r["idx"] for r in top_rnd]}, open(os.path.join(OUT, "top_evaluated.json"), "w"), indent=0)


if __name__ == "__main__":
    main()
