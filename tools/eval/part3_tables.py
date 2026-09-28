"""Run 013 Part 3 tables (read-only): three models (012, 013C, 013D) on held-out shapes 65-80 x seeds 10001-10300 x passIndex 0-1 in both
presentations, paired per episode (seed, passIndex, shape; theta, mass, spawn and yaw draws shared); 013D on train shapes; anchor regression;
range probes. Writes markdown to --out.

usage: python -m tools.eval.part3_tables --out results/013/eval/PART3_TABLES.md
"""
import argparse, csv, json, math, os, statistics as st, sys
from collections import Counter, defaultdict
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); sys.path.insert(0, REPO)
from tools.eval.summarize_pass import wilson, summarize
from tools.eval.grid_tables import fam, drop_phase, FAMS

E = os.path.join(REPO, "results", "013", "eval"); MODELS = ("012", "013C", "013D")


def load_log(d): p = os.path.join(E, d, "passes.json"); return json.load(open(p)) if os.path.exists(p) else []


def rows_for(model, prefix):
    out = {}
    for e in load_log(model):
        if not (e.get("ok") and e["tag"].startswith(prefix)): continue
        p = int(e["tag"].rsplit("_p", 1)[1]) if "_p" in e["tag"] else 0
        for r in csv.DictReader(open(os.path.join(REPO, e["csv"]), newline="")):
            if not (r.get("seed") or "").isdigit(): continue
            zero = r.get("maxStepTimeout") == "1" or (r["endReason"] == "maxStep" and r["steps"] == "0")
            if zero and r["episode"] == "1": continue
            d = dict(r); d["reach_failure"] = int(zero); d["success"] = int(r["success"]); d["lifted"] = int(int(r["stepsToLift"]) >= 0); d["palm"] = int(r["palm"]); d["thumb"] = int(r["thumb"])
            d["contacts"] = int(r["contacts"]); d["idx"] = int(r["objIndex"]); d["gate"] = int(int(r["transitionStep"]) >= 0)
            out[(p, int(r["seed"]), d["idx"])] = d
    return out


def W(v):
    if not v: return "-"
    p, lo, hi = wilson(sum(v), len(v)); return f"{p:.3f} [{lo:.3f}, {hi:.3f}]"


def D(a, b, keys):
    ks = [k for k in keys if k in a and k in b]
    if len(ks) < 2: return "-"
    d = [a[k]["success"] - b[k]["success"] for k in ks]; m = st.mean(d); se = st.pstdev(d) / math.sqrt(len(d)); return f"{m:+.3f} [{m - 1.96 * se:+.3f}, {m + 1.96 * se:+.3f}]"


def pose_class_of(r):
    t = float(r["spawnTiltDeg"]); return "upright" if (t < 10 or t > 170) else ("lying" if 80 <= t <= 100 else "tilted rest")


