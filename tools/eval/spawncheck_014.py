"""Run 014 eval-position check (CJ 2026-10-04, before training): the spawn positions the eval harness will use pass the envelope filter.

Every eval reset (ArticulatedGates mode eval with spawnCheck: seeded exactly as an eval episode, then ended before the policy acts) records the
spawn attempts, the attempt the envelope accepted (chosen; -1 = fallback, i.e. a position the filter never passed) and the attempt the episode's
own-hand filter (the pre-fix / legacy filter) would have accepted first (handFirst). handFirst == chosen means the eval position is the one the
own-hand filter picks too, i.e. identical to the 013E-style position on the 5-digit hand.

Passes: held-out shapes 65-80 (stratified), seeds 12101-12400, passIndex 0, canonical and randomStable, compositions 3/4/5/6a/6b/7; anchor cylinder
canonical seeds 7001-7300 on the 5-digit hand. Spawn draws only: no policy step, no success measured on these seeds.

usage: python -m tools.eval.spawncheck_014 run|table [--out results/014/spawncheck]
"""
import argparse, csv, glob, os, sys
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); sys.path.insert(0, REPO)
MODEL = "results/014_smoke/Prosthetic/Prosthetic-500046.onnx"   # any slot-shaped model: the policy never acts in spawnCheck
COMPS = [("3", 3, False), ("4", 4, False), ("5", 5, False), ("6a", 6, False), ("6b", 6, True), ("7", 7, False)]


def cfg(seeds, csv_path, pose, digits, six_b, obj_mode, stratify):
    from tools.objects import run_zero_shot as Z
    c = Z.base_config(seeds, csv_path, obj_mode=obj_mode, stratify=stratify); c["modelPath"] = MODEL
    c.update({"objectPose": pose, "topology": "slots", "digits": digits, "digits6b": six_b, "liftBudgetSteps": 300, "spawnCheck": True, "timeScale": 1})
    return c


def passes(out):
    for name, d, b in COMPS:
        for pose in ("canonical", "randomStable"):
            yield f"{out}/heldout_{pose}_h{name}_12101_12400.csv", cfg((12101, 12400), f"{out}/heldout_{pose}_h{name}_12101_12400.csv", pose, d, b, "", "heldout")
    yield f"{out}/anchor_canonical_h5_7001_7300.csv", cfg((7001, 7300), f"{out}/anchor_canonical_h5_7001_7300.csv", "canonical", 5, False, "anchorOnly", "")


COLS = "seed,ordinal,composition,objIndex,pose,tested,envelopeRejects,handRejects,handFirst,chosen,fallback,x,y,z".split(",")


def rows(p):   # positional: the eval mode writes its own (episode) header line into the file; the data rows carry COLS
    return [dict(zip(COLS, l)) for l in csv.reader(open(p, newline="")) if len(l) == len(COLS) and l[0].isdigit()]


def table(out):
    md = ["# Run 014 eval-position check (envelope filter)", "", "| pass | resets | fallback (filter never passed) | chosen passes envelope | own-hand filter picks the same attempt | shapes |", "|---|---|---|---|---|---|"]
    pos = {}
    for p in sorted(glob.glob(os.path.join(REPO, out, "*.csv"))):
        R = rows(p); n = len(R); fb = sum(int(r["fallback"]) for r in R); same = sum(1 for r in R if r["handFirst"] == r["chosen"])
        md.append(f"| {os.path.basename(p)[:-4]} | {n} | {fb} | {n - fb} / {n} | {same} / {n} | {len(set(r['objIndex'] for r in R))} |")
        pos[os.path.basename(p)] = [(r["seed"], r["x"], r["y"], r["z"]) for r in R]
    for pose in ("canonical", "randomStable"):
        ref = pos.get(f"heldout_{pose}_h5_12101_12400.csv")
        if ref is None: continue
        diff = {k: sum(1 for a, b in zip(ref, v) if a != b) for k, v in pos.items() if k.startswith(f"heldout_{pose}_")}
        md.append(f"\n{pose}: rows whose spawn position differs from the 5-digit pass, by composition: " + ", ".join(f"{k.split('_')[2]} {v}" for k, v in sorted(diff.items())))
    s = "\n".join(md) + "\n"; open(os.path.join(REPO, out, "SPAWNCHECK.md"), "w", encoding="utf-8").write(s); print(s)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("cmd"); ap.add_argument("--out", default="results/014/spawncheck"); a = ap.parse_args()
    if a.cmd == "run":
        from tools.bo.gates_runner import run_pass
        os.makedirs(os.path.join(REPO, a.out), exist_ok=True)
        for path, c in passes(a.out):
            if os.path.exists(os.path.join(REPO, path)) and open(os.path.join(REPO, path)).read().rstrip().endswith("DONE"): print("skip", path); continue
            p, wall = run_pass(c, max_wait=1800); print(path, f"{wall:.0f} s", flush=True)
    table(a.out)
