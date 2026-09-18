"""Palm-contact rate of the run-010 policy by theta bin, from the existing random-theta evaluation records (read-only).

Input : results/010/validation/episodes.csv (seeds 3001-3100, theta sampled per episode; columns palm, holdReached,
        lenMean, activeGroups, contacts) plus the two reference-hand evaluations for context.
Output: results/bo_eval/analysis/palm_by_theta.md (also printed).
usage : python -m tools.bo_eval.palm_by_theta [episodes.csv] [out.md]
"""
import csv
import os
import statistics
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
LEN_BINS = ((0.80, 0.93), (0.93, 1.07), (1.07, 1.201))
GROUP_BINS = ((6, 9), (10, 11), (12, 13), (14, 14))


def rows(path):
    with open(path, newline="") as f:
        return list(csv.DictReader(f))


def rate(rs):
    return (statistics.mean(int(r["palm"]) for r in rs), len(rs)) if rs else (float("nan"), 0)


def main(argv):
    src = argv[0] if argv else os.path.join(REPO, "results", "010", "validation", "episodes.csv")
    out = argv[1] if len(argv) > 1 else os.path.join(REPO, "results", "bo_eval", "analysis", "palm_by_theta.md")
    eps = rows(src)
    need = ("palm", "holdReached", "lenMean", "activeGroups", "contacts", "randomizing")
    missing = [c for c in need if c not in eps[0]]
    if missing:
        raise SystemExit("columns missing from " + src + ": " + ", ".join(missing))
    held = [r for r in eps if r["holdReached"] == "1"]
    md = [f"# Run 010 palm-contact rate by theta bin (random theta, {os.path.relpath(src, REPO)}, n = {len(eps)} episodes, {len(held)} reached the hold)", "",
          "palm = LastPalmTouching at hold completion (Q-only palm collider; never part of the success gate). Rates are over episodes that reached the hold; the last column repeats them over all episodes (palm at episode end, 0 for failures).", "",
          "| bin | n (held) | palm-contact rate (held) | mean contacts (held) | palm rate (all episodes, n) |", "|---|---|---|---|---|"]

    def emit(label, sel_held, sel_all):
        pr, n = rate(sel_held)
        pa, na = rate(sel_all)
        mc = statistics.mean(float(r["contacts"]) for r in sel_held) if sel_held else float("nan")
        md.append(f"| {label} | {n} | {pr:.2f} | {mc:.2f} | {pa:.2f} (n={na}) |")

    emit("all random theta", held, eps)
    md.append("| **mean link-length scale** | | | | |")
    for lo, hi in LEN_BINS:
        emit(f"{lo:.2f}-{min(hi, 1.20):.2f}", [r for r in held if lo <= float(r["lenMean"]) < hi], [r for r in eps if lo <= float(r["lenMean"]) < hi])
    md.append("| **active finger groups** | | | | |")
    for lo, hi in GROUP_BINS:
        emit(f"{lo}-{hi}" if lo != hi else f"{lo}", [r for r in held if lo <= int(r["activeGroups"]) <= hi], [r for r in eps if lo <= int(r["activeGroups"]) <= hi])
    # context: the two reference-hand evaluations, if present
    for label, p in (("010 reference hand (seeds 2001-2100)", os.path.join(REPO, "results", "010", "validation", "reference_hand", "episodes.csv")),
                     ("009 reference hand (seeds 2001-2100)", os.path.join(REPO, "results", "009", "validation", "009", "episodes.csv"))):
        if os.path.isfile(p):
            rs = rows(p)
            if "palm" in rs[0]:
                hs = [r for r in rs if r["holdReached"] == "1"]
                emit(label, hs, rs)
    text = "\n".join(md) + "\n"
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w") as f:
        f.write(text)
    print(text)


if __name__ == "__main__":
    main(sys.argv[1:])
