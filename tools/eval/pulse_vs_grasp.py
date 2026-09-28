"""Run 013 pulse-vs-grasp analysis (read-only). For 013C and 013D: random-stable, held-out shapes 65-80, seeds 10001-10300, passIndex 0, perturbation 0
(results/013/eval/pulse/) against the Part 3 perturbation-1.0 pass on the same rows (results/013/eval/<model>/heldout_randomStable_10001_10300_p0.csv).
Rows pair on (seed, shape); theta, mass, spawn, yaw, pose and the pulse schedule are drawn identically (at perturbation 0 the pulses are scheduled at the
same hold steps with zero force). Writes markdown to stdout.

Verdict rule (brief): PULSE-LIMITED if success(perturb 0) - success(perturb 1.0) >= 0.15 and the paired 95 % CI excludes 0; GRASP-LIMITED if the difference is
< 0.05 and the CI includes 0; otherwise MIXED.
"""
import csv, math, os, statistics as st, sys
from collections import Counter
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); sys.path.insert(0, REPO)
from tools.eval.summarize_pass import wilson
from tools.eval.grid_tables import fam, drop_phase


def load(p):
    out = {}
    for r in csv.DictReader(open(os.path.join(REPO, p), newline="")):
        if not (r.get("seed") or "").isdigit(): continue
        zero = r.get("maxStepTimeout") == "1" or (r["endReason"] == "maxStep" and r["steps"] == "0")
        if zero and r["episode"] == "1": continue
        d = dict(r); d["reach_failure"] = int(zero); d["success"] = int(r["success"]); d["lifted"] = int(int(r["stepsToLift"]) >= 0); d["palm"] = int(r["palm"])
        d["contacts"] = int(r["contacts"]); d["thumb"] = int(r["thumb"]); d["idx"] = int(r["objIndex"]); d["holdSteps"] = int(r["holdSteps"])
        t = float(r["spawnTiltDeg"]); d["poseClass"] = "upright" if (t < 10 or t > 170) else ("lying" if 80 <= t <= 100 else "tilted rest")
        out[(int(r["seed"]), d["idx"])] = d
    return out


def W(v):
    if not v: return "-"
    p, lo, hi = wilson(sum(v), len(v)); return f"{p:.3f} [{lo:.3f}, {hi:.3f}] (n={len(v)})"


def paired(a, b, ks):
    d = [a[k]["success"] - b[k]["success"] for k in ks]; m = st.mean(d); se = st.pstdev(d) / math.sqrt(len(d)); return m, m - 1.96 * se, m + 1.96 * se


def verdict(m, lo, hi):
    if m >= 0.15 and lo > 0: return "PULSE-LIMITED"
    if m < 0.05 and lo <= 0 <= hi: return "GRASP-LIMITED"
    return "MIXED"


