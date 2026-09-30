"""Run 014 physics gates: G1-G4 and G6 with the spike-6 harness configs (tools/eval/gates_014/*.json, recovered from the spike-6 session),
on any hand configuration, through the Editor harness (ArticulatedGates, close-until-contact controller). G5 (throughput) is the trainer probe.

  python -m tools.eval.gates_014 run --out results/014/gates_baseline [--tags g1 g1x ...] [--extra '{"topology": "slots", "digits": 7}']
  python -m tools.eval.gates_014 table --out results/014/gates_baseline [--ref results/011_artic2]   (markdown summary, per gate)

Gate meaning (MLAGENTS_UPGRADE.md:773-828): G1 lift 0.2 / 0.6 / 1.5 kg (+ 2.0 / 2.5 ext), G2 perturbation 0.6 kg scales 0.25-4.0, G3 pinch (index + thumb) by
mass and by scale (must be strictly weaker than the power grasp), G4 stability (20 random-θ draws x 2000 steps), G6 push at 10 / 20 / 40 / 80 N
(every drive at its force limit). Penetration: the hold-window statistic in the note column (penWindow max / p95).
"""
import argparse, csv, json, os, re, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo.gates_runner import run_pass, REPO

CFG = os.path.join(REPO, "tools", "eval", "gates_014")
ALL = ["g1", "g1x", "g2", "g2x", "g3m", "g3mx", "g3s", "g3x", "g4", "g6_10", "g6_20", "g6_40", "g6_80"]
SPIKE = {"g1": "g1_s6", "g1x": "g1_s6_ext", "g2": "g2_s6", "g2x": "g2_s6_ext", "g3m": "g3_s5_mass", "g3mx": "g3_s5_mass_ext", "g3s": "g3_s5_scale", "g3x": "g3_s5_scale_ext",
         "g4": "g4_s6", "g6_10": "g6_s6_10N", "g6_20": "g6_s6_20N", "g6_40": "g6_s6_40N", "g6_80": "g6_s6_80N"}


def run(out, tags, extra):
    os.makedirs(os.path.join(REPO, out), exist_ok=True); log = os.path.join(REPO, out, "passes.json"); L = json.load(open(log)) if os.path.exists(log) else []
    for t in tags:
        c = json.load(open(os.path.join(CFG, t + ".json"))); c.pop("_source", None); c.update(extra or {}); c["csv"] = f"{out}/{t}.csv"
        t0 = time.time()
        try: path, wall = run_pass(c, max_wait=3600); ok = True; err = ""
        except Exception as ex:
            p = os.path.join(REPO, c["csv"]); ok = os.path.exists(p) and open(p).read().rstrip().endswith("DONE"); wall = time.time() - t0; err = repr(ex)[:300]
        L.append({"tag": t, "ok": ok, "csv": c["csv"], "wall_s": round(wall, 1), "extra": extra, "error": err, "time": time.strftime("%Y-%m-%d %H:%M:%S")}); json.dump(L, open(log, "w"), indent=1)
        print(t, "ok" if ok else "FAILED " + err, f"{wall:.0f} s", "|", summary_line(os.path.join(REPO, c["csv"])) if ok else "", flush=True)


def rows(path):
    if not os.path.exists(path): return []
    return [r for r in csv.DictReader(open(path, newline="")) if (r.get("trial") or "").isdigit()]


def pen_window(r):
    m = re.search(r"penWindow n=(\d+) max=([\d.]+)mm p95=([\d.]+)mm", r.get("note", "") or "")
    return (float(m.group(2)), float(m.group(3))) if m else (float("nan"), float("nan"))


def summary_line(path):
    R = rows(path)
    if not R: return "no rows"
    by = {}
    for r in R: by.setdefault((r["grip"], r["mass"], r["scale"]), []).append(r)
    parts = []
    for (g, m, s), v in sorted(by.items(), key=lambda kv: (kv[0][0], float(kv[0][1]), float(kv[0][2]))):
        pw = [pen_window(r)[0] for r in v]; pw = [x for x in pw if x == x]
        parts.append(f"{g} m{float(m):g} s{float(s):g}: {sum(int(r['success']) for r in v)}/{len(v)} ends {','.join(sorted(set(r['endReason'] for r in v)))}"
                     f" win {max(pw) if pw else float('nan'):.2f} mm ep {max(float(r['maxPenMm']) for r in v):.2f} mm c {'/'.join(r['contactsEnd'] for r in v)}")
    return "; ".join(parts)


def table(out, ref):
    md = [f"# Gates: {out}" + (f" vs {ref}" if ref else ""), "", "| gate | this run | reference |", "|---|---|---|"]
    for t in ALL:
        p = os.path.join(REPO, out, t + ".csv")
        if not os.path.exists(p): continue
        rp = os.path.join(REPO, ref, SPIKE[t] + ".csv") if ref else None
        md.append(f"| {t} | {summary_line(p)} | {summary_line(rp) if rp and os.path.exists(rp) else '-'} |")
    s = "\n".join(md) + "\n"; open(os.path.join(REPO, out, "GATES.md"), "w", encoding="utf-8").write(s); sys.stdout.buffer.write(s.encode("utf-8"))


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("cmd"); ap.add_argument("--out", required=True); ap.add_argument("--tags", nargs="*", default=ALL); ap.add_argument("--extra", default="{}"); ap.add_argument("--ref", default="")
    a = ap.parse_args()
    run(a.out, a.tags, json.loads(a.extra)) if a.cmd == "run" else table(a.out, a.ref)
