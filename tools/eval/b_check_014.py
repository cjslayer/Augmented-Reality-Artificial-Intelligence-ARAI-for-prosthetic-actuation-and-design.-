"""Run 014 attempt 2, phase B check (CJ 2026-10-05): training stats only (episode logs of phase B, sampled head).

At a check point the report gives, upright and lying separately, gate / lift / hold / success per composition and the end-reason split
(drop / lift-budget timeout / MaxStep / success) over the last window (500k for the checks at 2.75-4.25M, 1M after), plus the pooled upright
lift per 500k window since B began.

Stop rule: pooled UPRIGHT LIFT falls in two consecutive 500k windows (w[i-2] > w[i-1] > w[i]) AND w[i] < 0.5 (switch value 0.771). Upright
success and lying have no stop criterion.

usage: python -m tools.eval.b_check_014 CHECK_STEP [--window 500000]   (appends to results/014/B_CHECKS.md; prints STOP if the rule fires)
"""
import argparse, os, sys
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); sys.path.insert(0, REPO)
from tools.eval import phase_monitor_014 as M
START, LOG, EP = 2249961, "results/014_logs/r2_B.log", "results/014_r2_episodes/B"
COMPS = M.COMPS


def row(R):
    n = len(R)
    if n == 0: return "| - |" * 1
    f = lambda c: sum(1 for r in R if c(r)) / n
    return (f"{n} | {f(lambda r: int(r['transitionStep']) >= 0):.3f} | {f(lambda r: int(r['stepsToLift']) >= 0):.3f} | {f(lambda r: int(r['holdSteps']) > 0):.3f} | "
            f"{f(lambda r: r['success'] == '1'):.3f} | {f(lambda r: r['endReason'] == 'drop'):.3f} | {f(lambda r: r['endReason'] == 'liftBudget'):.3f} | "
            f"{f(lambda r: r['endReason'] == 'maxStep'):.3f} | {f(lambda r: r['endReason'] == 'success'):.3f}")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("check", type=int); ap.add_argument("--window", type=int, default=500000); a = ap.parse_args()
    now, el, s0, e0 = M.last_step(LOG); T = max(1, now - START); R = M.episodes(EP, START, T)
    lift = []; b = START
    while b + 500000 <= now:
        W = [r for r in R if b <= r["_step"] < b + 500000 and r.get("pose") == "canonical"]
        lift.append((b + 500000, sum(int(r["stepsToLift"]) >= 0 for r in W) / max(1, len(W)), len(W))); b += 500000
    stop = len(lift) >= 3 and lift[-3][1] > lift[-2][1] > lift[-1][1] and lift[-1][1] < 0.5
    W = [r for r in R if r["_step"] >= now - a.window]
    md = [f"\n## Check at {a.check / 1e6:.2f}M (trainer step {now}; last {a.window // 1000}k, {now - a.window} to {now}; {(now - s0) / max(1e-9, el - e0):.0f} steps/s)", "",
          "Pooled upright lift per 500k window since B began (2.25M): " + ", ".join(f"{e / 1e6:.2f}M {v:.3f} (n {n})" for e, v, n in lift), "",
          f"Stop rule (two consecutive falls AND below 0.5): **{'FIRES' if stop else 'not met'}**"]
    for pose, lab in (("canonical", "Upright"), ("randomStable", "Lying")):
        P = [r for r in W if r.get("pose") == pose]
        md += ["", f"**{lab}**", "", "| composition | episodes | gate | lift | hold | success | end drop | end liftBudget | end maxStep | end success |", "|---|---|---|---|---|---|---|---|---|---|"]
        for c in COMPS + ["all"]: md.append(f"| {c} | " + row(P if c == "all" else [r for r in P if r["composition"] == c]) + " |")
    s = "\n".join(md) + "\n"; open(os.path.join(REPO, "results/014/B_CHECKS.md"), "a", encoding="utf-8").write(s); print(s)
    if stop: print("STOP")
