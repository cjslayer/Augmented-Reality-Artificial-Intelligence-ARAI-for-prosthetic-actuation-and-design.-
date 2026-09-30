"""Run 014 Part 2f layout proof (Editor harness, mode "layout"): link tables of the built hand in the legacy layout and in the slot layout for every composition;
the 5-digit slot hand must reproduce the legacy hand (every link length, radius, mass and pivot within 1 %). Also the shape-guard check (the slot layout must refuse
a legacy model) and a short slot-layout lift on the 7-digit hand to exercise the token / ray pipeline.

  python -m tools.eval.layout_014 run --out results/014/layout
  python -m tools.eval.layout_014 table --out results/014/layout
"""
import argparse, csv, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo.gates_runner import run_pass, console, REPO

DUMPS = {"legacy": {}, "slots_3": {"topology": "slots", "digits": 3}, "slots_4": {"topology": "slots", "digits": 4}, "slots_5": {"topology": "slots", "digits": 5},
         "slots_6a": {"topology": "slots", "digits": 6}, "slots_6b": {"topology": "slots", "digits": 6, "digits6b": True}, "slots_7": {"topology": "slots", "digits": 7}}


def logs(pattern): return [m for lvl, m in console(400) if pattern in m]


def run(out):
    os.makedirs(os.path.join(REPO, out), exist_ok=True); notes = {}
    for name, extra in DUMPS.items():
        c = {"mode": "layout", "timeScale": 1, "csv": f"{out}/{name}.csv"}; c.update(extra)
        run_pass(c, max_wait=300)
        notes[name] = {"layout_check": logs("layout check passed")[:1] + logs("FATAL")[:2], "sensors": logs("created sensors")[:1]}
        print(name, "done |", notes[name]["sensors"][:1], flush=True)
    # guard: a legacy (013E) model under the slot layout must be refused
    c = {"mode": "eval", "topology": "slots", "digits": 5, "modelPath": "results/013/013E/Prosthetic.onnx", "seedFrom": 12001, "seedTo": 12002, "timeScale": 20, "headMode": "deterministic",
         "jobWorkers": 1, "csv": f"{out}/guard_slots_013E.csv"}
    try: run_pass(c, max_wait=300)
    except Exception as ex: print("guard pass ended:", repr(ex)[:120])
    g = os.path.join(REPO, out, "guard_slots_013E.csv"); notes["guard"] = {"csv": open(g).read()[:400] if os.path.exists(g) else "", "log": logs("REFUSED")[:1]}
    print("guard:", notes["guard"], flush=True)
    # slot-layout smoke: one scripted lift on the 7-digit hand (observations and the folded rays are collected every decision in heuristic mode)
    c = json.load(open(os.path.join(REPO, "tools/eval/gates_014/g1.json"))); c.pop("_source", None); c.update({"topology": "slots", "digits": 7, "masses": [0.6], "seeds": 1, "csv": f"{out}/smoke_lift_7.csv"})
    run_pass(c, max_wait=600); notes["smoke7"] = {"errors": [m[:200] for lvl, m in console(400) if lvl in ("error", "exception", "Error", "Exception")][:5], "sensors": logs("created sensors")[:1]}
    print("smoke7:", notes["smoke7"], flush=True)
    json.dump(notes, open(os.path.join(REPO, out, "notes.json"), "w"), indent=1)


def load(p): return [r for r in csv.DictReader(open(os.path.join(REPO, p), newline=""))] if os.path.exists(os.path.join(REPO, p)) else []


def table(out):
    L = {r["link"]: r for r in load(f"{out}/legacy.csv") if r.get("link")}; S = {r["link"]: r for r in load(f"{out}/slots_5.csv") if r.get("link")}
    md = ["# Run 014 layout proof: 5-digit slot hand vs the legacy (013) hand", "", "| link | length mm legacy / slots | radius mm | mass g | pivot along / out / across mm | max rel. diff |", "|---|---|---|---|---|---|"]
    worst = 0.0
    for k in L:
        a, b = L[k], S.get(k)
        if b is None: md.append(f"| {k} | missing in slots | | | | - |"); worst = float("inf"); continue
        rel = 0.0
        for f in ("lengthMm", "radiusMm", "capHeightMm", "massG", "pivotAlongMm", "pivotOutMm", "pivotAcrossMm"):
            if a[f] == "" and b[f] == "": continue
            x, y = float(a[f] or 0), float(b[f] or 0); rel = max(rel, abs(x - y) / max(abs(x), 1e-6) if abs(x) > 1e-3 else abs(x - y))
        worst = max(worst, rel)
        md.append(f"| {k} | {a['lengthMm']} / {b['lengthMm']} | {a['radiusMm']} / {b['radiusMm']} | {a['massG']} / {b['massG']} | {a['pivotAlongMm']}, {a['pivotOutMm']}, {a['pivotAcrossMm']} / {b['pivotAlongMm']}, {b['pivotOutMm']}, {b['pivotAcrossMm']} | {rel:.2e} |")
    extra = [k for k in S if k not in L]
    md += ["", f"Links only in the slot table: {extra or 'none'}. **Largest relative difference over every field: {worst:.2e} (criterion <= 1e-2).**", "", "## Every composition", "",
           "| hand | links | hand mass g (palm + fingers) | palm width mm | slots |", "|---|---|---|---|---|"]
    for name in DUMPS:
        R = [r for r in load(f"{out}/{name}.csv") if r.get("link")]
        if not R: continue
        mass = sum(float(r["massG"]) for r in R); palm = [r for r in R if r["link"] == "L_Palm"]
        md.append(f"| {name} | {len(R) - 1} | {mass:.1f} | {palm[0]['capHeightMm'] if palm else '-'} | {R[0]['layout']} |")
    s = "\n".join(md) + "\n"; open(os.path.join(REPO, out, "LAYOUT.md"), "w", encoding="utf-8").write(s); sys.stdout.buffer.write(s.encode("utf-8"))


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("cmd"); ap.add_argument("--out", default="results/014/layout"); a = ap.parse_args()
    run(a.out) if a.cmd == "run" else table(a.out)