def three_model_section(md, pose):
    R = {m: rows_for(m, f"heldout_{pose}_10001_10300_") for m in MODELS}
    keys = sorted(set.intersection(*(set(v) for v in R.values()))) if all(R.values()) else []
    md.append(f"### Held-out shapes 65-80, {pose}: {len(keys)} episodes paired across the three models"); md.append("")
    if not keys: md.append("(missing passes)"); md.append(""); return R, keys
    md.append("| subset | n | 012 | 013C | 013D | 013D − 012 | 013D − 013C | 013C − 012 |"); md.append("|---|---|---|---|---|---|---|---|")
    subsets = [("all", keys)]
    for key, label in FAMS:
        for g in sorted(set(fam(k[2])[key] for k in keys)): subsets.append((g, [k for k in keys if fam(k[2])[key] == g]))
    vols = sorted(set(fam(i)["vol"] for i in range(65, 81))); c1, c2 = vols[len(vols) // 3], vols[2 * len(vols) // 3]
    for nm, sel in (("volume low", lambda v: v < c1), ("volume mid", lambda v: c1 <= v < c2), ("volume high", lambda v: v >= c2)): subsets.append((f"{nm} (cuts {c1:.0f}, {c2:.0f} cm³)", [k for k in keys if sel(fam(k[2])["vol"])]))
    if pose == "randomStable":
        for pc in ("upright", "lying", "tilted rest"): subsets.append((f"pose class {pc}", [k for k in keys if pose_class_of(R["013D"][k]) == pc]))
    for nm, ks in subsets:
        if not ks: continue
        md.append(f"| {nm} | {len(ks)} | " + " | ".join(W([R[m][k]["success"] for k in ks]) for m in MODELS) + f" | {D(R['013D'], R['012'], ks)} | {D(R['013D'], R['013C'], ks)} | {D(R['013C'], R['012'], ks)} |")
    md.append("")
    md.append("| model | reach-failure rate | gate | lifted | palm at end | thumb at end | contacts | drops (lift phase / before first pulse / during / between / after) |"); md.append("|---|---|---|---|---|---|---|---|")
    for m in MODELS:
        v = [R[m][k] for k in keys]; ph = Counter(drop_phase(r) for r in v if r["endReason"] == "drop")
        md.append(f"| {m} | {st.mean(r['reach_failure'] for r in v):.3f} | {st.mean(r['gate'] for r in v):.3f} | {st.mean(r['lifted'] for r in v):.3f} | {st.mean(r['palm'] for r in v):.3f} | {st.mean(r['thumb'] for r in v):.3f} | {st.mean(r['contacts'] for r in v):.2f} | {ph.get('lift phase', 0)} / {ph.get('before first pulse', 0)} / {ph.get('during pulse', 0)} / {ph.get('between pulses', 0)} / {ph.get('after last pulse', 0)} |")
    md.append("")
    md.append("Contacts and palm flag by length family:"); md.append(""); md.append("| length | " + " | ".join(f"{m} contacts / palm" for m in MODELS) + " |"); md.append("|---|---|---|---|")
    for g in sorted(set(fam(k[2])["lenbin"] for k in keys)):
        ks = [k for k in keys if fam(k[2])["lenbin"] == g]
        md.append(f"| {g} | " + " | ".join(f"{st.mean(R[m][k]['contacts'] for k in ks):.2f} / {st.mean(R[m][k]['palm'] for k in ks):.2f}" for m in MODELS) + " |")
    md.append(""); return R, keys


def train_section(md):
    md.append("### 013D on train shapes 1-64, seeds 10001-10300 × passIndex 0-2"); md.append("")
    md.append("| subset | canonical | random-stable |"); md.append("|---|---|---|")
    C, S = rows_for("013D", "train_canonical_10001_10300_"), rows_for("013D", "train_randomStable_10001_10300_")
    if not C: md.append("(missing)"); md.append(""); return
    md.append(f"| all (n={len(C)} / {len(S)}) | {W([r['success'] for r in C.values()])} | {W([r['success'] for r in S.values()])} |")
    for g in ("length < 130 mm", "length 130-210 mm", "length 210-280 mm"):
        md.append(f"| {g} | {W([r['success'] for k, r in C.items() if fam(k[2])['lenbin'] == g])} | {W([r['success'] for k, r in S.items() if fam(k[2])['lenbin'] == g])} |")
    ps = defaultdict(list)
    for k, r in C.items(): ps[k[2]].append(r["success"])
    v = [sum(x) / len(x) for x in ps.values()]; md.append(""); md.append(f"Per-shape canonical success over 64 shapes: min {min(v):.2f}, median {st.median(v):.2f}, max {max(v):.2f}; shapes ≤ 0.70: {sum(1 for x in v if x <= 0.70)}.")
    ps = defaultdict(list)
    for k, r in S.items(): ps[k[2]].append(r["success"])
    v = [sum(x) / len(x) for x in ps.values()] or [float("nan")]; md.append(f"Per-shape random-stable success over 64 shapes: min {min(v):.2f}, median {st.median(v):.2f}, max {max(v):.2f}; shapes ≤ 0.70: {sum(1 for x in v if x <= 0.70)}."); md.append("")


def anchor_section(md):
    md.append("### Anchor regression (canonical cylinder, seeds 7001-7300)"); md.append(""); md.append("| model | success [95 % Wilson] | lifted | palm | ends |"); md.append("|---|---|---|---|---|")
    md.append("| 012 (reference, eval_n300_v2 / 2026-09-27) | 1.000 [0.987, 1.000] | 1.000 | 0.967 | 300 success |")
    for m in ("013C", "013D"):
        e = [x for x in load_log(m) if x.get("ok") and x["tag"].startswith("anchor_canonical")]
        if e: s = summarize(os.path.join(REPO, e[-1]["csv"])); md.append(f"| {m} | {s['success']:.3f} [{s['lo']:.3f}, {s['hi']:.3f}] | {s['lifted']:.3f} | {s['palm']:.3f} | {s['ends']} |")
    md.append("")


def probe_section(md):
    md.append("### BO range probes (20 probe theta, held-out shapes 65-80, seeds 10001-10100, n = 100 each, perturbation 1.0)"); md.append("")
    md.append("Rule: the objective has range if the mean over the 20 theta is in [0.45, 0.80] and the SD across theta is ≥ 0.10."); md.append("")
    md.append("| model / presentation | n theta | mean | min | max | SD | worst 4 theta | range? |"); md.append("|---|---|---|---|---|---|---|---|")
    for d, lab in (("probe_013D_rs", "013D random-stable"), ("probe_013D_can", "013D canonical"), ("probe_012_can", "012 canonical")):
        e = [x for x in load_log(d) if x.get("ok") and x["tag"].startswith("probe_")]
        if not e: md.append(f"| {lab} | 0 | - | - | - | - | - | - |"); continue
        v = {int(x["tag"][7:9]): x["success"] for x in e}; vals = list(v.values()); sd = st.stdev(vals) if len(vals) > 1 else float("nan")
        worst = sorted(v, key=v.get)[:4]; ok = 0.45 <= st.mean(vals) <= 0.80 and sd >= 0.10
        md.append(f"| {lab} | {len(vals)} | {st.mean(vals):.3f} | {min(vals):.2f} | {max(vals):.2f} | {sd:.3f} | {', '.join(f'{i} ({v[i]:.2f})' for i in worst)} | **{'yes' if ok else 'no'}** |")
    md.append("")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--out", default="results/013/eval/PART3_TABLES.md"); a = ap.parse_args()
    md = ["# Run 013 Part 3 tables", ""]
    for pose in ("canonical", "randomStable"): three_model_section(md, pose)
    train_section(md); anchor_section(md); probe_section(md)
    open(os.path.join(REPO, a.out), "w", encoding="utf-8").write("\n".join(md) + "\n"); print("wrote", a.out, len(md), "lines")
