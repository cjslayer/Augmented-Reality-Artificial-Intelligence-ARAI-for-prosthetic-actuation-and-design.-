"""Run 013 prep: Editor passes for the anchor regression, the zero-shot bank evaluations, the hand x shape grid and the wrist pass.

Every pass goes through tools/bo/gates_runner.run_pass (the Editor harness ArticulatedGates mode eval) with the instrument settings:
deterministic head, one job worker, per-episode reseed DerivedSeed(seed, passIndex), throwaway pass after every Editor launch. The
object bank is driven with the harness config fields objectMode / objectIndex / objectStratify (Diagnostics plumbing).
Resumable: results/013_prep/passes.json logs every completed pass; a pass with an existing log entry is skipped.

usage: python -m tools.objects.run_zero_shot <stage> [--dry]
  stages: throwaway | anchor | train | heldout | grid | wrist | all
"""
import csv, json, os, sys, time
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); sys.path.insert(0, REPO)
from tools.bo.gates_runner import run_pass, pass_hash
from tools.bo import theta_space as TS
from tools.eval.summarize_pass import summarize

OUT = "results/013_prep/passes"; LOG = os.path.join(REPO, "results/013_prep/passes.json"); MODEL = "results/012/Prosthetic.onnx"
os.makedirs(os.path.join(REPO, OUT), exist_ok=True)
log = json.load(open(LOG)) if os.path.exists(LOG) else []
done = {e["tag"] for e in log if e.get("ok")}


def base_config(seeds, csv_path, perturb=1.0, mu=1.0, hold=50, pass_index=0, mass_range=None, theta=None, obj_mode="", obj_index=-1, stratify=""):
    cfg = {"mode": "eval", "modelPath": MODEL, "seedFrom": int(seeds[0]), "seedTo": int(seeds[1]), "objectFriction": float(mu), "evalPerturbScale": float(perturb),
           "evalHoldDecisions": int(hold), "timeScale": 20, "maxTrialSteps": 6000, "headMode": "deterministic", "passIndex": int(pass_index), "jobWorkers": 1, "legacyNoReseed": False, "csv": csv_path,
           "objectMode": obj_mode, "objectIndex": int(obj_index), "objectStratify": stratify}
    if mass_range: cfg["massMin"], cfg["massMax"] = float(mass_range[0]), float(mass_range[1])
    if theta is not None: cfg.update(TS.harness_fields(theta))
    return cfg


def save(e): log.append(e); json.dump(log, open(LOG, "w"), indent=1)


def run(tag, cfg, dry=False, max_wait=3600):
    if tag in done: print("skip (done)", tag); return
    if dry: print("DRY", tag, json.dumps({k: v for k, v in cfg.items() if k.startswith(("object", "seed", "pass", "evalP", "mass", "csv"))})); return
    t0 = time.time()
    try:
        path, wall = run_pass(cfg, max_wait=max_wait)
    except Exception as ex:
        save({"tag": tag, "ok": False, "error": repr(ex)[:400], "time": time.strftime("%H:%M:%S")}); print(tag, "FAILED", repr(ex)[:300], flush=True); raise
    s = summarize(path)
    e = {"tag": tag, "ok": True, "csv": os.path.relpath(path, REPO).replace("\\", "/"), "n": s["n"], "success": s["success"], "lo": s["lo"], "hi": s["hi"], "reach_failures": s["reach_failures"],
         "lifted": s["lifted"], "palm": s["palm"], "contacts": s["contacts"], "hold": s["meanHold"], "ends": {k: int(v) for k, v in s["ends"].items()}, "hash": pass_hash(path), "wall_s": wall, "time": time.strftime("%H:%M:%S"),
         "cfg": {k: v for k, v in cfg.items() if not k.startswith("theta")}}
    save(e); done.add(tag)
    print(f"{tag}: n {s['n']} success {s['success']:.3f} [{s['lo']:.3f}, {s['hi']:.3f}] reach_fail {s['reach_failures']} lifted {s['lifted']:.3f} palm {s['palm']:.3f} hash {e['hash']} ({wall:.0f} s)", flush=True)


def probe_theta(i): return json.load(open(os.path.join(REPO, "results/bo/partB/probe_thetas.json")))[i]


def wrist_theta():
    """hand 14 with k_wristPron moved to the training-distribution median (the unit-box median of 2,000 draws, i.e. the geometric mean of the wrist range)"""
    import numpy as np
    t = json.loads(json.dumps(probe_theta(14)))
    UT = np.array([TS.to_unit(x) for x in TS.sample_training_distribution(2000, 1)]); med = np.median(UT, 0)
    d = TS.DIM_NAMES.index("k_wristPron"); u = TS.to_unit(t); u[d] = med[d]; t["k"][15] = TS.from_unit(u)["k"][15]
    return t


def stage(name, dry=False):
    if name in ("throwaway", "all"):
        run("throwaway_" + time.strftime("%H%M%S"), base_config((6091, 6100), f"{OUT}/throwaway.csv"), dry, max_wait=600)
    if name in ("anchor", "all"):
        run("anchor_default_7001_7300", base_config((7001, 7300), f"{OUT}/anchor_default_7001_7300.csv"), dry)                                       # runtime default (anchorOnly), no object columns: byte-identical target
        run("anchor_fixed0_7001_7300", base_config((7001, 7300), f"{OUT}/anchor_fixed0_7001_7300.csv", obj_mode="fixedIndex", obj_index=0), dry)     # the anchor through the swap path
    if name in ("train", "all"):
        for p in range(3): run(f"train_8001_8300_p{p}", base_config((8001, 8300), f"{OUT}/train_8001_8300_p{p}.csv", pass_index=p, stratify="train"), dry)
    if name in ("heldout", "all"):
        for p in range(2): run(f"heldout_8001_8300_p{p}", base_config((8001, 8300), f"{OUT}/heldout_8001_8300_p{p}.csv", pass_index=p, stratify="heldout"), dry)
    if name in ("grid", "all"):
        for h in (5, 11, 14, 1, 2, 3):
            for p in range(4): run(f"grid_h{h:02d}_8301_8400_p{p}", base_config((8301, 8400), f"{OUT}/grid_h{h:02d}_8301_8400_p{p}.csv", pass_index=p, stratify="all", theta=probe_theta(h)), dry)
    if name in ("wrist", "all"):
        t = wrist_theta(); json.dump(t, open(os.path.join(REPO, "results/013_prep/theta_h14_wristPron_median.json"), "w"))
        run("wrist_h14_kWristPron_median_4001_4100", base_config((4001, 4100), f"{OUT}/wrist_h14_kWristPron_median_4001_4100.csv", perturb=2.0, mass_range=(0.4, 3.0), theta=t), dry)


if __name__ == "__main__":
    a = [x for x in sys.argv[1:] if not x.startswith("--")]; dry = "--dry" in sys.argv
    for s in (a or ["all"]): stage(s, dry)
