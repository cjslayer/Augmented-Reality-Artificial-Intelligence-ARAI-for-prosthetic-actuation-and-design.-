"""Fixed-theta evaluation endpoint: evaluate(theta, ...) -> dict (BO outer loop, Part A).

Runs the headless BoEval player (Builds/BoEval/BoEval.exe, built from the committed InferenceOnly scene with the
deployed run-010 model and Assets/Scripts/BoEval/BoEvalHarness.cs) on one job file, waits for done.json and summarizes
the per-episode CSVs. Ground-truth metric: the drop test (pass-vs-mu), same mechanics as
results/010/validation/reference_hand/. The reward-side quality score Q is a training shaping signal and is NOT reported.

Seeds: 1001-1100, 2001-2100 and 3001-3100 are spent on model decisions; 4001-4100 is the reserved BO evaluation block
(default seed_block = (4001, 4020)). Every candidate is evaluated on the same seeds (common random numbers).
"""
import csv
import hashlib
import json
import os
import random
import statistics
import subprocess
import time

from .theta import validate

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_PLAYER = os.path.join(REPO, "Builds", "BoEval", "BoEval.exe")
DEFAULT_ROOT = os.path.join(REPO, "results", "bo_eval", "runs")
RESERVED_SEED_BLOCK = (4001, 4100)
SPENT_SEED_BLOCKS = ((1001, 1100), (2001, 2100), (3001, 3100))
BOOTSTRAP_N = 4000


def bootstrap_ci(vals, n=BOOTSTRAP_N, seed=0):
    """Mean with a percentile bootstrap 95% CI; same scheme as refhand_summary.py / paired_mu.py (Random(0), 4000)."""
    vals = list(vals)
    if not vals:
        return {"mean": float("nan"), "lo": float("nan"), "hi": float("nan"), "n": 0}
    m = statistics.mean(vals)
    rng = random.Random(seed)
    bs = sorted(statistics.mean(rng.choices(vals, k=len(vals))) for _ in range(n))
    return {"mean": m, "lo": bs[int(0.025 * n)], "hi": bs[int(0.975 * n) - 1], "n": len(vals)}


def seeds_for(n_episodes, seed_block):
    a, b = int(seed_block[0]), int(seed_block[1])
    if b < a:
        raise ValueError("seed_block must be (first, last) with last >= first")
    if n_episodes > b - a + 1:
        raise ValueError(f"n_episodes={n_episodes} exceeds the seed block {seed_block} ({b - a + 1} seeds)")
    return list(range(a, a + n_episodes))


def theta_hash(theta):
    return hashlib.sha1(json.dumps(theta, sort_keys=True).encode()).hexdigest()[:10]


def run_player(job, player=None, timeout=3600.0, extra_args=()):
    """Writes job.json into job['outDir'], runs the player headless, returns the parsed done.json."""
    player = player or os.environ.get("BO_EVAL_PLAYER") or DEFAULT_PLAYER
    if not os.path.isfile(player):
        raise FileNotFoundError(f"BoEval player not found: {player} (build it with `unity command build --target StandaloneWindows64 --outputPath Builds/BoEval/BoEval.exe --confirm true`)")
    out = job["outDir"]
    os.makedirs(out, exist_ok=True)
    for name in ("done.json", "oracle.json", "episodes.csv", "drops.csv", "decisions.csv"):
        p = os.path.join(out, name)
        if os.path.exists(p):
            os.remove(p)
    job_path = os.path.join(out, "job.json")
    with open(job_path, "w") as f:
        json.dump(job, f, indent=1)
    log = os.path.join(out, "player.log")
    cmd = [player, "-batchmode", "-nographics", "-logFile", log, "-boEvalJob", job_path] + list(extra_args)
    t0 = time.time()
    proc = subprocess.Popen(cmd, cwd=os.path.dirname(player))
    try:
        proc.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        proc.kill()
        raise RuntimeError(f"BoEval player exceeded {timeout} s (log: {log})")
    done_path = os.path.join(out, "done.json")
    if not os.path.isfile(done_path):
        raise RuntimeError(f"BoEval player exited (code {proc.returncode}) without done.json (log: {log})")
    with open(done_path) as f:
        done = json.load(f)
    if done.get("error"):
        raise RuntimeError("BoEval harness error: " + done["error"])
    done["wallSec"] = time.time() - t0
    done["player"] = player
    return done


