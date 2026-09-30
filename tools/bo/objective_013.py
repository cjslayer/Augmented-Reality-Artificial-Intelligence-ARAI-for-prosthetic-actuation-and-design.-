"""BO objective J(theta) on the run-013 substrate (brief 2b).

J = mean success over 128 episodes: the 32 even train shapes 2, 4, ..., 64 x both pose modes x 2 episodes; seeds 11001-11128
(canonical 11001-11064, randomStable 11065-11128), perturbation 1.0, K = 50, mu 1.0, mass as training (the agent's serialized
log-uniform range: no massMin / massMax), deterministic head, one job worker, per-episode reseed, passIndex 0, theta pinned for every
episode (thetaFixed). One evaluation = one realization of these 128 episodes; it takes two harness plays (one per pose mode: the
harness sets the pose mode per play). Shape per episode: objectStratify "train" gives 1 + ordinal mod 64 and the Editor-only harness
key objectIndexStride 2 (ObjectBank.cs) maps it to 2 (i mod 32) + 2, so ordinals 0-63 of each play cover every even shape twice.
Zero-step rows follow tools/eval/summarize_pass.py (first-episode warm-up abort excluded; other zero-step rows = reach failures = 0).

J(theta, model, tag, out_dir) -> dict: J, n, per mode (success, n), per family (length bin x pose mode), reach failures, hashes, wall.
A failed play is retried once; a second failure raises (stop condition "a pass fails twice").
"""
import json, os, sys, time
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_space as TS
from tools.bo.gates_runner import run_pass, pass_hash, REPO
from tools.eval.summarize_pass import load, warmup_abort, reach_failure
from tools.eval.grid_tables import fam

SEEDS = {"canonical": (11001, 11064), "randomStable": (11065, 11128)}
SHAPES = list(range(2, 65, 2))


class PassFailedTwice(RuntimeError):
    pass


def config(theta, model, pose, csv, perturb=1.0):
    s0, s1 = SEEDS[pose]
    c = {"mode": "eval", "modelPath": model, "seedFrom": s0, "seedTo": s1, "objectFriction": 1.0, "evalPerturbScale": float(perturb), "evalHoldDecisions": 50, "timeScale": 20,
         "maxTrialSteps": 6000, "headMode": "deterministic", "passIndex": 0, "jobWorkers": 1, "legacyNoReseed": False, "csv": csv,
         "objectMode": "", "objectIndex": -1, "objectStratify": "train", "objectPose": pose, "objectIndexStride": 2}
    c.update(TS.harness_fields(theta)); return c


def _play(cfg, max_wait):
    for attempt in (1, 2):
        try: return run_pass(cfg, max_wait=max_wait)
        except Exception as ex:
            p = os.path.join(REPO, cfg["csv"])
            if os.path.exists(p) and open(p).read().rstrip().endswith("DONE"): return p, -1.0   # runner race: the CSV is complete
            print(f"play {cfg['csv']} failed (attempt {attempt}): {repr(ex)[:300]}", flush=True)
            if attempt == 2: raise PassFailedTwice(f"{cfg['csv']}: {ex!r}"[:500])
            time.sleep(10)


def rows_of(path):
    out = []
    for r in load(path):
        if warmup_abort(r): continue
        out.append({"seed": int(r["seed"]), "idx": int(r["objIndex"]), "success": int(r["success"]), "reach": int(reach_failure(r)), "lifted": int(int(r["stepsToLift"]) >= 0),
                    "gate": int(int(r["transitionStep"]) >= 0), "end": r["endReason"], "row": r})
    return out


def J(theta, model, tag, out_dir, perturb=1.0, max_wait=1800):
    t0 = time.time(); res = {"tag": tag, "model": model, "per_mode": {}, "per_family": {}, "hash": {}, "wall_play": {}}
    allrows = []; exp = TS.summary_features(theta)
    for pose in ("canonical", "randomStable"):
        csv = f"{out_dir}/{tag}_{pose}.csv"; path, wall = _play(config(theta, model, pose, csv, perturb), max_wait)
        rows = rows_of(path); res["hash"][pose] = pass_hash(path); res["wall_play"][pose] = round(wall, 1)
        bad = [r["idx"] for r in rows if r["idx"] not in SHAPES]
        if bad: raise RuntimeError(f"{csv}: shapes outside the even train set: {sorted(set(bad))[:5]} (is objectIndexStride compiled in?)")
        for key in ("kFingerMean", "zetaMean", "inertiaScaleMean", "lenMean"):
            if rows and abs(float(rows[0]["row"][key]) - exp[key]) > 0.01: raise RuntimeError(f"theta cross-check failed on {key}: logged {rows[0]['row'][key]}, expected {exp[key]:.3f}")
        for r in rows: r["pose"] = pose
        res["per_mode"][pose] = {"success": sum(r["success"] for r in rows) / max(1, len(rows)), "n": len(rows), "reach": sum(r["reach"] for r in rows), "gate": sum(r["gate"] for r in rows) / max(1, len(rows)),
                                 "lifted": sum(r["lifted"] for r in rows) / max(1, len(rows))}
        allrows += rows
    fam_acc = defaultdict(list)
    for r in allrows: fam_acc[f"{fam(r['idx'])['lenbin']} | {r['pose']}"].append(r["success"])
    res["per_family"] = {k: {"success": sum(v) / len(v), "n": len(v)} for k, v in sorted(fam_acc.items())}
    res["n"] = len(allrows); res["J"] = sum(r["success"] for r in allrows) / max(1, len(allrows)); res["reach"] = sum(r["reach"] for r in allrows); res["wall_s"] = round(time.time() - t0, 1)
    res["shape_counts_ok"] = all(sum(1 for r in allrows if r["idx"] == s) == 4 for s in SHAPES) if len(allrows) == 128 else False
    return res


if __name__ == "__main__":
    import argparse
    from tools.bo import theta_reduced as TR
    ap = argparse.ArgumentParser(); ap.add_argument("--model", required=True); ap.add_argument("--tag", default="reference"); ap.add_argument("--out", default="results/bo/partC/passes"); a = ap.parse_args()
    print(json.dumps(J(TR.to_theta(TR.reference_u()), a.model, a.tag, a.out), indent=1))
