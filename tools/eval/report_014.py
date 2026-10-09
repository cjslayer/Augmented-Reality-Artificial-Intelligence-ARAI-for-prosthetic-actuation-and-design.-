"""Run 014 Part 4b tables from the eval passes in results/014/eval (tools/eval/eval_014.py). Prints markdown sections for REPORT.md.

Paired statistics: rows pair by seed (same object, pose, rest-pose index, yaw and placement; checked per pair). Difference d = mean(x - y) with a
normal CI on the per-row differences (paired), the discordant counts b (014 only) / c (other only), and an exact two-sided McNemar p.
Single proportions: Wilson 95 %.
"""
import csv, math, os, sys
from collections import Counter
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
D = os.path.join(REPO, "results/014/eval")
COMPS = ["3", "4", "5", "6a", "6b", "7"]; POSES = [("canonical", "upright"), ("randomStable", "lying")]
ENDS = ["success", "drop", "liftBudget", "taskBudget", "maxStep"]


def rows(name):
    p = os.path.join(D, name + ".csv")
    if not os.path.exists(p): return None
    return {r["seed"]: r for r in csv.DictReader(open(p, newline="")) if (r.get("seed") or "").isdigit()}


def wilson(k, n, z=1.96):
    if n == 0: return float("nan"), float("nan")
    p = k / n; d = 1 + z * z / n; c = (p + z * z / (2 * n)) / d; h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d
    return c - h, c + h


def ok(r): return r["success"] in ("1", "True", "true")
def gate(r): return int(r["transitionStep"]) >= 0
def lift(r): return int(r["stepsToLift"]) >= 0
def hold(r): return int(r["holdSteps"]) > 0


def cell(R):
    n = len(R); k = sum(ok(r) for r in R.values()); lo, hi = wilson(k, n); e = Counter(r["endReason"] for r in R.values())
    return n, k / n, lo, hi, sum(gate(r) for r in R.values()) / n, sum(lift(r) for r in R.values()) / n, sum(hold(r) for r in R.values()) / n, {x: e.get(x, 0) / n for x in ENDS}, e


def mcnemar(b, c):
    n = b + c
    if n == 0: return 1.0
    k = min(b, c); p = sum(math.comb(n, i) for i in range(0, k + 1)) / 2 ** n
    return min(1.0, 2 * p)


def paired(A, B, f=ok, keys=None):
    keys = sorted(set(A) & set(B)) if keys is None else keys
    d = [int(f(A[s])) - int(f(B[s])) for s in keys]; n = len(d); m = sum(d) / n
    sd = math.sqrt(sum((x - m) ** 2 for x in d) / (n - 1)) if n > 1 else 0.0; h = 1.96 * sd / math.sqrt(n)
    b = sum(1 for x in d if x == 1); c = sum(1 for x in d if x == -1)
    return n, m, m - h, m + h, b, c, mcnemar(b, c)


def same_rows(A, B):
    bad = [s for s in A if s in B and any(A[s].get(k) != B[s].get(k) for k in ("objIndex", "poseMode", "poseIndex", "spawnYaw"))]
    return len(set(A) & set(B)), bad


def fmt_cell(c):
    n, p, lo, hi, g, l, h, ends, _ = c
    return f"{n} | **{p:.3f}** [{lo:.3f}, {hi:.3f}] | {g:.3f} | {l:.3f} | {h:.3f} | " + " | ".join(f"{ends[x]:.3f}" for x in ENDS)


