"""Run 014 checkpoint-3 training criteria (post hoc, set by CJ 2026-10-01) over the slot-layout gate runs with the penetration / contact trace:
  (i)   no self-penetration on any hand (every gate, every trial; the trace's link-pair maximum, parent-child and palm-base pairs excluded as in the physics);
  (ii)  hold-window penetration at task-level loads <= 3 mm absolute on every hand: G1 lift (0.2 / 0.6 / 1.5 kg) and G2 at perturbation scale 1.0;
  (iii) force-limit gates (G6 20 / 40 / 80 N, G4 max) reported, not gated, with the mean contact count in each hold window.
Consistency check: at each load, hold-window penetration x window contacts should be roughly constant across hands (force per contact); "roughly" is
taken as max / min across hands <= 2. Contacts = touching finger groups (the agent's count) + the palm's contact fraction.

usage: python -m tools.eval.contact_load_014 [--dir results/014/gates_slots_v2] [--hands 3 5 6a 6b 7] [--out results/014/CRITERIA_014.md]
"""
import argparse, csv, os, re, statistics as st, sys
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RX_WIN = re.compile(r"penWindow n=(\d+) max=([\d.]+)mm p95=([\d.]+)mm median=([\d.]+)mm")
RX_TR = re.compile(r"penTrace objMax=([\d.]+)mm\(([^)]*)\) selfMax=([\d.]+)mm\(([^)]*)\) windowContacts mean=([\d.na]+) palm=([\d.na]+) n=(\d+)")


def trials(path):
    out = []
    if not os.path.exists(path): return out
    for r in csv.DictReader(open(path, newline="")):
        if not (r.get("trial") or "").isdigit(): continue
        note = r.get("note") or ""; w = RX_WIN.search(note); t = RX_TR.search(note)
        out.append({"mass": float(r["mass"]), "scale": float(r["scale"]), "mode": r["mode"], "end": r["endReason"], "success": r["success"] == "1", "maxPen": float(r["maxPenMm"]),
                    "winMax": float(w.group(2)) if w else float("nan"), "winMed": float(w.group(4)) if w else float("nan"),
                    "self": float(t.group(3)) if t else float("nan"), "selfWhere": t.group(4) if t else "",
                    "contacts": float(t.group(5)) if t and t.group(5) != "nan" else float("nan"), "palm": float(t.group(6)) if t and t.group(6) != "nan" else float("nan")})
    return out


