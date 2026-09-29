"""Run 013E evaluation tables and the BO-substrate gate (brief 1g / 1h). Read-only over the pass CSVs; writes markdown to --out.

Held-out: shapes 65-80 x seeds 10001-10300 x passIndex 0-1 per presentation (600 episodes each), 013E paired per row (passIndex, seed,
shape) with 013D's Part 3 passes (results/013/eval/013D/heldout_<pose>_10001_10300_p<k>.csv). Anchor: canonical cylinder, seeds 7001-7300.
Masked stratum: probe hands 5 / 11 / 14 (thumb base masked) and 1 / 2 / 3 (easy), canonical, held-out shapes x 5 episodes (seeds 9301-9380),
013E paired with 013D per (hand, seed, shape).
Gate (all must hold): anchor >= 0.95; upright (canonical) 013E - 013D >= -0.03 with 95 % CI lower bound >= -0.06; lying (randomStable) the same.
Stop rule: both upright and lying differences below -0.06.
usage: python -m tools.eval.gate_013E --out results/013/013E/GATE.md
"""
import argparse, math, os, statistics as st, sys
from collections import Counter
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); sys.path.insert(0, REPO)
from tools.eval.pulse_vs_grasp import load, W
from tools.eval.grid_tables import fam, drop_phase
from tools.eval.summarize_pass import summarize

EV = "results/013/eval"


def load_pair(model, pose):
    out = {}
    for p in (0, 1):
        f = os.path.join(REPO, EV, model, f"heldout_{pose}_10001_10300_p{p}.csv")
        if os.path.exists(f):
            for k, v in load(f).items(): out[(p,) + k] = v
    return out


def paired(a, b, ks):
    d = [a[k]["success"] - b[k]["success"] for k in ks]; m = st.mean(d); se = st.pstdev(d) / math.sqrt(len(d)); return m, m - 1.96 * se, m + 1.96 * se


def fmt(t): return f"{t[0]:+.3f} [{t[1]:+.3f}, {t[2]:+.3f}]"