def read_csv(path):
    if not os.path.isfile(path):
        return []
    with open(path, newline="") as f:
        return list(csv.DictReader(f))


def summarize(out_dir, mu_levels):
    """Metrics from episodes.csv / drops.csv / oracle.json in out_dir."""
    eps = read_csv(os.path.join(out_dir, "episodes.csv"))
    drops = read_csv(os.path.join(out_dir, "drops.csv"))
    held = [r for r in eps if r["holdReached"] == "1"]
    passes = {}
    for r in drops:
        passes.setdefault((r["seed"], f"{float(r['mu']):.1f}"), []).append(int(r["pass"]))
    drop_pass = {}
    for mu in mu_levels:
        key = f"{float(mu):.1f}"
        given = [statistics.mean(passes[(r["seed"], key)]) for r in held if (r["seed"], key) in passes]
        uncond = [statistics.mean(passes[(r["seed"], key)]) if (r["seed"], key) in passes else 0.0 for r in eps]
        drop_pass[key] = {
            "given_hold": bootstrap_ci(given),
            "times_success": bootstrap_ci(uncond),
            "any_repeat": statistics.mean([1.0 if v > 0 else 0.0 for v in given]) if given else float("nan"),
            "all_repeats": statistics.mean([1.0 if v == 1 else 0.0 for v in given]) if given else float("nan"),
        }
    success = [int(r["holdReached"]) for r in eps]
    oracle_path = os.path.join(out_dir, "oracle.json")
    oracle = json.load(open(oracle_path)) if os.path.isfile(oracle_path) else None
    theta_seen = None
    if eps:
        r0 = eps[0]
        theta_seen = {k: r0[k] for k in ("randomizing", "lenMean", "lenIndex", "lenMiddle", "lenRing", "lenPinky", "lenThumb", "omegaFingerMean",
                                          "omegaFingerMin", "omegaFingerMax", "zetaFingerMean", "omegaWristMean", "activeGroups", "mask", "handSpan", "fingerLengthRatio")}
    return {
        "episodes_run": len(eps),
        "success": {"count": sum(success), "n": len(success), **bootstrap_ci(success)} if eps else None,
        "drop_pass": drop_pass,
        "palm_contact_rate": statistics.mean([int(r["palm"]) for r in held]) if held else float("nan"),
        "mean_contacts": statistics.mean([float(r["contacts"]) for r in held]) if held else float("nan"),
        "mean_steps_to_hold": statistics.mean([float(r["steps"]) for r in held]) if held else float("nan"),
        "feasible": bool(oracle["gateMet"]) if oracle and oracle.get("ran") else None,
        "oracle": oracle,
        "theta_as_applied": theta_seen,
    }