def section(model):
    P0 = load(f"results/013/eval/pulse/{model}_heldout_randomStable_10001_10300_p0_perturb0.csv")
    P1 = load(f"results/013/eval/{model}/heldout_randomStable_10001_10300_p0.csv")
    ks = sorted(set(P0) & set(P1)); md = [f"### {model}: {len(ks)} rows paired (perturbation 0 vs 1.0)", ""]
    m, lo, hi = paired(P0, P1, ks); v = verdict(m, lo, hi)
    md.append("| quantity | perturbation 0 | perturbation 1.0 |"); md.append("|---|---|---|")
    for lab, key in (("success [95 % Wilson]", "success"), ("lifted", "lifted")):
        md.append(f"| {lab} | {W([P0[k][key] for k in ks])} | {W([P1[k][key] for k in ks])} |")
    for lab, M in (("perturbation 0", P0), ("perturbation 1.0", P1)): pass
    ltd = lambda M: [M[k] for k in ks if M[k]["lifted"]]
    md.append(f"| lifted then dropped (share of lifted episodes) | {sum(r['endReason'] == 'drop' for r in ltd(P0)) / max(1, len(ltd(P0))):.3f} ({sum(r['endReason'] == 'drop' for r in ltd(P0))}/{len(ltd(P0))}) | {sum(r['endReason'] == 'drop' for r in ltd(P1)) / max(1, len(ltd(P1))):.3f} ({sum(r['endReason'] == 'drop' for r in ltd(P1))}/{len(ltd(P1))}) |")
    md.append(f"| lifted then dropped (share of all episodes) | {sum(r['endReason'] == 'drop' for r in ltd(P0)) / len(ks):.3f} | {sum(r['endReason'] == 'drop' for r in ltd(P1)) / len(ks):.3f} |")
    for lab, M in (("reach failures", None),): md.append(f"| reach failures | {sum(P0[k]['reach_failure'] for k in ks)} | {sum(P1[k]['reach_failure'] for k in ks)} |")
    md.append(f"| ends | {dict(Counter(('reach' if P0[k]['reach_failure'] else P0[k]['endReason']) for k in ks))} | {dict(Counter(('reach' if P1[k]['reach_failure'] else P1[k]['endReason']) for k in ks))} |")
    for M, lab in ((P0, "perturbation 0"), (P1, "perturbation 1.0")): pass
    dp = lambda M: Counter(drop_phase(M[k]) for k in ks if M[k]["endReason"] == "drop")
    for ph in ("lift phase", "before first pulse", "during pulse", "between pulses", "after last pulse"):
        md.append(f"| drops: {ph} | {dp(P0).get(ph, 0)} | {dp(P1).get(ph, 0)} |")
    med = lambda M: st.median([M[k]["holdSteps"] for k in ks if M[k]["endReason"] == "drop"] or [float("nan")])
    md.append(f"| median hold step at drop (of 500) | {med(P0):.0f} | {med(P1):.0f} |")
    for q, key in (("contacts", "contacts"), ("palm flag", "palm"), ("thumb flag", "thumb")):
        at = lambda M, cond: [M[k][key] for k in ks if cond(M[k])]
        f = lambda v: f"{st.mean(v):.2f}" if v else "-"
        md.append(f"| {q} at drop / at success | {f(at(P0, lambda r: r['endReason'] == 'drop'))} / {f(at(P0, lambda r: r['success']))} | {f(at(P1, lambda r: r['endReason'] == 'drop'))} / {f(at(P1, lambda r: r['success']))} |")
    md.append(f"| **paired success difference, perturbation 0 − 1.0 [95 % CI]** | **{m:+.3f} [{lo:+.3f}, {hi:+.3f}]** | |")
    md.append(""); md.append(f"Verdict by the rule: **{v}**."); md.append("")
    md.append("| subset | n | perturbation 0 | perturbation 1.0 | paired difference [95 % CI] |"); md.append("|---|---|---|---|---|")
    subs = [(g, [k for k in ks if fam(k[1])["lenbin"] == g]) for g in ("length < 130 mm", "length 130-210 mm", "length 210-280 mm")]
    subs += [(f"pose class {c}", [k for k in ks if P1[k]["poseClass"] == c]) for c in ("lying", "upright", "tilted rest")]
    for nm, s in subs:
        if len(s) < 2: continue
        mm, l2, h2 = paired(P0, P1, s)
        md.append(f"| {nm} | {len(s)} | {W([P0[k]['success'] for k in s])} | {W([P1[k]['success'] for k in s])} | {mm:+.3f} [{l2:+.3f}, {h2:+.3f}] |")
    md.append("")
    return md, (model, m, lo, hi, v, st.mean(P0[k]["success"] for k in ks), st.mean(P1[k]["success"] for k in ks))


if __name__ == "__main__":
    md = []; vt = []
    for model in ("013C", "013D"):
        s, row = section(model); md += s; vt.append(row)
    head = ["| model | success, perturbation 0 | success, perturbation 1.0 | paired difference [95 % CI] | verdict |", "|---|---|---|---|---|"]
    head += [f"| {m} | {a:.3f} | {b:.3f} | {d:+.3f} [{lo:+.3f}, {hi:+.3f}] | **{v}** |" for m, d, lo, hi, v, a, b in vt]
    sys.stdout.buffer.write(("\n".join(["## Verdict table", ""] + head + [""] + md) + "\n").encode("utf-8"))
