"""Brief 2c / 2d: noise floor and cost of J(theta) at the reference theta on the chosen substrate.

  python -m tools.bo.noise_013 --model M --session 1 --repeats 3     (throwaway, then J(reference) x 3 in one Editor session)
  python -m tools.bo.noise_013 --model M --session 2 --repeats 1     (after an Editor relaunch: throwaway, then J(reference) x 1)
  python -m tools.bo.noise_013 --summary                              (noise floor = max(0.002, variance of the 4 J values); seconds per J)
Appends to results/bo/partC/noise.json.
"""
import argparse, json, math, os, statistics as st, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_reduced as TR
from tools.bo.objective_013 import J
from tools.bo.gates_runner import run_pass, REPO
from tools.bo.campaign_013 import throwaway_cfg

OUT = "results/bo/partC"; LOG = os.path.join(REPO, OUT, "noise.json")


def load(): return json.load(open(LOG)) if os.path.exists(LOG) else []


def summary():
    L = [e for e in load() if "J" in e]; v = [e["J"] for e in L]
    var = st.variance(v) if len(v) > 1 else float("nan")
    s = {"n": len(v), "J": v, "mean": st.mean(v) if v else None, "variance": var, "sd": math.sqrt(var) if v and len(v) > 1 else None, "noise_floor": max(0.002, var) if len(v) > 1 else None,
         "wall_s": [e["wall_s"] for e in L], "wall_mean_s": st.mean(e["wall_s"] for e in L) if L else None,
         "per_mode": [{k: e["per_mode"][k]["success"] for k in e["per_mode"]} for e in L], "hashes": [e["hash"] for e in L]}
    print(json.dumps(s, indent=1)); return s


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--model"); ap.add_argument("--session", type=int); ap.add_argument("--repeats", type=int, default=1); ap.add_argument("--summary", action="store_true"); a = ap.parse_args()
    if a.summary: summary(); sys.exit(0)
    os.makedirs(os.path.join(REPO, OUT, "passes"), exist_ok=True)
    run_pass(throwaway_cfg(a.model, f"{OUT}/passes/throwaway_noise_s{a.session}_{time.strftime('%H%M%S')}.csv"), max_wait=600)
    for i in range(a.repeats):
        r = J(TR.to_theta(TR.reference_u()), a.model, f"noise_ref_s{a.session}_r{i}", f"{OUT}/passes")
        L = load(); L.append(dict(r, session=a.session, repeat=i, time=time.strftime("%Y-%m-%d %H:%M:%S"))); json.dump(L, open(LOG, "w"), indent=1)
        print(f"session {a.session} repeat {i}: J {r['J']:.4f} (canonical {r['per_mode']['canonical']['success']:.3f}, randomStable {r['per_mode']['randomStable']['success']:.3f}, n {r['n']}, shapes ok {r['shape_counts_ok']}) "
              f"hash {r['hash']} wall {r['wall_s']:.0f} s", flush=True)