def main():
    md = []
    # headline by composition x pose
    for pose, lab in POSES:
        md += [f"\n### {lab.capitalize()} (held-out shapes 65–80, seeds 12401–12700, lift budget 300, perturbation 1.0)", "",
               "| composition | n | success, deterministic [95 % Wilson] | gate | lift | hold entered | end success | end drop | end liftBudget | end taskBudget | end maxStep | success, sampled [95 %] |",
               "|---|---|---|---|---|---|---|---|---|---|---|---|"]
        for c in COMPS:
            R = rows(f"b1_014_h{c}_{pose}_det_lb300"); S = rows(f"b1_014_h{c}_{pose}_sam_lb300")
            if R is None: md.append(f"| {c} | missing |"); continue
            s = cell(S) if S else None
            md.append(f"| {c} | " + fmt_cell(cell(R)) + (f" | {s[1]:.3f} [{s[2]:.3f}, {s[3]:.3f}] |" if s else " | - |"))
    # composition contrasts vs 5 (paired by row)
    md += ["\n### Composition minus 5-digit, paired by row (deterministic, lift budget 300)", "", "| pose | composition | n | d success [95 %] | b / c | McNemar p |", "|---|---|---|---|---|---|"]
    for pose, lab in POSES:
        R5 = rows(f"b1_014_h5_{pose}_det_lb300")
        for c in COMPS:
            if c == "5": continue
            R = rows(f"b1_014_h{c}_{pose}_det_lb300")
            if R and R5: n, m, lo, hi, b, cc, p = paired(R, R5); md.append(f"| {lab} | {c} | {n} | {m:+.3f} [{lo:+.3f}, {hi:+.3f}] | {b} / {cc} | {p:.3g} |")
    # 014 - 013E paired
    md += ["\n### 014 − 013E on the 5-digit hand, paired by row (deterministic; 013E with the envelope filter forced, all rows)", "",
           "| cell | lift budget | n | 014 success [95 %] | 013E success [95 %] | d (014 − 013E) [95 % paired] | b / c | McNemar p | rows identical (object, pose, yaw) |", "|---|---|---|---|---|---|---|---|---|"]
    pairs = [("upright", lb, f"b1_014_h5_canonical_det_lb{lb}", f"b2_013E_h5_canonical_det_lb{lb}") for lb in (300, 200)] + \
            [("lying", lb, f"b1_014_h5_randomStable_det_lb{lb}", f"b2_013E_h5_randomStable_det_lb{lb}") for lb in (300, 200)] + \
            [("anchor 7001–7300", lb, f"b3_anchor_014_h5_det_lb{lb}", f"b3_anchor_013E_h5_det_lb{lb}") for lb in (300, 200)]
    for lab, lb, a, b_ in pairs:
        A, B = rows(a), rows(b_)
        if not A or not B: md.append(f"| {lab} | {lb} | missing |"); continue
        n, m, lo, hi, b, c, p = paired(A, B); ca, cb = cell(A), cell(B); nn, bad = same_rows(A, B)
        md.append(f"| {lab} | {lb} | {n} | {ca[1]:.3f} [{ca[2]:.3f}, {ca[3]:.3f}] | {cb[1]:.3f} [{cb[2]:.3f}, {cb[3]:.3f}] | **{m:+.3f}** [{lo:+.3f}, {hi:+.3f}] | {b} / {c} | {p:.3g} | {nn - len(bad)} / {nn} |")
    # sensitivity: matched rows, 013E on its own filter
    md += ["\n### Sensitivity: matched rows only, 013E on its own-hand filter (lift budget 300)", "", "| cell | matched rows | d (014 − 013E own filter) [95 % paired] | b / c | McNemar p |", "|---|---|---|---|---|"]
    for lab, pr, a, b_ in [("upright", "pair_canonical_013E", "b1_014_h5_canonical_det_lb300", "b2_013E_h5_canonical_det_lb300_ownfilter"),
                           ("lying", "pair_randomStable_013E", "b1_014_h5_randomStable_det_lb300", "b2_013E_h5_randomStable_det_lb300_ownfilter"),
                           ("anchor", "pair_anchor_013E", "b3_anchor_014_h5_det_lb300", "b3_anchor_013E_h5_det_lb300_ownfilter")]:
        P = [l.strip().split(",") for l in open(os.path.join(D, pr + ".csv")) if l[:1].isdigit()]
        keys = sorted(r[0] for r in P if r[8] == r[9]); A, B = rows(a), rows(b_)
        if not A or not B: md.append(f"| {lab} | {len(keys)} | missing |"); continue
        n, m, lo, hi, b, c, p = paired(A, B, keys=keys); md.append(f"| {lab} | {n} | {m:+.3f} [{lo:+.3f}, {hi:+.3f}] | {b} / {c} | {p:.3g} |")
    # pulse test
    md += ["\n### Pulse test: lying, perturbation 0 vs 1.0 on identical rows (deterministic, lift budget 300)", "",
           "| composition | n | success p0 | success p1 | d success (p1 − p0) [95 %] | drop p0 | drop p1 | d drop (p1 − p0) [95 %] | lift p0 | lift p1 |", "|---|---|---|---|---|---|---|---|---|---|"]
    drop = lambda r: r["endReason"] == "drop"
    for c in COMPS:
        P0, P1 = rows(f"b4_014_h{c}_randomStable_det_lb300_perturb0"), rows(f"b1_014_h{c}_randomStable_det_lb300")
        if not P0 or not P1: md.append(f"| {c} | missing |"); continue
        n, m, lo, hi, *_ = paired(P1, P0); _, md_, dlo, dhi, *_ = paired(P1, P0, f=drop); c0, c1 = cell(P0), cell(P1)
        md.append(f"| {c} | {n} | {c0[1]:.3f} | {c1[1]:.3f} | {m:+.3f} [{lo:+.3f}, {hi:+.3f}] | {c0[7]['drop']:.3f} | {c1[7]['drop']:.3f} | {md_:+.3f} [{dlo:+.3f}, {dhi:+.3f}] | {c0[5]:.3f} | {c1[5]:.3f} |")
    sys.stdout.buffer.write(("\n".join(md) + "\n").encode("utf-8"))


