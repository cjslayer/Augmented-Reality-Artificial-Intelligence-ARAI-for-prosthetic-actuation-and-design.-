# tools/bo_optim — Bayesian morphology optimizer (BO outer loop, Part B)

Optimizes the morphology vector theta of the prosthetic hand for the deployed run-010 policy, using
`tools/bo_eval.evaluate` as the objective. Outputs go to `results/bo_optim/` (gitignored); the campaign report is
`results/bo_optim/REPORT.md`.

```
python -m tools.bo_optim run      # full campaign; resumable (finished candidates are read back from records.jsonl)
python -m tools.bo_optim report   # regenerate REPORT.md, best_so_far.csv, best_so_far.svg, candidates.csv
```

Run from the repository root with the ML-Agents venv python (`C:\Users\chris\ml-agents\venv`); torch and numpy only,
nothing else is installed (the GP, the feasibility model and the SVG plot are written directly).

## Objective and instrument

- Objective: **pass x success at mu = 1.0** from `evaluate(theta, n_episodes=20, mu_levels=(1.0,), seed_block=(4001, 4020))`
  with the sampled action head (`deterministic=False`, deployment behaviour). Q never enters ranking or acquisition.
- `evaluate` is deterministic for a given (theta, seed block, player build): common spawn seeds and a per-process-seeded
  policy noise stream. One player build is used for the whole campaign; its sha256 over `Builds/BoEval/` (runtime
  outputs excluded) is recorded in `build_hash.json` at the start and re-checked at the end.
- Drift control: the reference theta is re-evaluated after every 25 objective evaluations and its `episodes.csv` and
  `drops.csv` must be byte-identical to the anchor (BO candidate #1); any difference stops the campaign.
- Feasibility: every candidate first goes through the forced-close oracle (`bo_eval.feasible`); infeasible candidates
  are recorded for the feasibility model only and never enter the objective GP.

## Search space (`space.py`, bounds from `tools/bo_eval/theta.py` = `MorphologyManager.Sample`)

53 continuous dims normalized to [0, 1]: 5 link-length scales [0.8, 1.2]; omega for 16 impedance groups (14 finger
groups [12, 40] rad/s, 2 wrist axes [8, 25]); zeta (fingers [0.4, 0.9], wrist [0.5, 0.9]); inertia scale [0.5, 2.0].
14-bit actuation mask constrained to >= 6 active groups with at least one thumb group; masks are only ever drawn from the
constrained set (Bernoulli(0.8) with rejection, the training rule).

## Surrogate and acquisition (`models.py`)

- Exact GP: Matern-5/2 on the continuous dims with one lengthscale per parameter block (scales, omega, zeta, inertia)
  times an exponential Hamming kernel on the mask, `exp(-H/l_m)`; outputscale and noise fitted; standardized targets;
  hyperparameters by Adam on the log marginal likelihood under weak log-normal priors.
- Feasibility: L2-regularized logistic regression on [x, mask, active fraction] over all oracle outcomes so far
  (Laplace-smoothed constant while only one class has been observed).
- Acquisition: expected improvement over the best observed feasible objective, times P(feasible); maximized by ranking a
  pool of 10,000 random constrained draws plus 5,000 local perturbations of the top-5 incumbents (sigma 0.05 normalized,
  mask bits flipped with p = 0.1, constraint-repaired). No gradient steps over the mask.

## Campaign (`campaign.py`)

1. BO arm: reference theta as candidate #1 (anchor), 29 random constrained draws (`Random(20260919)`), then 70 BO iterations.
2. Random baseline: 100 random constrained draws (`Random(20260920)`), same seeds and build.
3. Stops: drift mismatch; `evaluate` errors on more than 2 candidates; oracle feasibility rate below 20 % after the
   initial design.
4. Confirmation: top 5 of each arm (by the optimization-block objective) re-evaluated on seeds 4021-4100
   (80 fresh episodes) with mu 0.6 / 1.0 / 1.5. These are the headline numbers; optimization-block numbers are
   selection-biased and labelled as such.

Files: `records.jsonl` (every evaluation, one JSON per line), `evals/<arm>_<idx>/{oracle,obj}/`, `drift/`, `confirm/`,
`campaign.log`, `summary.json`, `candidates.csv`, `best_so_far.csv`, `best_so_far.svg`, `REPORT.md`.
