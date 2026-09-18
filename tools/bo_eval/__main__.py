"""CLI for the fixed-theta evaluation endpoint.

  python -m tools.bo_eval --theta reference --episodes 20 --mu 1.0 --seeds 4001 4020
  python -m tools.bo_eval --theta random --random-seed 7 --episodes 20 --mu 0.6 1.0 1.5
  python -m tools.bo_eval --theta my_theta.json --out results/bo_eval/runs/my_run
  python -m tools.bo_eval --theta reference --oracle-only
Run from the repository root with the ML-Agents venv python (stdlib only; numpy/pandas not required).
"""
import argparse
import json
import random
import sys

from .evaluate import evaluate, feasible, format_summary, RESERVED_SEED_BLOCK
from .theta import reference_theta, random_theta, validate


def load_theta(spec, random_seed):
    if spec == "reference":
        return reference_theta()
    if spec == "random":
        return random_theta(random.Random(random_seed))
    with open(spec) as f:
        return validate(json.load(f))


def main(argv=None):
    p = argparse.ArgumentParser(prog="python -m tools.bo_eval", description="Evaluate one morphology theta with the deployed run-010 policy (drop test).")
    p.add_argument("--theta", default="reference", help="'reference', 'random' or a path to a theta JSON file")
    p.add_argument("--random-seed", type=int, default=0, help="RNG seed for --theta random")
    p.add_argument("--episodes", type=int, default=20)
    p.add_argument("--mu", type=float, nargs="+", default=[1.0])
    p.add_argument("--seeds", type=int, nargs=2, default=list(RESERVED_SEED_BLOCK[:1]) + [RESERVED_SEED_BLOCK[0] + 19], metavar=("FIRST", "LAST"))
    p.add_argument("--allow-spent-seeds", action="store_true", help="permit the spent blocks 1001-1100 / 2001-2100 / 3001-3100 (reproduction checks only)")
    p.add_argument("--out", default=None, help="output directory (default results/bo_eval/runs/<timestamp>_<theta hash>)")
    p.add_argument("--player", default=None, help="BoEval player exe (default Builds/BoEval/BoEval.exe or $BO_EVAL_PLAYER)")
    p.add_argument("--deterministic", action="store_true", help="deterministic action head (the recorded evaluations sampled)")
    p.add_argument("--no-oracle", action="store_true")
    p.add_argument("--oracle-only", action="store_true", help="feasibility oracle only, no policy episodes")
    p.add_argument("--drop-repeats", type=int, default=3)
    p.add_argument("--log-decisions", action="store_true", help="write decisions.csv (per-decision observation + action)")
    p.add_argument("--timeout", type=float, default=3600.0)
    p.add_argument("--tag", default=None)
    p.add_argument("--json", action="store_true", help="print the full result as JSON")
    a = p.parse_args(argv)

    theta = load_theta(a.theta, a.random_seed)
    if a.oracle_only:
        ok, contacts, rec = feasible(theta, player=a.player, out_dir=a.out)
        print(f"feasible={ok} maxContacts={contacts} oracle={json.dumps(rec)}")
        return 0
    r = evaluate(theta, n_episodes=a.episodes, mu_levels=tuple(a.mu), seed_block=tuple(a.seeds), out_dir=a.out, player=a.player,
                 deterministic=a.deterministic, drop_repeats=a.drop_repeats, oracle=not a.no_oracle, timeout=a.timeout, tag=a.tag,
                 log_decisions=a.log_decisions, allow_spent_seeds=a.allow_spent_seeds)
    print(json.dumps(r, indent=1) if a.json else format_summary(r))
    return 0


if __name__ == "__main__":
    sys.exit(main())
