"""Run 014 smoke check (checkpoint 3b, item 6): 500k steps in the slot layout (Config/run_014_smoke.yaml, run id 014_smoke).

Checks:
  - every composition (3, 4, 5, 6a, 6b, 7) drawn at its design-doc share, from the per-episode logs (exact counts; 95 % binomial interval);
  - per-digit-count StatsRecorder channels populated in TensorBoard (Success / Gate / Lift by Digits3..7, Success/Digits6a and 6b, Comp/Share*, Effort/Digits*);
  - spawn rejection rate per composition (overlap filter: rejected / reach-accepted candidates) vs the 5-digit rate; flag a difference > 3 percentage points;
  - no NaN / inf in any TensorBoard scalar; effort return finite on 3-digit hands;
  - one logged episode per composition in which the gate, the lift and the hold all fire.

usage: python -m tools.eval.smoke_014 [--run results/014_smoke] [--episodes results/014_smoke_episodes] [--out results/014/SMOKE_014.md]
"""
import argparse, csv, glob, math, os, sys
from collections import defaultdict
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
COMPS = ["3", "4", "5", "6a", "6b", "7"]
DESIGN = {"3": 0.10, "4": 0.15, "5": 0.45, "6a": 0.075, "6b": 0.075, "7": 0.15}


def wilson(k, n, z=1.96):
    if n == 0: return float("nan"), float("nan")
    p = k / n; d = 1 + z * z / n; c = (p + z * z / (2 * n)) / d; h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d
    return c - h, c + h


