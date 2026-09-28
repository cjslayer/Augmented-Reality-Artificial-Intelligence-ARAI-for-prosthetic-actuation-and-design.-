"""Editor eval-harness pass driver (moved from the session scratchpad of 2026-09-27, run_v2.py; the stages and their configs are unchanged).

Every pass: tools/bo/gates_runner.run_pass on the open Editor (ArticulatedGates mode eval), deterministic head, one job worker, per-episode reseed
DerivedSeed(seed, passIndex), K = 50, mu 1.0, perturbation 1.0 unless a stage says otherwise, object pose by objectPose / poseCanonicalFraction.
A pass whose runner poll lost the race with the harness's own stop but whose CSV ends with DONE is registered from the CSV.
Log: one JSON entry per pass (summary via tools/eval/summarize_pass.py, data-row hash via gates_runner.pass_hash, the pass config).

usage: python -m tools.eval.run_passes STAGE [STAGE ...] [--model PATH] [--out DIR] [--log FILE] [--prefix TAG]
stages (v2 set, defaults = results/013_prep/passes_v2 + passes_v2.json, model results/012/Prosthetic.onnx):
  throwaway repro_first repro_later repro_rs anchor zs grid h14combo
stages added for run 013:
  grid_canonical   hands 5/11/14/1/2/3, 80 shapes x 5 episodes (stratify all), seeds 9301-9400, passIndex 0-3, canonical
  grid_rs          same, randomStable (the v2 'grid' stage with the prefix / out / model options)
  eval_heldout     held-out shapes 65-80, seeds 10001-10300, passIndex 0-1, canonical and randomStable (paired)
  eval_train       train shapes 1-64, seeds 10001-10300, passIndex 0-2, canonical and randomStable (paired)
  anchor_canonical anchor cylinder, canonical, seeds 7001-7300
  frac_smoke       train shapes, seeds 6001-6100, stratified, pose mixed by --canonical-fraction (0b check)
  range_probe      20 probe theta (results/bo/partB/probe_thetas.json), held-out shapes, randomStable, seeds 10001-10100 (n = 100 each); --perturb for the level
"""
import argparse, copy, json, os, sys, time
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); sys.path.insert(0, REPO)
from tools.objects import run_zero_shot as Z
from tools.bo.gates_runner import run_pass, pass_hash
from tools.eval.summarize_pass import summarize
from tools.bo import theta_space as TS

A = None; log = []; done = set(); OUT = LOG = MODEL = PREFIX = None


def cfg(seeds, csv, pose="canonical", obj_mode="anchorOnly", **kw):
    c = Z.base_config(seeds, csv, obj_mode=obj_mode, **kw); c["objectPose"] = pose; c["modelPath"] = MODEL
    if A is not None and A.canonical_fraction is not None: c["poseCanonicalFraction"] = float(A.canonical_fraction)   # read by ObjectBank (Editor) from Temp/gates.json
    return c


def save(e):
    log.append(e); json.dump(log, open(LOG, "w"), indent=1)


def run(tag, c, max_wait=3600, allow_repeat=False):
    tag = PREFIX + tag
    if tag in done and not allow_repeat: print("skip (done)", tag); return
    try: path, wall = run_pass(c, max_wait=max_wait)
    except Exception as ex:
        p = os.path.join(REPO, c["csv"])
        if os.path.exists(p) and open(p).read().rstrip().endswith("DONE"): path, wall = p, -1.0; print(tag, "runner race (CSV complete); registering", flush=True)
        else: save({"tag": tag, "ok": False, "error": repr(ex)[:400], "time": time.strftime("%H:%M:%S")}); print(tag, "FAILED", repr(ex)[:300], flush=True); raise
    s = summarize(path)
    e = {"tag": tag, "ok": True, "csv": os.path.relpath(path, REPO).replace("\\", "/"), "n": s["n"], "success": s["success"], "lo": s["lo"], "hi": s["hi"], "reach_failures": s["reach_failures"], "lifted": s["lifted"], "palm": s["palm"],
         "contacts": s["contacts"], "hold": s["meanHold"], "ends": {k: int(v) for k, v in s["ends"].items()}, "hash": pass_hash(path), "wall_s": wall, "time": time.strftime("%H:%M:%S"), "cfg": {k: v for k, v in c.items() if not k.startswith("theta")}}
    save(e); done.add(tag); print(f"{tag}: n {s['n']} success {s['success']:.3f} [{s['lo']:.3f}, {s['hi']:.3f}] reach_fail {s['reach_failures']} lifted {s['lifted']:.3f} palm {s['palm']:.3f} hash {e['hash']} ({wall:.0f} s)", flush=True)


def h14_variants():
    import numpy as np
    th = json.load(open(os.path.join(REPO, "results/bo/partB/probe_thetas.json"))); t = copy.deepcopy(th[14])
    UT = np.array([TS.to_unit(x) for x in TS.sample_training_distribution(2000, 1)]); med = np.median(UT, 0)
    combo = copy.deepcopy(t); combo["mask"][12] = 1; u = TS.to_unit(t); d = TS.DIM_NAMES.index("k_wristPron"); u[d] = med[d]; combo["k"][15] = TS.from_unit(u)["k"][15]
    return combo


