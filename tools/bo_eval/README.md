# tools/bo_eval — fixed-theta evaluation endpoint (BO outer loop, Part A)

`evaluate(theta, n_episodes=20, mu_levels=(1.0,), seed_block=(4001, 4020)) -> dict` evaluates one morphology vector
theta with the deployed run-010 policy on fixed seeds and returns the drop-test pass rates (the ground-truth metric),
grasp success, palm-contact rate, mean contact count and a forced-close feasibility flag. It is the function the
Bayesian optimizer (Part B) calls; nothing here implements the optimizer.

## How it runs

Python writes a job file and launches the headless **BoEval player** (`Builds/BoEval/BoEval.exe`, built from the
committed `Dynamic_Scene` — InferenceOnly, `Assets/Models/Prosthetic.onnx` = run 010 — with
`Assets/Scripts/BoEval/BoEvalHarness.cs` compiled in). `BoEvalBootstrap` attaches the harness at scene load only when
the player is started with `-boEvalJob <job.json>`; without that argument the scene behaves exactly as committed.
The harness:

1. pins theta through the `MorphologyManager` serialized fields with `randomizeByDefault = false` before every episode
   (link scales, `k = I w^2`, `b = 2 zeta I w`, `I = nominalI * inertiaScale`, 14-bit mask; a first reset recomputes the
   nominal inertia at the requested link scales, the second starts the first evaluated episode);
2. optionally runs the forced-close oracle (MorphVerify close mode: object teleported to `GraspPoint`, backed off along
   the palm normal, staged wrap for 300 steps under HeuristicOnly; `feasible` = hold criterion met);
3. runs the seeded episodes (`Random.InitState(seed)` before each reset, hold = 50 consecutive held steps) and the drop
   test with the mechanics of the recorded reference-hand evaluation (`Physics.Simulate` inside one FixedUpdate, arm
   kinematic, platform disabled, 3 repeats per friction level from the identical pre-drop state, pass = centre
   displacement < 0.1 m after 2 s);
4. writes `episodes.csv`, `drops.csv` (same columns as `results/010/validation/reference_hand/`), `oracle.json`,
   optionally `decisions.csv`, and `done.json`, then quits.

Python (stdlib only) summarizes the CSVs: mean per-episode pass fraction given hold and times success, with bootstrap
95% CIs (4000 resamples, `Random(0)`, the scheme used by `refhand_summary.py` / `paired_mu.py`). The reward-side
quality score Q is a training shaping signal and is **not** reported.

## Usage

```
# from the repository root, ML-Agents venv python (C:\Users\chris\ml-agents\venv)
python -m tools.bo_eval --theta reference --episodes 20 --mu 1.0 --seeds 4001 4020
python -m tools.bo_eval --theta random --random-seed 7 --episodes 20 --mu 0.6 1.0 1.5
python -m tools.bo_eval --theta my_theta.json --out results/bo_eval/runs/my_run --json
python -m tools.bo_eval --theta reference --oracle-only
```

```python
from tools.bo_eval import evaluate, feasible, reference_theta, random_theta
r = evaluate(theta, n_episodes=20, mu_levels=(1.0,), seed_block=(4001, 4020))
r["success"]["mean"], r["drop_pass"]["1.0"]["given_hold"], r["feasible"], r["palm_contact_rate"], r["files"]["episodes_csv"]
```

theta (see `theta.py`): `lengthScale[5]`, `omega[16]`, `zeta[16]`, `inertiaScale[16]`, `mask[14]`; `reference_theta()`
is the reference hand, `random_theta(rng)` draws from the training distribution (same rule as
`MorphologyManager.Sample`).

Options: `deterministic=True` uses the model's deterministic action head (the recorded evaluations, and the default
here, use the sampled head, whose noise stream is seeded per player process, so results are reproducible run to run);
`oracle=False` skips the feasibility check; `log_decisions=True` writes per-decision observation/action rows;
`out_dir` (default `results/bo_eval/runs/<timestamp>_<theta hash>/`); `player` or `$BO_EVAL_PLAYER` for the exe.

## Seeds

1001-1100, 2001-2100 and 3001-3100 are spent on model decisions; `evaluate` refuses them unless
`allow_spent_seeds=True` (reproduction checks only). **4001-4100 is the reserved BO evaluation block**: every candidate
theta is evaluated on the same seeds (common random numbers), so paired comparisons between candidates are low-variance.
Paper numbers need a further held-out block.

## Rebuilding the player

Any change to the harness or the deployed model needs a rebuild (the Editor must have the project open):

```
unity command build --target StandaloneWindows64 --outputPath Builds/BoEval/BoEval.exe --confirm true
```

`Builds/` is gitignored. 100 episodes with three friction levels take about 20 s of wall time.

## Validation (2026-09-18, details in MLAGENTS_UPGRADE.md)

- Inference fidelity: deterministic head on 64 fixed observation sets, Editor vs player: max |difference| 0.0; a
  two-episode trajectory with the deterministic head is identical decision for decision (267/267) between Editor and
  player.
- Reproduction of the recorded reference-hand evaluation (seeds 2001-2100): success 100/100; pass given hold
  0.663 / 0.747 / 0.840 at mu 0.6 / 1.0 / 1.5 against the recorded 0.690 / 0.770 / 0.880, all inside the recorded CIs;
  per-seed pass-fraction match 68 / 72 / 77 % (different sampled-noise stream); endpoint run-to-run 900/900 identical.
- Theta variation: a random theta is applied on every episode row (`randomizing = 0`, scales / omega / zeta / mask as
  requested).

`palm_by_theta.py` is a read-only analysis of the existing random-theta records (palm-contact rate by theta bin).
