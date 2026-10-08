"""Run 014 Part 4b evaluation driver (CJ 2026-10-06). Editor harness, nothing training. Every pass: mode eval, deterministic head unless stated,
one job worker, per-episode reseed DerivedSeed(seed, passIndex 0), K = 50, mu 1.0, randomized theta per episode, spawnStream (the object and spawn
draws are reseeded from the seed right before they happen, so placements pair across layouts and compositions).

Seed blocks (fresh; 12001-12002 went to the layout check and 12101-12400 to spawn-only draws at checkpoint 4):
  held-out shapes 65-80 (stratified): 12401-12700, used by every held-out cell (blocks 1, 2, 4) so all cells pair by seed;
  anchor cylinder: 7001-7300.

Stages:
  contain     the 5-digit hand inside the envelope: spawnCheck + containment grid 2 mm, 300 seeded resets, randomized theta,
              (a) legacy layout with 013E and the envelope forced (013E's own colliders), (b) slot layout, digits 5, with 014
  pairing     spawnCheck, both poses: 013E legacy + forced envelope vs 014 slot 5-digit; placements must be identical row by row
  block1      014, compositions 3/4/5/6a/6b/7 x pose, deterministic and sampled, lift budget 300
  block2      013E 5-digit legacy + forced envelope, both poses, deterministic, lift budget 300 and 200; 014 5-digit at 200 (deterministic)
  block3      anchor 7001-7300, 5-digit: 014 and 013E (forced envelope), deterministic, lift budget 300 and 200
  block4      pulse test: 014 lying, all compositions, perturbation 0 (deterministic), the perturbation-1.0 pair is block 1

usage: python -m tools.eval.eval_014 STAGE [STAGE ...]   (results/014/eval/, log results/014/eval/passes.json)
"""
import json, os, sys, time
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); sys.path.insert(0, REPO)
from tools.objects import run_zero_shot as Z
from tools.bo.gates_runner import run_pass, pass_hash
OUT = "results/014/eval"; LOG = os.path.join(REPO, OUT, "passes.json")
M014, M013E = "results/014/Prosthetic.onnx", "results/013/013E/Prosthetic.onnx"
HELD = (12401, 12700); ANCHOR = (7001, 7300)
COMPS = [("3", 3, False), ("4", 4, False), ("5", 5, False), ("6a", 6, False), ("6b", 6, True), ("7", 7, False)]
log = json.load(open(LOG)) if os.path.exists(LOG) else []; done = {e["tag"] for e in log if e.get("ok")}


def cfg(model, seeds, csv, pose="canonical", head="deterministic", lb=300, perturb=1.0, digits=None, legacy=False, anchor=False, **kw):
    c = Z.base_config(seeds, csv, perturb=perturb, obj_mode="anchorOnly" if anchor else "", stratify="" if anchor else "heldout")
    c.update({"modelPath": model, "objectPose": pose, "headMode": head, "liftBudgetSteps": lb, "spawnStream": True})
    if legacy: c["forceSpawnEnvelope"] = True
    else: c.update({"topology": "slots", "digits": digits[1], "digits6b": digits[2]})
    c.update(kw); return c


def run(tag, c, max_wait=5400):
    if tag in done: print("skip (done)", tag, flush=True); return
    t0 = time.time()
    try: path, wall = run_pass(c, max_wait=max_wait)
    except Exception as ex:
        p = os.path.join(REPO, c["csv"])
        if os.path.exists(p) and open(p).read().rstrip().endswith("DONE"): path, wall = p, time.time() - t0
        else: log.append({"tag": tag, "ok": False, "error": repr(ex)[:400], "time": time.strftime("%F %T")}); json.dump(log, open(LOG, "w"), indent=1); print(tag, "FAILED", repr(ex)[:300], flush=True); raise
    log.append({"tag": tag, "ok": True, "csv": c["csv"], "hash": pass_hash(path), "wall_s": round(wall), "time": time.strftime("%F %T"), "cfg": c}); done.add(tag)
    json.dump(log, open(LOG, "w"), indent=1); print(f"{tag}: {wall:.0f} s", flush=True)