def scalars(run):
    from tensorboard.backend.event_processing.event_accumulator import EventAccumulator
    out = defaultdict(list)
    for f in sorted(glob.glob(os.path.join(REPO, run, "Prosthetic", "events.out.tfevents.*"))):
        ea = EventAccumulator(f, size_guidance={"scalars": 0}); ea.Reload()
        for t in ea.Tags()["scalars"]:
            for e in ea.Scalars(t): out[t].append((e.step, e.value))
    return out


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--run", default="results/014_smoke"); ap.add_argument("--episodes", default="results/014_smoke_episodes"); ap.add_argument("--out", default="results/014/SMOKE_014.md")
    a = ap.parse_args()
    rows = []
    for f in glob.glob(os.path.join(REPO, a.episodes, "episodes_*.csv")):
        for r in csv.DictReader(open(f, newline="")):
            if (r.get("episode") or "").isdigit(): rows.append(r)   # every ended episode; a MaxStep end (reach failure) is logged after ML-Agents reset its step count, so steps reads 0 there
    S = scalars(a.run)
    md = [f"# Run 014 smoke ({a.run}, slot layout)", "", f"Episode logs: {len(glob.glob(os.path.join(REPO, a.episodes, 'episodes_*.csv')))} files, {len(rows)} episodes.", ""]
    steps = max((s for v in S.values() for s, _ in v), default=0); md.append(f"TensorBoard: last summary step {steps}, {len(S)} scalar tags.")
    # composition shares
    n = len(rows); cnt = {c: sum(1 for r in rows if r["composition"] == c) for c in COMPS}
    md += ["", "## Composition shares (episode logs)", "", "| composition | slots | episodes | share [95 %] | design | inside? |", "|---|---|---|---|---|---|"]
    all_in = True
    for c in COMPS:
        lo, hi = wilson(cnt[c], n); ok = lo <= DESIGN[c] <= hi; all_in &= ok
        sl = next((r["slots"] for r in rows if r["composition"] == c), "-")
        md.append(f"| {c} | {sl} | {cnt[c]} | {cnt[c] / max(n, 1):.3f} [{lo:.3f}, {hi:.3f}] | {DESIGN[c]:.3f} | {'yes' if ok else '**no**'} |")
    md.append(f"\nAll six shares consistent with the design: **{'YES' if all_in else 'NO'}**")
    # channels
    want = [f"{k}/Digits{d}" for k in ("Success", "Gate", "Lift") for d in (3, 4, 5, 6, 7)] + ["Success/Digits6a", "Success/Digits6b"] + [f"Comp/Share{c}" for c in COMPS] + [f"Effort/Digits{c}" for c in ("3", "4", "5", "6a", "6b", "7")] + [f"Spawn/OverlapRejectRate{c}" for c in COMPS]
    md += ["", "## Per-digit-count StatsRecorder channels (TensorBoard)", "", "| channel | summaries | last value |", "|---|---|---|"]
    missing = []
    for t in want:
        v = S.get(t, []); md.append(f"| {t} | {len(v)} | {v[-1][1]:.4f} |" if v else f"| {t} | 0 | **missing** |")
        if not v: missing.append(t)
    md.append(f"\nAll channels populated: **{'YES' if not missing else 'NO (' + ', '.join(missing) + ')'}**")
    # NaN
    bad = [t for t, v in S.items() if any(not math.isfinite(x) for _, x in v)]
    md += ["", f"## NaN / inf in any scalar: **{'none' if not bad else ', '.join(bad)}**"]
    # effort on 3-digit hands
    e3 = [float(r["effortReturn"]) for r in rows if r["composition"] == "3"]
    fin = all(math.isfinite(x) for x in e3)
    md += ["", f"## Effort return on 3-digit hands: {len(e3)} episodes, all finite: **{'YES' if fin and e3 else 'NO'}**" + (f"; mean {sum(e3) / len(e3):.5f}, min {min(e3):.5f}, max {max(e3):.5f}" if e3 else ""), ""]
    md += ["| composition | episodes | mean effort return | mean squared action per present joint and step |", "|---|---|---|---|"]
    joints = {"3": 13, "4": 16, "5": 19, "6a": 22, "6b": 21, "7": 24}
    for c in COMPS:
        e = [(float(r["effortReturn"]), int(r["steps"])) for r in rows if r["composition"] == c and int(r["steps"]) > 0]   # per-step rate: episodes with a known step count
        if e: md.append(f"| {c} | {len(e)} | {sum(x for x, _ in e) / len(e):.5f} | {sum(-x / s for x, s in e) / len(e) / joints[c] / 2e-5:.4f} |")
    # spawn rejection
    md += ["", "## Spawn rejection by the open-hand overlap filter (rejected / reach-accepted candidates)", "", "| composition | candidates | rejected | rate | rate − 5-digit | fallback episodes |", "|---|---|---|---|---|---|"]
    rate = {}
    for c in COMPS:
        R = [r for r in rows if r["composition"] == c]; t = sum(int(r["spawnTested"]) for r in R); j = sum(int(r["spawnOverlapRejects"]) for r in R)
        rate[c] = (j / t if t else float("nan"), t, j, sum(int(r["spawnFallback"]) for r in R))
    flags = []
    for c in COMPS:
        rt, t, j, fb = rate[c]; d = rt - rate["5"][0]
        if abs(d) > 0.03: flags.append(c)
        md.append(f"| {c} | {t} | {j} | {rt:.4f} | {d:+.4f}{' **> 3 pp**' if abs(d) > 0.03 else ''} | {fb} |")
    md.append(f"\nCompositions differing from the 5-digit rate by more than 3 percentage points: **{', '.join(flags) if flags else 'none'}**")
    # phases per composition
    md += ["", "## One episode per composition with the gate, the lift and the hold firing (episode logs)", "",
           "| composition | episode | success | end | steps | transition step | steps to lift | hold steps / needed (K) | contacts | thumb | palm |", "|---|---|---|---|---|---|---|---|---|---|---|"]
    all_ph = True
    for c in COMPS:
        R = [r for r in rows if r["composition"] == c and int(r["transitionStep"]) >= 0 and int(r["stepsToLift"]) >= 0 and int(r["holdSteps"]) > 0]
        R.sort(key=lambda r: (-int(r["success"]), -int(r["holdSteps"])))
        if not R: md.append(f"| {c} | **none** | | | | | | | | | |"); all_ph = False; continue
        r = R[0]; md.append(f"| {c} | {r['episode']} | {r['success']} | {r['endReason']} | {r['steps']} | {r['transitionStep']} | {r['stepsToLift']} | {r['holdSteps']} / {r['holdNeeded']} ({r['holdDecisions']}) | {r['contacts']} | {r['thumb']} | {r['palm']} |")
    md.append(f"\nGate, lift and hold fire on every composition: **{'YES' if all_ph else 'NO'}**")
    # outcome rates per composition (episode logs)
    md += ["", "## Rates by composition over the smoke (episode logs)", "", "| composition | episodes | reach failure (MaxStep) | gate | lift | hold entered | success |", "|---|---|---|---|---|---|---|"]
    for c in COMPS:
        R = [r for r in rows if r["composition"] == c]
        if not R: continue
        f = lambda cond: sum(1 for r in R if cond(r)) / len(R)
        md.append(f"| {c} | {len(R)} | {f(lambda r: r['endReason'] == 'maxStep'):.3f} | {f(lambda r: int(r['transitionStep']) >= 0):.3f} | {f(lambda r: int(r['stepsToLift']) >= 0):.3f} | {f(lambda r: int(r['holdSteps']) > 0):.3f} | {f(lambda r: r['success'] == '1'):.3f} |")
    s = "\n".join(md) + "\n"; open(os.path.join(REPO, a.out), "w", encoding="utf-8").write(s); sys.stdout.buffer.write(s.encode("utf-8"))
