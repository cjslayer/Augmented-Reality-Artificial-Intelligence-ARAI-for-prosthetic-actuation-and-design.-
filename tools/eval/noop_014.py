"""Run 014 no-op proof (checkpoint-1 decision 6b): re-run recorded 013E fixed-seed passes with the default (legacy) layout and compare them
pass-level with the recorded passes (results/013/eval/013E/, 2026-09-29): hash, success, and per-episode agreement of (success, endReason, steps).

  python -m tools.eval.noop_014 run --out results/014/noop_head      (throwaway, then the passes)
  python -m tools.eval.noop_014 compare --out results/014/noop_head
"""
import argparse, csv, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo.gates_runner import run_pass, pass_hash, REPO
from tools.eval.summarize_pass import summarize

MODEL = "results/013/013E/Prosthetic.onnx"; REC = "results/013/eval/013E"
PASSES = {"anchor_canonical_7001_7300": dict(seeds=(7001, 7300), pose="canonical", obj_mode="", stratify=""),
          "heldout_randomStable_10001_10300_p0": dict(seeds=(10001, 10300), pose="randomStable", obj_mode="", stratify="heldout")}


def cfg(seeds, csv_path, pose, obj_mode, stratify, extra):
    c = {"mode": "eval", "modelPath": MODEL, "seedFrom": seeds[0], "seedTo": seeds[1], "objectFriction": 1.0, "evalPerturbScale": 1.0, "evalHoldDecisions": 50, "timeScale": 20,
         "maxTrialSteps": 6000, "headMode": "deterministic", "passIndex": 0, "jobWorkers": 1, "legacyNoReseed": False, "csv": csv_path,
         "objectMode": obj_mode, "objectIndex": -1, "objectStratify": stratify, "objectPose": pose}
    c.update(extra or {}); return c


def run(out, extra):
    os.makedirs(os.path.join(REPO, out), exist_ok=True); log = os.path.join(REPO, out, "passes.json"); L = json.load(open(log)) if os.path.exists(log) else []
    ts = time.strftime("%H%M%S"); run_pass(cfg((6091, 6100), f"{out}/throwaway_{ts}.csv", "canonical", "", "", extra), max_wait=600)
    for tag, p in PASSES.items():
        path, wall = run_pass(cfg(p["seeds"], f"{out}/{tag}.csv", p["pose"], p["obj_mode"], p["stratify"], extra), max_wait=3600); s = summarize(path)
        L.append({"tag": tag, "csv": f"{out}/{tag}.csv", "hash": pass_hash(path), "success": s["success"], "n": s["n"], "wall_s": round(wall, 1), "extra": extra, "time": time.strftime("%H:%M:%S")})
        json.dump(L, open(log, "w"), indent=1); print(f"{tag}: success {s['success']:.3f} n {s['n']} hash {pass_hash(path)} ({wall:.0f} s)", flush=True)


def rows(p): return {r["episode"]: r for r in csv.DictReader(open(os.path.join(REPO, p), newline="")) if (r.get("seed") or "").isdigit()}


def compare(out):
    md = [f"# No-op proof: {out} vs recorded 013E passes ({REC})", "", "| pass | recorded hash | this hash | recorded success | this success | episodes agreeing (success, end, steps) | first difference |", "|---|---|---|---|---|---|---|"]
    for tag in PASSES:
        a, b = os.path.join(REC, tag + ".csv"), os.path.join(out, tag + ".csv")
        if not os.path.exists(os.path.join(REPO, b)): continue
        A, B = rows(a), rows(b); ks = sorted(set(A) & set(B), key=int)
        agree = [k for k in ks if (A[k]["success"], A[k]["endReason"], A[k]["steps"]) == (B[k]["success"], B[k]["endReason"], B[k]["steps"])]
        first = next((k for k in ks if A[k] != B[k]), "-")
        md.append(f"| {tag} | {pass_hash(os.path.join(REPO, a))} | {pass_hash(os.path.join(REPO, b))} | {summarize(os.path.join(REPO, a))['success']:.3f} | {summarize(os.path.join(REPO, b))['success']:.3f} | {len(agree)}/{len(ks)} | episode {first} |")
    s = "\n".join(md) + "\n"; open(os.path.join(REPO, out, "NOOP.md"), "w", encoding="utf-8").write(s); sys.stdout.buffer.write(s.encode("utf-8"))


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("cmd"); ap.add_argument("--out", required=True); ap.add_argument("--extra", default="{}"); a = ap.parse_args()
    run(a.out, json.loads(a.extra)) if a.cmd == "run" else compare(a.out)
