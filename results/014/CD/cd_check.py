"""Run 014 C/D phase check (brief 2026-10-08): training stats only (episode logs of one phase, sampled head). Reads via tools/eval/phase_monitor_014.

C (lying only): per composition gate / lift / hold / success and the end-reason split over the last window; pooled lying success per 500k window
and pooled lying lift per 1M window since C began.
  switch to D: pooled lying success >= 0.40 over the trailing 500k window (all of it after C began), or step >= 32M.
  stop: pooled lying lift falls in two consecutive 1M windows (reference before the first window = B's last 1M lying lift, 0.435) AND the latest
  1M window is below 0.35.
D (50/50): upright and lying separately, per composition, end-reason split; pooled upright / lying success per 500k window since D began.
  end: pooled upright success >= 0.72 AND pooled lying success >= 0.30 over the same trailing 500k window, or D+6M, or step >= 36M.
  stop: at or after D+3M, upright success over the trailing 500k below 0.65 -> stop and report.

usage: python results/014/CD/cd_check.py {C|D} START LABEL [--window 1000000] [--append]   (appends to results/014/CD/C_CHECKS.md or D_CHECKS.md)
"""
import argparse, os, sys
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))); sys.path.insert(0, REPO)
from tools.eval import phase_monitor_014 as M
COMPS = M.COMPS; B_LYING_LIFT = 0.435


def row(R):
    x = M.rates(R)
    return "| - |" if x is None else (f"{x['n']} | {x['gate']:.3f} | {x['lift']:.3f} | {x['hold']:.3f} | {x['success']:.3f} | {x['drop']:.3f} | "
                                     f"{x['liftBudget']:.3f} | {x['maxStep']:.3f} | {x['success']:.3f} |")


def windows(R, start, now, w, pose, key):
    out = []; b = start
    while b + w <= now:
        W = [r for r in R if b <= r["_step"] < b + w and r.get("pose") == pose]; x = M.rates(W)
        out.append((b + w, x[key] if x else float("nan"), len(W))); b += w
    return out


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("phase", choices=["C", "D"]); ap.add_argument("start", type=int); ap.add_argument("label")
    ap.add_argument("--window", type=int, default=1000000); ap.add_argument("--append", action="store_true"); a = ap.parse_args()
    log, ep = f"results/014_logs/r2_{a.phase}.log", f"results/014_r2_episodes/{a.phase}"
    now, el, s0, e0 = M.last_step(log); T = max(1, now - a.start); R = M.episodes(ep, a.start, T)
    md = [f"\n## {a.label}: trainer step {now} (phase {a.phase} since {a.start}, +{(now - a.start) / 1e6:.2f}M; table window last {a.window // 1000}k; "
          f"{(now - s0) / max(1e-9, el - e0):.0f} steps/s over this log)", ""]
    tail = lambda pose, w: M.rates([r for r in R if r["_step"] >= now - w and r.get("pose") == pose])
    verdict = []
    if a.phase == "C":
        s5 = windows(R, a.start, now, 500000, "randomStable", "success"); l1 = windows(R, a.start, now, 1000000, "randomStable", "lift")
        md += ["Pooled lying success per 500k window since C began: " + (", ".join(f"{e / 1e6:.2f}M {v:.3f} (n {n})" for e, v, n in s5) or "none complete"),
               "", "Pooled lying lift per 1M window since C began (reference B last 1M: 0.435): " + (", ".join(f"{e / 1e6:.2f}M {v:.3f} (n {n})" for e, v, n in l1) or "none complete")]
        t5 = tail("randomStable", 500000) if now - a.start >= 500000 else None
        sw = (t5 is not None and t5["success"] >= 0.40) or now >= 32000000
        seq = [B_LYING_LIFT] + [v for _, v, _ in l1]
        stop = len(seq) >= 3 and seq[-3] > seq[-2] > seq[-1] and seq[-1] < 0.35
        md += ["", f"Switch criterion (trailing 500k lying success ≥ 0.40, or step ≥ 32M): trailing 500k = "
               + ("n/a (< 500k in C)" if t5 is None else "%.3f (n %d)" % (t5["success"], t5["n"])) + f" → **{'MET' if sw else 'not met'}**",
               f"Stop rule (lying lift falls in two consecutive 1M windows AND < 0.35): **{'FIRES' if stop else 'not met'}**"]
        poses = [("randomStable", "Lying")]
        if sw: verdict.append("SWITCH")
        if stop: verdict.append("STOP")
    else:
        su = windows(R, a.start, now, 500000, "canonical", "success"); sl = windows(R, a.start, now, 500000, "randomStable", "success")
        md += ["Pooled upright success per 500k window since D began: " + (", ".join(f"{e / 1e6:.2f}M {v:.3f} (n {n})" for e, v, n in su) or "none complete"),
               "", "Pooled lying success per 500k window since D began: " + (", ".join(f"{e / 1e6:.2f}M {v:.3f} (n {n})" for e, v, n in sl) or "none complete")]
        ok = now - a.start >= 500000; tu, tl = (tail("canonical", 500000), tail("randomStable", 500000)) if ok else (None, None)
        end = (ok and tu["success"] >= 0.72 and tl["success"] >= 0.30) or now - a.start >= 6000000 or now >= 36000000
        stop = ok and now - a.start >= 3000000 and tu["success"] < 0.65
        md += ["", f"End criterion (same trailing 500k: upright ≥ 0.72 AND lying ≥ 0.30; or D+6M; or 36M): " +
               ("n/a (< 500k in D)" if not ok else f"upright {tu['success']:.3f} (n {tu['n']}), lying {tl['success']:.3f} (n {tl['n']})") + f" → **{'MET' if end else 'not met'}**",
               f"Recovery rule (upright ≥ 0.65 by D+3M): **{'FIRES (upright below 0.65)' if stop else ('not yet applicable' if now - a.start < 3000000 else 'passed')}**"]
        poses = [("canonical", "Upright"), ("randomStable", "Lying")]
        if end: verdict.append("END")
        if stop: verdict.append("STOP")
    W = [r for r in R if r["_step"] >= now - a.window]
    for pose, lab in poses:
        P = [r for r in W if r.get("pose") == pose]
        md += ["", f"**{lab}** (last {a.window // 1000}k)", "", "| composition | episodes | gate | lift | hold | success | end drop | end liftBudget | end maxStep | end success |", "|---|---|---|---|---|---|---|---|---|---|"]
        for c in COMPS + ["all"]: md.append(f"| {c} | " + row(P if c == "all" else [r for r in P if r["composition"] == c]))
    other = [r for r in W if r.get("pose") not in [p for p, _ in poses]]
    if other: md += ["", f"Episodes with another pose in the window: {len(other)}"]
    s = "\n".join(md) + "\n"
    if a.append: open(os.path.join(REPO, f"results/014/CD/{a.phase}_CHECKS.md"), "a", encoding="utf-8").write(s)
    sys.stdout.buffer.write(s.encode("utf-8")); print(" ".join(verdict) or "CONTINUE")