def agg(T):
    v = [t for t in T if t["winMax"] == t["winMax"]]
    if not v: return None
    c = [t["contacts"] + (t["palm"] if t["palm"] == t["palm"] else 0) for t in v if t["contacts"] == t["contacts"]]
    win = st.mean(t["winMax"] for t in v); med = st.mean(t["winMed"] for t in v); cc = st.mean(c) if c else float("nan")
    return {"n": len(v), "ok": sum(t["success"] for t in T), "win": win, "worst": max(t["winMax"] for t in v), "med": med, "contacts": cc, "pc": win * cc, "pcMed": med * cc}


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--dir", default="results/014/gates_slots_v2"); ap.add_argument("--hands", nargs="*", default=["3", "5", "6a", "6b", "7"])
    ap.add_argument("--out", default="results/014/CRITERIA_014.md"); a = ap.parse_args()
    H = a.hands; D = lambda h, g: trials(os.path.join(REPO, a.dir, "h" + h, g + ".csv"))
    md = ["# Run 014 training criteria (post hoc, checkpoint 3) on the slot-layout gates with contact tracing", "", f"Source: `{a.dir}` (spike-6 gate configs, penTrace).", ""]
    # (i) self-penetration
    md += ["## (i) Self-penetration (every gate, every trial)", "", "| hand | max link-pair penetration | where |", "|---|---|---|"]
    ok_i = True
    for h in H:
        worst, where = 0.0, ""
        for g in ("g1", "g1x", "g2", "g2x", "g3m", "g3mx", "g3s", "g3x", "g4", "g6_10", "g6_20", "g6_40", "g6_80"):
            for t in D(h, g):
                if t["self"] == t["self"] and t["self"] > worst: worst, where = t["self"], g + ": " + t["selfWhere"]
        ok_i &= worst == 0.0
        md.append(f"| {h} | {worst:.2f} mm | {where or '-'} |")
    md += ["", f"**(i) {'PASS' if ok_i else 'FAIL'}**", ""]
    # (ii) task-level loads
    md += ["## (ii) Hold-window penetration at task-level loads (gate: <= 3 mm on every hand)", "", "| load | " + " | ".join(f"{h}-digit: window max mm (contacts)" for h in H) + " |", "|---|" + "---|" * len(H)]
    ok_ii = True; loads = []
    for g, key, label in [("g1", 0.2, "G1 lift 0.2 kg"), ("g1", 0.6, "G1 lift 0.6 kg"), ("g1", 1.5, "G1 lift 1.5 kg"), ("g2", 1.0, "G2 0.6 kg, perturbation 1.0")]:
        row = []
        for h in H:
            T = [t for t in D(h, g) if (abs(t["mass"] - key) < 1e-3 if g == "g1" else abs(t["scale"] - key) < 1e-3)]; s = agg(T)
            if s is None: row.append("-"); continue
            ok_ii &= s["worst"] <= 3.0
            row.append(f"{s['worst']:.2f}{' **>3**' if s['worst'] > 3.0 else ''} ({s['contacts']:.2f})")
        md.append(f"| {label} | " + " | ".join(row) + " |"); loads.append((g, key, label))
    md += ["", f"**(ii) {'PASS' if ok_ii else 'FAIL'}**", ""]
    # (iii) force-limit gates, reported
    md += ["## (iii) Force-limit gates (reported, not gated)", "", "| gate | " + " | ".join(f"{h}-digit" for h in H) + " |", "|---|" + "---|" * len(H)]
    for g, label in [("g6_10", "G6 push 10 N"), ("g6_20", "G6 push 20 N"), ("g6_40", "G6 push 40 N"), ("g6_80", "G6 push 80 N")]:
        row = []
        for h in H:
            T = D(h, g); s = agg(T)
            row.append("-" if s is None else f"{'held' if T and T[0]['end'] != 'drop' else 'pushed out'}, window max {s['worst']:.2f} mm, contacts {s['contacts']:.2f}, max {max(t['maxPen'] for t in T):.2f} mm")
        md.append(f"| {label} | " + " | ".join(row) + " |")
    row = []
    for h in H:
        T = D(h, "g4"); row.append("-" if not T else f"{sum(t['success'] for t in T)}/{len(T)} finite, max {max(t['maxPen'] for t in T):.2f} mm")
    md.append("| G4 stability (20 draws) | " + " | ".join(row) + " |")
    # consistency: penetration x contacts
    md += ["", "## Penetration x contacts across hands at the same load", "",
           "Window penetration (mean over the load's trials of the window maximum, and of the window median) times the mean window contact count (finger groups + palm).", "",
           "| load | " + " | ".join(f"{h}-digit max x c / median x c" for h in H) + " | max / min (max x c) | max / min (median x c) |", "|---|" + "---|" * len(H) + "---|---|"]
    consistent = True
    all_loads = loads + [("g6_20", None, "G6 push 20 N"), ("g6_40", None, "G6 push 40 N"), ("g6_80", None, "G6 push 80 N")]
    for g, key, label in all_loads:
        vals, vmed, row = [], [], []
        for h in H:
            T = D(h, g)
            if key is not None: T = [t for t in T if (abs(t["mass"] - key) < 1e-3 if g == "g1" else abs(t["scale"] - key) < 1e-3)]
            s = agg(T)
            if s is None or s["pc"] != s["pc"]: row.append("-"); continue
            vals.append(s["pc"]); vmed.append(s["pcMed"]); row.append(f"{s['pc']:.1f} / {s['pcMed']:.1f}")
        r1 = max(vals) / min(vals) if vals and min(vals) > 0 else float("nan"); r2 = max(vmed) / min(vmed) if vmed and min(vmed) > 0 else float("nan")
        consistent &= (r1 <= 2.0)
        md.append(f"| {label} | " + " | ".join(row) + f" | {r1:.2f} | {r2:.2f} |")
    md += ["", f"**Penetration x contacts roughly constant across hands (max / min <= 2 at every load, window-maximum based): {'YES' if consistent else 'NO'}**"]
    s = "\n".join(md) + "\n"; open(os.path.join(REPO, a.out), "w", encoding="utf-8").write(s); sys.stdout.buffer.write(s.encode("utf-8"))
