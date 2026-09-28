"""Hand x shape grid tables from eval-harness pass CSVs (stratified 'all' passes: 80 shapes x 5 episodes per hand, paired across hands on seed,
passIndex and shape). Read-only; writes markdown to stdout or --out.

usage: python -m tools.eval.grid_tables --log LOG.json --prefix TAGPREFIX [--title TITLE] [--out FILE.md] [--pair-log LOG2.json --pair-prefix P2 --pair-name NAME]
  --prefix: tag prefix of the grid passes, e.g. grid_can_ (tags grid_can_hNN_9301_9400_pP)
  --pair-*: a second grid on the same seeds (another model or pose mode) for per-hand paired differences
"""
import argparse, csv, json, math, os, statistics as st, sys
from collections import defaultdict, Counter
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); sys.path.insert(0, REPO)
from tools.eval.summarize_pass import wilson

HANDS = (5, 11, 14, 1, 2, 3); HARD, EASY = (5, 11, 14), (1, 2, 3); PULSE_LEN = 5
bank = {int(r["index"]): r for r in csv.DictReader(open(os.path.join(REPO, "Assets/Objects/Bank/bank.csv")))}


def fam(idx):
    b = bank[idx]; a1, a2, a3, e1, e2 = (float(b[k]) for k in ("a1_mm", "a2_mm", "a3_mm", "e1", "e2"))
    cross = 2 * max(a1, a2); length = 2 * a3; aspect = a3 / max(a1, a2)
    return dict(e1bin="e1 0.3-0.7" if e1 < 0.7 else ("e1 0.7-1.1" if e1 < 1.1 else "e1 1.1-1.5"), e2bin="e2 0.3-0.7" if e2 < 0.7 else ("e2 0.7-1.1" if e2 < 1.1 else "e2 1.1-1.5"),
                crossbin="cross < 50 mm" if cross < 50 else ("cross 50-65 mm" if cross < 65 else "cross 65-80 mm"), lenbin="length < 130 mm" if length < 130 else ("length 130-210 mm" if length < 210 else "length 210-280 mm"),
                aspectbin="aspect < 1.5" if aspect < 1.5 else ("aspect 1.5-3" if aspect < 3 else "aspect >= 3"), vol=float(b["volume_cm3"]))


FAMS = (("lenbin", "length 2·a3"), ("crossbin", "cross-section 2·max(a1, a2)"), ("e1bin", "e1"), ("e2bin", "e2"), ("aspectbin", "aspect a3 / max(a1, a2)"))


def drop_phase(r):
    if r["endReason"] != "drop": return None
    hs = int(r["holdSteps"])
    if hs <= 0: return "lift phase"
    starts = sorted(int(r[k]) for k in ("pulseStep1", "pulseStep2", "pulseStep3") if int(r[k]) >= 0)
    for i, s in enumerate(starts):
        if s <= hs < s + PULSE_LEN: return "during pulse"
    if not starts or hs < starts[0]: return "before first pulse"
    if hs >= starts[-1] + PULSE_LEN: return "after last pulse"
    return "between pulses"


def load_grid(log_path, prefix):
    L = json.load(open(os.path.join(REPO, log_path))); grid = {}
    for h in HANDS:
        rows = []
        for e in L:
            if not (e.get("ok") and e["tag"].startswith(f"{prefix}h{h:02d}_")): continue
            p = int(e["tag"].rsplit("_p", 1)[1])
            for r in csv.DictReader(open(os.path.join(REPO, e["csv"]), newline="")):
                if not (r.get("seed") or "").isdigit(): continue
                zero = r.get("maxStepTimeout") == "1" or (r["endReason"] == "maxStep" and r["steps"] == "0")
                if zero and r["episode"] == "1": continue
                d = dict(r); d["reach_failure"] = int(zero); d["success"] = int(r["success"]); d["lifted"] = int(int(r["stepsToLift"]) >= 0); d["palm"] = int(r["palm"]); d["thumb"] = int(r["thumb"])
                d["contacts"] = int(r["contacts"]); d["idx"] = int(r["objIndex"]); d["seed"] = int(r["seed"]); d["_pass"] = p; d["mass"] = float(r["mass"]); rows.append(d)
        if rows: grid[h] = rows
    return grid


def fmt(rows, key="success"):
    n = len(rows); k = sum(r[key] for r in rows); p, lo, hi = wilson(k, n); return f"{p:.3f} [{lo:.3f}, {hi:.3f}] (n={n})" if n else "-"


def mean_ci(d):
    m = st.mean(d); se = st.pstdev(d) / math.sqrt(len(d)) if len(d) > 1 else 0.0; return m, m - 1.96 * se, m + 1.96 * se