def grid(pose, name):
    for h in (5, 11, 14, 1, 2, 3):
        for p in range(4): run(f"{name}_h{h:02d}_9301_9400_p{p}", cfg((9301, 9400), f"{OUT}/{name}_h{h:02d}_9301_9400_p{p}.csv", pose=pose, obj_mode="", pass_index=p, stratify="all", theta=Z.probe_theta(h)))


def stage(name):
    R = (6001, 6100); T = (6091, 6100); ts = time.strftime("%H%M%S")
    if name == "throwaway": run("throwaway_" + ts, cfg(T, f"{OUT}/throwaway_{ts}.csv"), max_wait=600, allow_repeat=True)
    if name == "repro_first": run("repro_first_" + ts, cfg(R, f"{OUT}/repro_first_{ts}.csv"), allow_repeat=True)
    if name == "repro_later": run("repro_later_" + ts, cfg(R, f"{OUT}/repro_later_{ts}.csv"), allow_repeat=True)
    if name == "repro_rs": run("repro_rs_" + ts, cfg(R, f"{OUT}/repro_rs_{ts}.csv", pose="randomStable"), allow_repeat=True)
    if name == "anchor": run("anchor_canonical_7001_7300", cfg((7001, 7300), f"{OUT}/anchor_canonical_7001_7300.csv"))
    if name == "zs":
        for pose in ("canonical", "randomStable"):
            for p in range(3): run(f"train_{pose}_9001_9300_p{p}", cfg((9001, 9300), f"{OUT}/train_{pose}_9001_9300_p{p}.csv", pose=pose, obj_mode="", pass_index=p, stratify="train"))
            for p in range(2): run(f"heldout_{pose}_9001_9300_p{p}", cfg((9001, 9300), f"{OUT}/heldout_{pose}_9001_9300_p{p}.csv", pose=pose, obj_mode="", pass_index=p, stratify="heldout"))
    if name == "grid": grid("randomStable", "grid_rs")
    if name == "h14combo":
        t = h14_variants(); json.dump(t, open(os.path.join(REPO, "results/013_prep/theta_h14_thumbBase_wristPron.json"), "w"))
        run("h14_thumbBase_wristPron_4001_4100", cfg((4001, 4100), f"{OUT}/h14_thumbBase_wristPron_4001_4100.csv", perturb=2.0, mass_range=(0.4, 3.0), theta=t))
    # ---- run 013 stages
    if name == "grid_canonical": grid("canonical", "grid_can")
    if name == "grid_rs": grid("randomStable", "grid_rs")
    if name == "anchor_canonical": run("anchor_canonical_7001_7300", cfg((7001, 7300), f"{OUT}/anchor_canonical_7001_7300.csv"))
    if name == "eval_heldout":
        for pose in ("canonical", "randomStable"):
            for p in range(2): run(f"heldout_{pose}_10001_10300_p{p}", cfg((10001, 10300), f"{OUT}/heldout_{pose}_10001_10300_p{p}.csv", pose=pose, obj_mode="", pass_index=p, stratify="heldout"))
    if name == "eval_train":
        for pose in ("canonical", "randomStable"):
            for p in range(3): run(f"train_{pose}_10001_10300_p{p}", cfg((10001, 10300), f"{OUT}/train_{pose}_10001_10300_p{p}.csv", pose=pose, obj_mode="", pass_index=p, stratify="train"))
    if name == "frac_smoke": run(f"frac_smoke_f{A.canonical_fraction}_6001_6100", cfg((6001, 6100), f"{OUT}/frac_smoke_f{A.canonical_fraction}_6001_6100.csv", obj_mode="", stratify="train"), allow_repeat=True)
    if name == "range_probe":
        th = json.load(open(os.path.join(REPO, "results/bo/partB/probe_thetas.json"))); lvl = f"s{A.perturb:.1f}"
        for i, t in enumerate(th): run(f"probe_t{i:02d}_{lvl}_heldout_rs_10001_10100", cfg((10001, 10100), f"{OUT}/probe_t{i:02d}_{lvl}_heldout_rs_10001_10100.csv", pose="randomStable", obj_mode="", stratify="heldout", theta=t, perturb=A.perturb))


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0]); ap.add_argument("stages", nargs="+")
    ap.add_argument("--model", default="results/012/Prosthetic.onnx"); ap.add_argument("--out", default="results/013_prep/passes_v2"); ap.add_argument("--log", default="results/013_prep/passes_v2.json")
    ap.add_argument("--prefix", default=""); ap.add_argument("--perturb", type=float, default=1.0); ap.add_argument("--canonical-fraction", type=float, default=None)
    A = ap.parse_args(); MODEL, OUT, PREFIX = A.model, A.out, A.prefix; LOG = os.path.join(REPO, A.log)
    os.makedirs(os.path.join(REPO, OUT), exist_ok=True)
    log = json.load(open(LOG)) if os.path.exists(LOG) else []; done = {e["tag"] for e in log if e.get("ok")}
    for s in A.stages: stage(s)