def main_cd():
    """C/D evaluation (brief 2026-10-08): 014CD tables, 014CD - 014 r2, 014CD - 013E, anchor, placement check, lying curve from training logs."""
    md = []; mism = []
    def chk(A, B, lab):
        nn, bad = same_rows(A, B); mism.append((lab, nn, len(bad))); return nn - len(bad), nn
    for pose, lab in POSES:
        md += [f"\n### 014CD, {lab} (held-out shapes 65–80, seeds 12701–13000, lift budget 300, perturbation 1.0)", "",
               "| composition | n | success, deterministic [95 % Wilson] | gate | lift | hold entered | end success | end drop | end liftBudget | end taskBudget | end maxStep | success, sampled [95 %] |",
               "|---|---|---|---|---|---|---|---|---|---|---|---|"]
        R5 = rows(f"cd1_014CD_h5_{pose}_det_lb300")
        for c in COMPS:
            R = rows(f"cd1_014CD_h{c}_{pose}_det_lb300"); S = rows(f"cd1_014CD_h{c}_{pose}_sam_lb300")
            if R is None: md.append(f"| {c} | missing |"); continue
            chk(R, R5, f"cd1 {pose} h{c} det vs h5")
            if S: chk(S, R5, f"cd1 {pose} h{c} sam vs h5")
            s_ = cell(S) if S else None
            md.append(f"| {c} | " + fmt_cell(cell(R)) + (f" | {s_[1]:.3f} [{s_[2]:.3f}, {s_[3]:.3f}] |" if s_ else " | - |"))
    md += ["\n### 014CD: composition minus 5-digit, paired by row (deterministic, lift budget 300)", "", "| pose | composition | n | d success [95 %] | b / c | McNemar p |", "|---|---|---|---|---|---|"]
    for pose, lab in POSES:
        R5 = rows(f"cd1_014CD_h5_{pose}_det_lb300")
        for c in COMPS:
            if c == "5": continue
            R = rows(f"cd1_014CD_h{c}_{pose}_det_lb300")
            if R and R5: n, m, lo, hi, b, cc, p = paired(R, R5); md.append(f"| {lab} | {c} | {n} | {m:+.3f} [{lo:+.3f}, {hi:+.3f}] | {b} / {cc} | {p:.3g} |")
    md += ["\n### 014CD − 014 r2 (24M), paired by row (deterministic, lift budget 300): what C / D did", "",
           "| pose | composition | n | 014CD success [95 %] | 014 r2 success [95 %] | d (CD − r2) [95 % paired] | b / c | McNemar p | 014CD lift | 014 r2 lift | 014CD drop | 014 r2 drop |", "|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for pose, lab in POSES:
        for c in COMPS:
            A, B = rows(f"cd1_014CD_h{c}_{pose}_det_lb300"), rows(f"cd2_014r2_h{c}_{pose}_det_lb300")
            if not A or not B: md.append(f"| {lab} | {c} | missing |"); continue
            chk(A, B, f"cd2 {pose} h{c}"); n, m, lo, hi, b, cc, p = paired(A, B); ca, cb = cell(A), cell(B)
            md.append(f"| {lab} | {c} | {n} | {ca[1]:.3f} [{ca[2]:.3f}, {ca[3]:.3f}] | {cb[1]:.3f} [{cb[2]:.3f}, {cb[3]:.3f}] | **{m:+.3f}** [{lo:+.3f}, {hi:+.3f}] | {b} / {cc} | {p:.3g} | {ca[5]:.3f} | {cb[5]:.3f} | {ca[7]['drop']:.3f} | {cb[7]['drop']:.3f} |")
    md += ["\n### 014CD − 013E on the 5-digit hand, paired by row (deterministic; 013E legacy layout with the envelope filter forced, all rows)", "",
           "| cell | lift budget | n | 014CD success [95 %] | 013E success [95 %] | d (CD − 013E) [95 % paired] | b / c | McNemar p | rows identical |", "|---|---|---|---|---|---|---|---|---|"]
    for pose, lab in POSES:
        for lb in (300, 200):
            A, B = rows(f"cd1_014CD_h5_{pose}_det_lb{lb}"), rows(f"cd3_013E_h5_{pose}_det_lb{lb}")
            if not A or not B: md.append(f"| {lab} | {lb} | missing |"); continue
            k, nn = chk(A, B, f"cd3 {pose} lb{lb}"); n, m, lo, hi, b, cc, p = paired(A, B); ca, cb = cell(A), cell(B)
            md.append(f"| {lab} | {lb} | {n} | {ca[1]:.3f} [{ca[2]:.3f}, {ca[3]:.3f}] | {cb[1]:.3f} [{cb[2]:.3f}, {cb[3]:.3f}] | **{m:+.3f}** [{lo:+.3f}, {hi:+.3f}] | {b} / {cc} | {p:.3g} | {k} / {nn} |")
    md += ["\n### Anchor cylinder 7001–7300, 5-digit hand, deterministic", "", "| model | lift budget | n | success [95 %] | gate | lift | hold | end drop | end maxStep |", "|---|---|---|---|---|---|---|---|---|"]
    AN = {k: rows(t) for k, t in [("014CD 300", "cd4_anchor_014CD_h5_det_lb300"), ("014CD 200", "cd4_anchor_014CD_h5_det_lb200"), ("014 r2 300", "cd4_anchor_014r2_h5_det_lb300"), ("013E 300", "cd4_anchor_013E_h5_det_lb300")]}
    for k, R in AN.items():
        if not R: md.append(f"| {k} | missing |"); continue
        c_ = cell(R); md.append(f"| {k.rsplit(' ', 1)[0]} | {k.rsplit(' ', 1)[1]} | {c_[0]} | {c_[1]:.3f} [{c_[2]:.3f}, {c_[3]:.3f}] | {c_[4]:.3f} | {c_[5]:.3f} | {c_[6]:.3f} | {c_[7]['drop']:.3f} | {c_[7]['maxStep']:.3f} |")
    md += ["", "| paired (lift budget 300) | n | d [95 % paired] | b / c | McNemar p |", "|---|---|---|---|---|"]
    for lab, a, b_ in [("014CD − 014 r2", "014CD 300", "014 r2 300"), ("014CD − 013E", "014CD 300", "013E 300"), ("014 r2 − 013E", "014 r2 300", "013E 300")]:
        A, B = AN[a], AN[b_]
        if A and B: chk(A, B, f"anchor {lab}"); n, m, lo, hi, b, cc, p = paired(A, B); md.append(f"| {lab} | {n} | **{m:+.3f}** [{lo:+.3f}, {hi:+.3f}] | {b} / {cc} | {p:.3g} |")
    tot = sum(x[2] for x in mism)
    md += ["\n### Placement check (object index, pose mode, rest-pose index, spawn yaw; every paired pass)", "", f"{len(mism)} pass pairs compared, **{tot} mismatched rows** in total."]
    md += [f"- {lab}: {nn - bad} / {nn} identical" for lab, nn, bad in mism if bad]
    sys.stdout.buffer.write(("\n".join(md) + "\n").encode("utf-8"))


if __name__ == "__main__":
    if sys.argv[1:] == ["cd"]: main_cd()
    else: main()
