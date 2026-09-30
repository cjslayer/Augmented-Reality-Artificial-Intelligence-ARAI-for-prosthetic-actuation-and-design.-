"""Run 014 Part 3 gate table: per gate and hand, trials passed, max penetration (the agent's per-episode maximum over the finger links, maxPenMm) and the
hold-window maximum (penWindow, the harness's hand-object statistic in the hold / squeeze / push window), against the fresh 5-digit TGS baseline on the same
gate (results/014/gates_baseline, unmodified HEAD). Rule (checkpoint 2): no training on a hand whose penetration exceeds 2x the baseline on any gate.

usage: python -m tools.eval.gates_table_014 [--hands 3 5 6a 6b 7] [--base results/014/gates_baseline] [--dir results/014/gates_slots] [--out results/014/GATES_014.md]
"""
import argparse, csv, os, re, sys
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GATES = [("g1", "G1 lift 0.2 / 0.6 / 1.5 kg"), ("g1x", "G1 ext 2.0 / 2.5 kg"), ("g2", "G2 perturb 0.25-1.5"), ("g2x", "G2 ext 2.0-4.0"), ("g3m", "G3 pinch 0.2 / 0.6 / 1.5 kg"),
         ("g3mx", "G3 pinch 1.0 kg"), ("g3s", "G3 pinch perturb 0.25-1.5"), ("g3x", "G3 pinch perturb 2.0-4.0"), ("g4", "G4 stability (20 draws)"),
         ("g6_10", "G6 push 10 N"), ("g6_20", "G6 push 20 N"), ("g6_40", "G6 push 40 N"), ("g6_80", "G6 push 80 N")]


def stats(path):
    if not os.path.exists(path): return None
    R = [r for r in csv.DictReader(open(path, newline="")) if (r.get("trial") or "").isdigit()]
    if not R: return None
    win = [float(m.group(1)) for r in R for m in [re.search(r"penWindow n=\d+ max=([\d.]+)mm", r.get("note") or "")] if m]
    push = R[0]["mode"] == "push"
    ok = sum(1 for r in R if (r["endReason"] in ("maxStep", "walk") if push else r["success"] == "1"))   # stability: success = finite (no NaN)
    held = sum(1 for r in R if r["endReason"] != "drop") if push else None
    nan = sum(1 for r in R if r.get("nan") == "1")
    return {"n": len(R), "ok": ok, "held": held, "max": max(float(r["maxPenMm"]) for r in R), "win": max(win) if win else float("nan"), "nan": nan, "ends": sorted(set(r["endReason"] for r in R))}


def cell(s, b):
    if s is None: return "-"
    flag = ""
    if b is not None:
        rm = s["max"] / b["max"] if b["max"] > 0 else float("inf"); rw = s["win"] / b["win"] if b["win"] == b["win"] and b["win"] > 0 else float("nan")
        if rm > 2.0 or (rw == rw and rw > 2.0): flag = " **>2x**"
        return f"{s['ok']}/{s['n']}{'' if s['held'] is None else ' held ' + str(s['held'])}, max {s['max']:.2f} ({rm:.2f}x), win {s['win']:.2f} ({rw:.2f}x){' NaN ' + str(s['nan']) if s['nan'] else ''}{flag}"
    return f"{s['ok']}/{s['n']}{'' if s['held'] is None else ' held ' + str(s['held'])}, max {s['max']:.2f}, win {s['win']:.2f}{' NaN ' + str(s['nan']) if s['nan'] else ''}"


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--hands", nargs="*", default=["3", "5", "6a", "6b", "7"]); ap.add_argument("--base", default="results/014/gates_baseline")
    ap.add_argument("--dir", default="results/014/gates_slots"); ap.add_argument("--out", default="results/014/GATES_014.md"); a = ap.parse_args()
    md = ["# Run 014 gates by hand (slot layout) vs the fresh 5-digit TGS baseline (unmodified HEAD, `results/014/gates_baseline`)", "",
          "Cells: trials passed (lift / pinch: success; G4: finite; G6: episode not ended by the agent, 'held' = not pushed out), max = the agent's per-episode maximum",
          "finger-object penetration (mm, ratio to the baseline), win = the hold / squeeze / push window maximum (mm, ratio). **>2x** marks the checkpoint-2 rule.", "",
          "| gate | baseline (5-digit, HEAD) | " + " | ".join(f"{h}-digit" for h in a.hands) + " |", "|---|---|" + "---|" * len(a.hands)]
    over = {h: [] for h in a.hands}
    for g, label in GATES:
        b = stats(os.path.join(REPO, a.base, g + ".csv")); row = [cell(b, None)]
        for h in a.hands:
            s = stats(os.path.join(REPO, a.dir, "h" + h, g + ".csv")); c = cell(s, b); row.append(c)
            if "**>2x**" in c: over[h].append(g)
        md.append(f"| {label} | " + " | ".join(row) + " |")
    md += ["", "Hands over 2x the baseline on any gate: " + ", ".join(f"{h}-digit: {', '.join(v) if v else 'none'}" for h, v in over.items())]
    s = "\n".join(md) + "\n"; open(os.path.join(REPO, a.out), "w", encoding="utf-8").write(s); sys.stdout.buffer.write(s.encode("utf-8"))
