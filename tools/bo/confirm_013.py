"""Brief 3c: held-out confirmation of the Part C campaign.

theta set: top-3 BO theta and top-3 random theta by observed J (campaign.csv), the reference theta, and BO-best with the thumb-base mask bit
(group 12, MorphologyManager.cs:179-184) OFF (thumb end stays on, so the mask rule of MorphologyManager.Sample still holds).
Each theta: held-out shapes 65-80 (objectStratify heldout), seeds 11501-11800, passIndex 0, both presentations (n = 300 per mode),
perturbation 1.0, mu 1.0, K = 50, mass as training, deterministic head, one worker. Episodes pair across theta on (seed, shape) and on
every harness draw except the theta itself.
  python -m tools.bo.confirm_013 run --model M      (throwaway, then 8 theta x 2 presentations; resumable)
  python -m tools.bo.confirm_013 table              (paired CIs -> results/bo/partC/CONFIRM.md)
"""
import argparse, csv, json, math, os, statistics as st, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_reduced as TR, theta_space as TS
from tools.bo.gates_runner import run_pass, pass_hash, REPO
from tools.bo.campaign_013 import throwaway_cfg, uparse, LOG as CAMPAIGN
from tools.eval.pulse_vs_grasp import load, W
from tools.eval.summarize_pass import summarize

OUT = "results/bo/partC/confirm"; PLOG = os.path.join(REPO, OUT, "passes.json"); SEEDS = (11501, 11800); POSES = ("canonical", "randomStable")


def theta_set():
    rows = list(csv.DictReader(open(os.path.join(REPO, CAMPAIGN), newline="")))
    top = lambda arm: sorted([r for r in rows if r["arm"] == arm], key=lambda r: (-float(r["J"]), int(r["idx"])))[:3]
    S = {}
    for arm in ("bo", "random"):
        for rank, r in enumerate(top(arm), 1): S[f"{arm}{rank}"] = {"theta": TR.to_theta(uparse(r["unit"])), "source": f"{arm} #{r['idx']} (campaign J {float(r['J']):.3f})", "unit": r["unit"]}
    S["reference"] = {"theta": TR.to_theta(TR.reference_u()), "source": "spec nominal hand", "unit": ";".join(f"{x:.6f}" for x in TR.reference_u())}
    t = json.loads(json.dumps(S["bo1"]["theta"])); t["mask"][12] = 0
    assert TS.mask_ok(t["mask"]); S["bo1_thumbBaseOff"] = {"theta": t, "source": "bo1 with mask bit 12 (thumb base) off", "unit": S["bo1"]["unit"]}
    return S


def cfg(theta, model, pose, csv_path):
    c = {"mode": "eval", "modelPath": model, "seedFrom": SEEDS[0], "seedTo": SEEDS[1], "objectFriction": 1.0, "evalPerturbScale": 1.0, "evalHoldDecisions": 50, "timeScale": 20, "maxTrialSteps": 6000,
         "headMode": "deterministic", "passIndex": 0, "jobWorkers": 1, "legacyNoReseed": False, "csv": csv_path, "objectMode": "", "objectIndex": -1, "objectStratify": "heldout", "objectPose": pose}
    c.update(TS.harness_fields(theta)); return c