def main(out):
    md = ["# Run 013E: evaluation and BO-substrate gate", ""]
    res = {}
    for pose, lab in (("canonical", "upright (canonical)"), ("randomStable", "lying (random stable rest pose)")):
        E, D = load_pair("013E", pose), load_pair("013D", pose); ks = sorted(set(E) & set(D))
        res[pose] = (paired(E, D, ks) if len(ks) > 1 else (float("nan"),) * 3, E, D, ks)
    a = os.path.join(REPO, EV, "013E", "anchor_canonical_7001_7300.csv"); anc = summarize(a) if os.path.exists(a) else None
    # ---- gate
    up, ly = res["canonical"][0], res["randomStable"][0]
    g_anchor = anc is not None and anc["success"] >= 0.95
    g_up = up[0] >= -0.03 and up[1] >= -0.06; g_ly = ly[0] >= -0.03 and ly[1] >= -0.06
    md += ["## Gate (brief 1h)", "", "| criterion | 013D | 013E | 013E − 013D [95 % CI] | threshold | pass |", "|---|---|---|---|---|---|"]
    md.append(f"| anchor cylinder, canonical, seeds 7001-7300 (n = {anc['n'] if anc else '-'}) | 0.853 | {W([1] * round(anc['success'] * anc['n']) + [0] * (anc['n'] - round(anc['success'] * anc['n']))) if anc else '-'} | | ≥ 0.95 | **{'yes' if g_anchor else 'no'}** |")
    for pose, lab, g in (("canonical", "upright, held-out 65-80", g_up), ("randomStable", "lying (random stable), held-out 65-80", g_ly)):
        (m, lo, hi), E, D, ks = res[pose]
        md.append(f"| {lab} (n = {len(ks)} paired) | {W([D[k]['success'] for k in ks])} | {W([E[k]['success'] for k in ks])} | {fmt((m, lo, hi))} | ≥ −0.03, lower ≥ −0.06 | **{'yes' if g else 'no'}** |")
    ok = g_anchor and g_up and g_ly; stop = up[0] < -0.06 and ly[0] < -0.06
    md += ["", f"**Gate: {'PASS: 013E is the BO substrate' if ok else 'FAIL: 013D stays the BO substrate'}.**" + (" STOP rule met (both upright and lying below −0.06)." if stop else ""), ""]
    # ---- held-out detail
    md += ["## Held-out detail (paired, 013E − 013D)", "", "| presentation | subset | n | 013D | 013E | 013E − 013D [95 % CI] |", "|---|---|---|---|---|---|"]
    for pose in ("canonical", "randomStable"):
        _, E, D, ks = res[pose]
        if not ks: continue
        subs = [("all", ks)] + [(g, [k for k in ks if fam(k[2])["lenbin"] == g]) for g in ("length < 130 mm", "length 130-210 mm", "length 210-280 mm")]
        if pose == "randomStable": subs += [(f"pose class {c}", [k for k in ks if D[k]["poseClass"] == c]) for c in ("upright", "lying", "tilted rest")]
        for nm, s in subs:
            if len(s) > 1: md.append(f"| {pose} | {nm} | {len(s)} | {W([D[k]['success'] for k in s])} | {W([E[k]['success'] for k in s])} | {fmt(paired(E, D, s))} |")
    md += ["", "| presentation | model | reach failures | gate | lifted | ends | drops by phase (lift / before 1st pulse / during / between / after) |", "|---|---|---|---|---|---|---|"]
    for pose in ("canonical", "randomStable"):
        _, E, D, ks = res[pose]
        for nm, M in (("013D", D), ("013E", E)):
            v = [M[k] for k in ks]
            if not v: continue
            ph = Counter(drop_phase(r) for r in v if r["endReason"] == "drop")
            ends = Counter("reach" if r["reach_failure"] else r["endReason"] for r in v)
            md.append(f"| {pose} | {nm} | {sum(r['reach_failure'] for r in v)} | {st.mean(int(int(r['transitionStep']) >= 0) for r in v):.3f} | {st.mean(r['lifted'] for r in v):.3f} | {dict(ends)} | "
                      f"{ph.get('lift phase', 0)} / {ph.get('before first pulse', 0)} / {ph.get('during pulse', 0)} / {ph.get('between pulses', 0)} / {ph.get('after last pulse', 0)} |")
    # ---- anchor
    md += ["", "## Anchor cylinder (canonical, seeds 7001-7300)", ""]
    if anc: md.append(f"013E: {anc['success']:.3f} [{anc['lo']:.3f}, {anc['hi']:.3f}], lifted {anc['lifted']:.3f}, palm {anc['palm']:.3f}, ends {dict(anc['ends'])}; 013D (Part 3): 0.853; 012: 1.000.")
    # ---- masked stratum
    md += ["", "## Masked stratum (canonical, held-out shapes × 5 episodes, seeds 9301-9380, paired 013E vs 013D)", "", "| hand | thumb base | n | 013D | 013E | 013E − 013D [95 % CI] |", "|---|---|---|---|---|---|"]
    grp = {"masked": ([], [], []), "easy": ([], [], [])}
    for h in (5, 11, 14, 1, 2, 3):
        fE = os.path.join(REPO, EV, "013E_masked", f"masked_can_h{h:02d}_heldout_9301_9380.csv"); fD = os.path.join(REPO, EV, "013D_masked", f"masked_can_h{h:02d}_heldout_9301_9380.csv")
        if not (os.path.exists(fE) and os.path.exists(fD)): md.append(f"| {h} | | - | - | - | - |"); continue
        E, D = load(fE), load(fD); ks = sorted(set(E) & set(D)); g = "masked" if h in (5, 11, 14) else "easy"
        grp[g][0].extend((h,) + k for k in ks); [grp[g][1].append(E[k]["success"]) for k in ks]; [grp[g][2].append(D[k]["success"]) for k in ks]
        md.append(f"| {h} | {'masked' if g == 'masked' else 'driven'} | {len(ks)} | {W([D[k]['success'] for k in ks])} | {W([E[k]['success'] for k in ks])} | {fmt(paired(E, D, ks))} |")
    for g in ("masked", "easy"):
        _, e, d = grp[g]
        if len(e) > 1:
            diff = [x - y for x, y in zip(e, d)]; m = st.mean(diff); se = st.pstdev(diff) / math.sqrt(len(diff))
            md.append(f"| **{g} hands pooled** | | {len(e)} | {W(d)} | {W(e)} | {fmt((m, m - 1.96 * se, m + 1.96 * se))} |")
    open(os.path.join(REPO, out), "w", encoding="utf-8").write("\n".join(md) + "\n")
    print("\n".join(md[:12]))


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--out", default="results/013/013E/GATE.md"); main(ap.parse_args().out)
