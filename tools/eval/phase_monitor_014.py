"""Run 014 attempt 2 phase monitor (training stats only; sampled head): pooled gate / lift / hold / success per composition and pose from the
per-player episode logs of ONE phase (each phase writes its own directory, so nothing carries across a switch).

Trainer step of an episode: the phase's players start together at the resume, so an episode at academyStep a in player p is placed at
start + (a / A_p) x T, where A_p is that player's last logged academyStep and T the trainer steps elapsed in the phase (trainer log). Player academy
steps run about 10x trainer steps each (DecisionPeriod 10, 8 players).

usage: python -m tools.eval.phase_monitor_014 --episodes results/014_r2_episodes/A1 --log results/014_logs/r2_A1.log [--start 0] [--bin 500000] [--window 200000]
"""
import argparse, csv, glob, os, re, sys
from collections import defaultdict
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
COMPS = ["3", "4", "5", "6a", "6b", "7"]


def last_step(log):
    s = re.findall(r"Step: (\d+)\. Time Elapsed: ([\d.]+)", open(os.path.join(REPO, log), encoding="utf-8", errors="replace").read())
    return (int(s[-1][0]), float(s[-1][1]), int(s[0][0]), float(s[0][1])) if s else (0, 0.0, 0, 0.0)


def episodes(d, start, T):
    out = []
    for f in glob.glob(os.path.join(REPO, d, "episodes_*.csv")):
        R = [r for r in csv.DictReader(open(f, newline="")) if (r.get("episode") or "").isdigit()]
        if not R: continue
        A = max(int(r["academyStep"]) for r in R) or 1
        for r in R: r["_step"] = start + int(r["academyStep"]) / A * T; out.append(r)
    return out


def rates(R):
    n = len(R)
    if n == 0: return None
    f = lambda c: sum(1 for r in R if c(r)) / n
    return {"n": n, "gate": f(lambda r: int(r["transitionStep"]) >= 0), "lift": f(lambda r: int(r["stepsToLift"]) >= 0), "hold": f(lambda r: int(r["holdSteps"]) > 0),
            "success": f(lambda r: r["success"] == "1"), "drop": f(lambda r: r["endReason"] == "drop"), "liftBudget": f(lambda r: r["endReason"] == "liftBudget"), "maxStep": f(lambda r: r["endReason"] == "maxStep")}


def fmt(x): return "-" if x is None else f"{x['n']} | {x['gate']:.3f} | {x['lift']:.3f} | {x['hold']:.3f} | {x['success']:.3f} | {x['drop']:.3f} | {x['liftBudget']:.3f} | {x['maxStep']:.3f}"


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--episodes", required=True); ap.add_argument("--log", required=True); ap.add_argument("--start", type=int, default=0)
    ap.add_argument("--bin", type=int, default=500000); ap.add_argument("--window", type=int, default=200000); a = ap.parse_args()
    now, el, s0, e0 = last_step(a.log); T = max(1, now - a.start); R = episodes(a.episodes, a.start, T)
    sps = (now - s0) / max(1e-9, el - e0)
    print(f"phase start {a.start}, trainer step {now}, elapsed in phase {T}, {len(R)} episodes, {sps:.0f} steps/s (log)")
    # trend: pooled per bin, overall and per composition (lift)
    print(f"\nTrend, pooled per {a.bin // 1000}k bin (phase steps):")
    print("bin end | episodes | lift | success | " + " | ".join(f"lift {c}" for c in COMPS))
    b = a.start
    while b < now:
        e = min(b + a.bin, now); W = [r for r in R if b <= r["_step"] < e]
        if W:
            x = rates(W); pc = {c: rates([r for r in W if r["composition"] == c]) for c in COMPS}
            print(f"{e / 1e6:.2f}M | {x['n']} | {x['lift']:.3f} | {x['success']:.3f} | " + " | ".join("-" if pc[c] is None else f"{pc[c]['lift']:.3f}" for c in COMPS))
        b = e
    W = [r for r in R if r["_step"] >= now - a.window]
    print(f"\nLast {a.window // 1000}k steps ({now - a.window} to {now}), per composition:")
    print("composition | episodes | gate | lift | hold | success | end drop | end liftBudget | end maxStep")
    for c in COMPS + ["all"]:
        print(f"{c} | " + fmt(rates(W if c == "all" else [r for r in W if r["composition"] == c])))
    if any(r.get("pose") for r in W):
        for pose in ("canonical", "randomStable"):
            P = [r for r in W if r.get("pose") == pose]
            if not P: continue
            print(f"\n  pose {pose}:")
            for c in COMPS + ["all"]: print(f"  {c} | " + fmt(rates(P if c == "all" else [r for r in P if r["composition"] == c])))