def tables(grid, title, thetas):
    md = [f"## {title}", ""]
    md.append("| hand | thumb base | episodes | success [95 % Wilson] | lifted | palm at end | thumb at end | contacts | reach fail | drops (lift phase / before first pulse / during / between / after) | gate→lift median steps |")
    md.append("|---|---|---|---|---|---|---|---|---|---|---|")
    for h, rows in grid.items():
        ph = Counter(drop_phase(r) for r in rows if r["endReason"] == "drop")
        lat = [int(r["stepsToLift"]) - int(r["transitionStep"]) for r in rows if int(r["stepsToLift"]) >= 0 and int(r["transitionStep"]) >= 0]
        md.append(f"| {h} | {'masked' if not thetas[h]['mask'][12] else 'active'} | {len(rows)} | {fmt(rows)} | {sum(r['lifted'] for r in rows) / len(rows):.3f} | {sum(r['palm'] for r in rows) / len(rows):.3f} | {sum(r['thumb'] for r in rows) / len(rows):.3f} | {st.mean(r['contacts'] for r in rows):.2f} | {sum(r['reach_failure'] for r in rows) / len(rows):.3f} | {ph.get('lift phase', 0)} / {ph.get('before first pulse', 0)} / {ph.get('during pulse', 0)} / {ph.get('between pulses', 0)} / {ph.get('after last pulse', 0)} | {st.median(lat) if lat else '-'} |")
    md.append("")
    per = {h: {(r["_pass"], r["seed"], r["idx"]): r for r in rows} for h, rows in grid.items()}
    keys = sorted(set.intersection(*(set(v) for v in per.values())))
    hard = [h for h in HARD if h in grid]; easy = [h for h in EASY if h in grid]
    d = [st.mean(per[h][k]["success"] for h in hard) - st.mean(per[h][k]["success"] for h in easy) for k in keys]
    m, lo, hi = mean_ci(d)
    md.append(f"Paired over {len(keys)} episodes common to all six hands (same seed, passIndex and shape): thumb-masked mean {st.mean(st.mean(per[h][k]['success'] for h in hard) for k in keys):.3f}, easy mean {st.mean(st.mean(per[h][k]['success'] for h in easy) for k in keys):.3f}, **thumb-masked − easy {m:+.3f} [{lo:+.3f}, {hi:+.3f}]**.")
    md.append("")
    for key, label in FAMS:
        fams = sorted(set(fam(i)[key] for i in range(1, 81)))
        md.append(f"Per hand × {label} (success; n per cell) and the paired thumb-masked − easy difference within the family:"); md.append("")
        md.append("| family | " + " | ".join(f"hand {h}" for h in grid) + " | thumb-masked − easy [95 % CI] |"); md.append("|---|" + "---|" * (len(grid) + 1))
        for g in fams:
            cells = []
            for h, rows in grid.items():
                v = [r for r in rows if fam(r["idx"])[key] == g]; cells.append(f"{sum(r['success'] for r in v) / len(v):.2f} ({len(v)})" if v else "-")
            dk = [st.mean(per[h][k]["success"] for h in hard) - st.mean(per[h][k]["success"] for h in easy) for k in keys if fam(k[2])[key] == g]
            mm, l2, h2 = mean_ci(dk) if dk else (float("nan"),) * 3
            md.append(f"| {g} | " + " | ".join(cells) + f" | {mm:+.3f} [{l2:+.3f}, {h2:+.3f}] (n={len(dk)}) |")
        md.append("")
    return md, per, keys


def paired_models(gA, gB, nameA, nameB):
    md = [f"### Paired per hand: {nameA} − {nameB} (same seed, passIndex, shape and theta)", "", "| hand | pairs | " + nameA + " | " + nameB + " | difference [95 % CI] |", "|---|---|---|---|---|"]
    for h in HANDS:
        if h not in gA or h not in gB: continue
        a = {(r["_pass"], r["seed"], r["idx"]): r["success"] for r in gA[h]}; b = {(r["_pass"], r["seed"], r["idx"]): r["success"] for r in gB[h]}
        ks = sorted(set(a) & set(b)); d = [a[k] - b[k] for k in ks]; m, lo, hi = mean_ci(d)
        md.append(f"| {h} | {len(ks)} | {st.mean(a[k] for k in ks):.3f} | {st.mean(b[k] for k in ks):.3f} | {m:+.3f} [{lo:+.3f}, {hi:+.3f}] |")
    md.append(""); return md


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--log", required=True); ap.add_argument("--prefix", required=True); ap.add_argument("--title", default="Hand x shape grid"); ap.add_argument("--out")
    ap.add_argument("--pair-log"); ap.add_argument("--pair-prefix"); ap.add_argument("--pair-name", default="other"); ap.add_argument("--name", default="this")
    a = ap.parse_args(); thetas = json.load(open(os.path.join(REPO, "results/bo/partB/probe_thetas.json")))
    g = load_grid(a.log, a.prefix); md, _, _ = tables(g, a.title, thetas)
    if a.pair_log: md += paired_models(g, load_grid(a.pair_log, a.pair_prefix), a.name, a.pair_name)
    text = "\n".join(md) + "\n"
    if a.out: open(os.path.join(REPO, a.out), "w", encoding="utf-8").write(text)
    else: sys.stdout.buffer.write(text.encode("utf-8"))