def run(model):
    os.makedirs(os.path.join(REPO, OUT), exist_ok=True); S = theta_set()
    json.dump({k: {kk: vv for kk, vv in v.items()} for k, v in S.items()}, open(os.path.join(REPO, OUT, "theta_set.json"), "w"), indent=1)
    log = json.load(open(PLOG)) if os.path.exists(PLOG) else []; done = {e["tag"] for e in log if e.get("ok")}
    run_pass(throwaway_cfg(model, f"{OUT}/throwaway_{time.strftime('%H%M%S')}.csv"), max_wait=600)
    for name, v in S.items():
        for pose in POSES:
            tag = f"{name}_{pose}_{SEEDS[0]}_{SEEDS[1]}"
            if tag in done: continue
            c = cfg(v["theta"], model, pose, f"{OUT}/{tag}.csv")
            for attempt in (1, 2):
                try: path, wall = run_pass(c, max_wait=3600); break
                except Exception as ex:
                    p = os.path.join(REPO, c["csv"])
                    if os.path.exists(p) and open(p).read().rstrip().endswith("DONE"): path, wall = p, -1.0; break
                    log.append({"tag": tag, "ok": False, "error": repr(ex)[:300]}); json.dump(log, open(PLOG, "w"), indent=1)
                    if attempt == 2: raise
            s = summarize(path)
            log.append({"tag": tag, "ok": True, "csv": os.path.relpath(path, REPO).replace("\\", "/"), "n": s["n"], "success": s["success"], "hash": pass_hash(path), "wall_s": wall, "time": time.strftime("%H:%M:%S")})
            json.dump(log, open(PLOG, "w"), indent=1); print(f"{tag}: n {s['n']} success {s['success']:.3f} [{s['lo']:.3f}, {s['hi']:.3f}] ({wall:.0f} s)", flush=True)


def paired(a, b, ks):
    d = [a[k]["success"] - b[k]["success"] for k in ks]; m = st.mean(d); se = st.pstdev(d) / math.sqrt(len(d)); return f"{m:+.3f} [{m - 1.96 * se:+.3f}, {m + 1.96 * se:+.3f}]"


def table(out="results/bo/partC/CONFIRM.md"):
    S = json.load(open(os.path.join(REPO, OUT, "theta_set.json"))); md = ["# Part C confirmation (held-out shapes 65-80, seeds 11501-11800, n = 300 per presentation)", ""]
    R = {(n, p): load(os.path.join(OUT, f"{n}_{p}_{SEEDS[0]}_{SEEDS[1]}.csv")) for n in S for p in POSES if os.path.exists(os.path.join(REPO, OUT, f"{n}_{p}_{SEEDS[0]}_{SEEDS[1]}.csv"))}
    md += ["| theta | source | canonical | random stable | both |", "|---|---|---|---|---|"]
    for n, v in S.items():
        c, r = R.get((n, "canonical")), R.get((n, "randomStable"))
        md.append(f"| {n} | {v['source']} | {W([x['success'] for x in c.values()]) if c else '-'} | {W([x['success'] for x in r.values()]) if r else '-'} | {W([x['success'] for x in list(c.values()) + list(r.values())]) if c and r else '-'} |")
    rb = max((n for n in S if n.startswith("random")), key=lambda n: sum(x["success"] for p in POSES for x in R.get((n, p), {}).values()), default=None)   # best random theta ON THE CONFIRMATION SET is reported too
    md += ["", "Paired differences (same seed and shape rows; 95 % CI from the paired SD):", "", "| comparison | canonical | random stable | both |", "|---|---|---|---|"]
    for lab, a, b in (("BO-best (bo1) − reference", "bo1", "reference"), ("BO-best (bo1) − random-best (random1, by campaign J)", "bo1", "random1"),
                      (f"BO-best (bo1) − best random on the confirmation set ({rb})", "bo1", rb), ("bo1 thumb base OFF − bo1", "bo1_thumbBaseOff", "bo1"), ("random1 − reference", "random1", "reference")):
        cells = []
        for p in POSES:
            A, B = R.get((a, p)), R.get((b, p))
            cells.append(paired(A, B, sorted(set(A) & set(B))) if A and B else "-")
        A = {(p,) + k: v for p in POSES for k, v in (R.get((a, p)) or {}).items()}; B = {(p,) + k: v for p in POSES for k, v in (R.get((b, p)) or {}).items()}
        cells.append(paired(A, B, sorted(set(A) & set(B))) if A and B and len(set(A) & set(B)) > 1 else "-")
        md.append(f"| {lab} | " + " | ".join(cells) + " |")
    open(os.path.join(REPO, out), "w", encoding="utf-8").write("\n".join(md) + "\n"); sys.stdout.buffer.write(("\n".join(md) + "\n").encode("utf-8"))


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("cmd"); ap.add_argument("--model"); a = ap.parse_args()
    run(a.model) if a.cmd == "run" else table()