def evaluate(theta, n_episodes=20, mu_levels=(1.0,), seed_block=(4001, 4020), *, out_dir=None, player=None,
             deterministic=False, drop_repeats=3, oracle=True, timeout=3600.0, tag=None, log_decisions=False,
             allow_spent_seeds=False):
    """Evaluate one morphology theta with the deployed run-010 policy on fixed seeds.

    theta        dict, see theta.py (lengthScale[5], omega[16], zeta[16], inertiaScale[16], mask[14])
    n_episodes   episodes = seeds seed_block[0] .. seed_block[0] + n_episodes - 1 (must fit inside seed_block)
    mu_levels    drop-test friction coefficients (1.0 is the primary one)
    seed_block   (first, last) inclusive; 4001-4100 is reserved for BO; 1001-1100 / 2001-2100 / 3001-3100 are spent
                 (only allowed with allow_spent_seeds=True, e.g. for the reproduction gate)
    deterministic  use the model's deterministic action head instead of the sampled one (the recorded evaluations sampled)
    oracle       run the forced-close feasibility check at theta before the episodes (feasible flag in the result)
    Returns a dict: success rate with bootstrap CI, drop-pass per mu (given hold and times success, with CIs),
    palm-contact rate, mean contact count, feasible flag + oracle record, CSV paths. Q is not reported.
    """
    theta = validate(theta)
    seeds = seeds_for(n_episodes, seed_block)
    if seeds and not allow_spent_seeds:
        for a, b in SPENT_SEED_BLOCKS:
            if any(a <= s <= b for s in seeds):
                raise ValueError(f"seeds {seeds[0]}-{seeds[-1]} overlap the spent block {a}-{b}; use the reserved block {RESERVED_SEED_BLOCK} or pass allow_spent_seeds=True")
    if out_dir is None:
        out_dir = os.path.join(DEFAULT_ROOT, time.strftime("%Y%m%d_%H%M%S") + "_" + (tag or theta_hash(theta)))
    out_dir = os.path.abspath(out_dir)
    job = {
        **theta,
        "seeds": seeds,
        "mu": [float(m) for m in mu_levels],
        "dropRepeats": int(drop_repeats),
        "deterministic": bool(deterministic),
        "oracle": bool(oracle),
        "oracleSteps": 300,
        "logDecisions": bool(log_decisions),
        "logDecisionsMax": 400,
        "timeScale": 20.0,
        "outDir": out_dir,
    }
    done = run_player(job, player=player, timeout=timeout)
    result = {
        "theta": theta,
        "theta_hash": theta_hash(theta),
        "seeds": seeds,
        "n_episodes": n_episodes,
        "mu_levels": [float(m) for m in mu_levels],
        "deterministic": bool(deterministic),
        "discarded": done.get("discarded", 0),
        "elapsed_s": done.get("elapsedSec"),
        "wall_s": done.get("wallSec"),
        "out_dir": out_dir,
        "files": {
            "episodes_csv": os.path.join(out_dir, "episodes.csv"),
            "drops_csv": os.path.join(out_dir, "drops.csv"),
            "oracle_json": os.path.join(out_dir, "oracle.json"),
            "job_json": os.path.join(out_dir, "job.json"),
            "player_log": os.path.join(out_dir, "player.log"),
        },
    }
    result.update(summarize(out_dir, mu_levels))
    with open(os.path.join(out_dir, "result.json"), "w") as f:
        json.dump(result, f, indent=1)
    return result


def feasible(theta, *, player=None, out_dir=None, timeout=600.0):
    """Forced-close feasibility oracle only (no policy episodes): returns (feasible: bool, max contact count, oracle record)."""
    r = evaluate(theta, n_episodes=0, mu_levels=(1.0,), seed_block=RESERVED_SEED_BLOCK, out_dir=out_dir, player=player,
                 oracle=True, timeout=timeout, tag="oracle_" + theta_hash(validate(theta)))
    o = r["oracle"] or {}
    return bool(o.get("gateMet", False)), int(o.get("maxContacts", 0)), o


def format_summary(r):
    lines = [f"theta {r['theta_hash']}  seeds {r['seeds'][0] if r['seeds'] else '-'}..{r['seeds'][-1] if r['seeds'] else '-'}  deterministic={r['deterministic']}  out={r['out_dir']}"]
    if r.get("oracle"):
        o = r["oracle"]
        lines.append(f"feasible={r['feasible']}  oracle: gateMet={o['gateMet']} firstGateStep={o['firstGateStep']} maxContacts={o['maxContacts']} finalContacts={o['finalContacts']} pushOut={o['pushOutMm']:.1f}mm activeGroups={o['activeGroups']}")
    if r.get("success"):
        s = r["success"]
        lines.append(f"success {s['count']}/{s['n']} = {s['mean']:.3f} [{s['lo']:.2f}, {s['hi']:.2f}]  discarded={r['discarded']}  palmContact={r['palm_contact_rate']:.2f}  contacts={r['mean_contacts']:.2f}  stepsToHold={r['mean_steps_to_hold']:.1f}")
        for mu, d in r["drop_pass"].items():
            g, u = d["given_hold"], d["times_success"]
            lines.append(f"mu={mu}: pass|hold {g['mean']:.3f} [{g['lo']:.3f}, {g['hi']:.3f}] (n={g['n']})  pass x success {u['mean']:.3f} [{u['lo']:.3f}, {u['hi']:.3f}]  any/all repeats {d['any_repeat']:.2f}/{d['all_repeats']:.2f}")
    return "\n".join(lines)