def stage(name):
    C5 = COMPS[2]
    if name == "contain":
        run("contain_legacy013E", cfg(M013E, HELD, f"{OUT}/contain_legacy013E.csv", legacy=True, spawnCheck=True, containmentMm=2.0, timeScale=1))
        run("contain_slots5_014", cfg(M014, HELD, f"{OUT}/contain_slots5_014.csv", digits=C5, spawnCheck=True, containmentMm=2.0, timeScale=1))
    if name == "pairing":
        for pose in ("canonical", "randomStable"):
            run(f"pair_{pose}_013E", cfg(M013E, HELD, f"{OUT}/pair_{pose}_013E.csv", pose=pose, legacy=True, spawnCheck=True, timeScale=1))
            for n, d, b in COMPS: run(f"pair_{pose}_014_h{n}", cfg(M014, HELD, f"{OUT}/pair_{pose}_014_h{n}.csv", pose=pose, digits=(n, d, b), spawnCheck=True, timeScale=1))
        run("pair_anchor_013E", cfg(M013E, ANCHOR, f"{OUT}/pair_anchor_013E.csv", legacy=True, anchor=True, spawnCheck=True, timeScale=1))
        run("pair_anchor_014_h5", cfg(M014, ANCHOR, f"{OUT}/pair_anchor_014_h5.csv", digits=C5, anchor=True, spawnCheck=True, timeScale=1))
    if name == "block1":
        for head in ("deterministic", "sampled"):
            for pose in ("canonical", "randomStable"):
                for n, d, b in COMPS:
                    t = f"b1_014_h{n}_{pose}_{head[:3]}_lb300"; run(t, cfg(M014, HELD, f"{OUT}/{t}.csv", pose=pose, head=head, digits=(n, d, b)))
    if name == "block2":
        for lb in (300, 200):
            for pose in ("canonical", "randomStable"):
                t = f"b2_013E_h5_{pose}_det_lb{lb}"; run(t, cfg(M013E, HELD, f"{OUT}/{t}.csv", pose=pose, lb=lb, legacy=True))
        for pose in ("canonical", "randomStable"):
            t = f"b1_014_h5_{pose}_det_lb200"; run(t, cfg(M014, HELD, f"{OUT}/{t}.csv", pose=pose, lb=200, digits=C5))
        for pose in ("canonical", "randomStable"):   # sensitivity: 013E on its own-hand filter (no forced envelope), for the matched-rows pairing
            t = f"b2_013E_h5_{pose}_det_lb300_ownfilter"; c = cfg(M013E, HELD, f"{OUT}/{t}.csv", pose=pose, legacy=True); c.pop("forceSpawnEnvelope"); run(t, c)
    if name == "block3":
        for lb in (300, 200):
            t = f"b3_anchor_014_h5_det_lb{lb}"; run(t, cfg(M014, ANCHOR, f"{OUT}/{t}.csv", lb=lb, digits=C5, anchor=True))
            t = f"b3_anchor_013E_h5_det_lb{lb}"; run(t, cfg(M013E, ANCHOR, f"{OUT}/{t}.csv", lb=lb, legacy=True, anchor=True))
        t = "b3_anchor_013E_h5_det_lb300_ownfilter"; c = cfg(M013E, ANCHOR, f"{OUT}/{t}.csv", legacy=True, anchor=True); c.pop("forceSpawnEnvelope"); run(t, c)
    if name == "block4":
        for n, d, b in COMPS:
            t = f"b4_014_h{n}_randomStable_det_lb300_perturb0"; run(t, cfg(M014, HELD, f"{OUT}/{t}.csv", pose="randomStable", perturb=0.0, digits=(n, d, b)))


if __name__ == "__main__":
    os.makedirs(os.path.join(REPO, OUT), exist_ok=True)
    for s in sys.argv[1:]: stage(s)
