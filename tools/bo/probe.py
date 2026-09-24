"""2b dynamic-range probe: 20 theta from the training distribution (seed 424242), each evaluated at perturbation scales
1.0 / 1.5 / 2.0 / 3.0 on seeds 4001-4100 (deterministic head, mu 1.0, K = 50). Objective level rule: the smallest scale
whose mean success over the 20 theta lies in [0.45, 0.80] AND whose across-theta SD >= 0.10. If none qualifies, the mass
range is doubled (0.4-3.0 kg log-uniform, environment parameters mass/min, mass/max) at scales 1.0 and 2.0.
Resumable: results/bo/partB/probe.csv is the log (one row per pass).
usage: python -m tools.bo.probe [--fallback]
"""
import csv, json, os, sys, time, statistics as st
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_space
from tools.bo.evaluate_theta import evaluate, throwaway

OUT = "results/bo/partB"; LOG = os.path.join(OUT, "probe.csv"); SEEDS = (4001, 4100); SCALES = (1.0, 1.5, 2.0, 3.0); N_THETA = 20; THETA_SEED = 424242
FIELDS = ["theta_id", "scale", "mass_min", "mass_max", "success", "lo", "hi", "n", "reach_failure", "lifted", "palm", "contacts", "hold", "hash", "wall_s", "csv", "time"]


def load_log():
    if not os.path.exists(LOG): return []
    return list(csv.DictReader(open(LOG, newline="")))


def append(row):
    new = not os.path.exists(LOG)
    with open(LOG, "a", newline="") as f:
        w = csv.DictWriter(f, fieldnames=FIELDS); (w.writeheader() if new else None); w.writerow(row)


def run(scales, mass_range=None):
    thetas = theta_space.sample_training_distribution(N_THETA, THETA_SEED); os.makedirs(os.path.join(OUT, "probe_passes"), exist_ok=True)
    json.dump(thetas, open(os.path.join(OUT, "probe_thetas.json"), "w"), indent=0)
    done = {(r["theta_id"], r["scale"], r["mass_min"], r["mass_max"]) for r in load_log()}
    for scale in scales:
        for i, th in enumerate(thetas):
            key = (str(i), f"{scale:.1f}", f"{mass_range[0]:.2f}" if mass_range else "", f"{mass_range[1]:.2f}" if mass_range else "")
            if key in done: continue
            tag = f"probe_t{i:02d}_s{scale:.1f}" + (f"_m{mass_range[0]:.1f}-{mass_range[1]:.1f}" if mass_range else "")
            r = evaluate(th, SEEDS, perturb=scale, csv=f"{OUT}/probe_passes/{tag}.csv", mass_range=mass_range)
            append({"theta_id": i, "scale": f"{scale:.1f}", "mass_min": key[2], "mass_max": key[3], "success": f"{r['success']:.4f}", "lo": f"{r['wilson'][0]:.4f}", "hi": f"{r['wilson'][1]:.4f}", "n": r["n"], "reach_failure": f"{r['reach_failure']:.4f}",
                    "lifted": f"{r['lifted']:.4f}", "palm": f"{r['palm']:.4f}", "contacts": f"{r['contacts']:.3f}", "hold": f"{r['hold']:.1f}", "hash": r["hash"], "wall_s": f"{r['wall_s']:.0f}", "csv": r["csv"], "time": time.strftime("%H:%M:%S")})
            print(f"theta {i:2d} scale {scale:.1f} mass {mass_range or 'default'}: success {r['success']:.2f} reach_fail {r['reach_failure']:.2f} ({r['wall_s']:.0f} s)", flush=True)


def table():
    rows = load_log(); out = ["| scale | mass range | n theta | mean success | min | max | SD across theta | qualifies |", "|---|---|---|---|---|---|---|---|"]; chosen = None
    for key in sorted({(r["scale"], r["mass_min"], r["mass_max"]) for r in rows}, key=lambda k: (k[1], float(k[0]))):
        s = [float(r["success"]) for r in rows if (r["scale"], r["mass_min"], r["mass_max"]) == key]
        if len(s) < N_THETA: out.append(f"| {key[0]} | {key[1] or 'default'}-{key[2] or ''} | {len(s)} | (incomplete) | | | | |"); continue
        m, sd = st.mean(s), st.stdev(s); q = 0.45 <= m <= 0.80 and sd >= 0.10
        out.append(f"| {key[0]} | {(key[1] + '-' + key[2]) if key[1] else 'default (0.2-1.5)'} | {len(s)} | {m:.3f} | {min(s):.2f} | {max(s):.2f} | {sd:.3f} | {'yes' if q else 'no'} |")
        if q and chosen is None: chosen = key
    out.append(""); out.append(f"chosen level: {chosen if chosen else 'NONE'}"); return "\n".join(out), chosen


if __name__ == "__main__":
    if "--table" not in sys.argv:
        throwaway()
        if "--fallback" in sys.argv: run((1.0, 2.0), mass_range=(0.4, 3.0))
        else: run(SCALES)
    t, chosen = table(); print(t); open(os.path.join(OUT, "probe_table.md"), "w").write(t + "\n")
