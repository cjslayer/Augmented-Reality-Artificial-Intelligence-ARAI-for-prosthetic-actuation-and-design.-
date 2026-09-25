# BO Part B on the articulated hand — dynamic-range probe (2026-09-24): no objective level with range, campaign not run

## Setup

Policy `results/012/Prosthetic.onnx` (deployed run 012, sha256 9bda654f) evaluated through the Editor harness
`ArticulatedGates` mode `eval` with a fixed theta per pass (`thetaFixed`, commit 63b03e1): deterministic head, one
job worker, per-episode reseed (`DerivedSeed(seed, passIndex)`), mu 1.0, K = 50, seeds 4001-4100 (n = 100 per pass, the
reserved BO block), throwaway pass first. theta = the 67-dimensional training parameterization of `MorphologyManager`
in k (tools/bo/theta_space.py: 5 link-length scales, 16 stiffnesses, 16 damping ratios, 16 inertia scales, 14 mask bits).
Zero-step rows follow tools/eval/summarize_pass.py (reach failures counted). Each pass is byte-reproducible within the
session (tools/bo/evaluate_theta.py cross-checks the pinned theta against the logged summaries). Files: `probe.csv`
(one row per pass), `probe_thetas.json` (the 20 theta, training distribution, seed 424242), `probe_passes/*.csv`,
`probe_table.md`, `smoke/` (harness verification passes).

## 2b — dynamic-range probe

20 theta from the training distribution, each evaluated at perturbation scales 1.0 / 1.5 / 2.0 / 3.0 with the default
object-mass range (0.2-1.5 kg log-uniform), then, since no scale qualified, with the mass range doubled to 0.4-3.0 kg
(environment parameters mass/min, mass/max; the observation normalisation keeps the training range) at scales 1.0 and 2.0.
Rule: the smallest scale whose mean success over the 20 theta lies in [0.45, 0.80] and whose across-theta SD is >= 0.10.

| scale | mass range (kg) | n theta | mean success | min | max | SD across theta | qualifies |
|---|---|---|---|---|---|---|---|
| 1.0 | 0.2-1.5 (default) | 20 | 0.994 | 0.91 | 1.00 | 0.020 | no |
| 1.5 | 0.2-1.5 | 20 | 0.993 | 0.93 | 1.00 | 0.017 | no |
| 2.0 | 0.2-1.5 | 20 | 0.987 | 0.85 | 1.00 | 0.040 | no |
| 3.0 | 0.2-1.5 | 20 | 0.976 | 0.85 | 1.00 | 0.051 | no |
| 1.0 | 0.4-3.0 | 20 | 0.952 | 0.66 | 1.00 | 0.094 | no |
| 2.0 | 0.4-3.0 | 20 | 0.951 | 0.57 | 1.00 | 0.108 | no |

Chosen level: NONE. The deployed policy succeeds on 95-99 % of episodes for every training-distribution hand at every
level tried; the across-theta spread comes from three or four hands (probe theta 5, 10, 14, 18) that read 0.57-0.87 at the
harder levels while the rest stay at 0.99-1.00. Mean reach-failure rate: 0.1 % (default mass) to 1.3 % (doubled mass).
A mean in [0.45, 0.80] would need a stressor well beyond a 3x perturbation or a 2x mass range; per the brief's stop
rule the campaign (2c-2f) is not run and the objective is declared to have no range at these levels.

## What was and wasn't found

- Found: the harness can pin any theta of the training space for a whole pass (verified with the reference hand and a
  doubled inertia scale: link masses double, logged theta summaries match), and the deployed policy is close to
  saturated over the training theta distribution at mu 1.0 for perturbation scales up to 3.0 and object masses up to 3 kg.
- Not found: an objective level with enough range for a BO campaign as specified; hence no BO-vs-random comparison and
  no best theta.
- Feasibility: the Part A forced-close oracle (tools/bo_eval) translated to the articulated hand as the harness's
  scripted enveloping close rejects the reference hand (4-5 contacts, gate never met, `smoke/oracle_*.csv`), while the
  policy grasps that hand on 100 / 100 episodes. The oracle is implemented (tools/bo/feasibility.py) but would not have
  been used; the campaign's feasibility would have been the analytic layer (bounds + the mask rule of
  MorphologyManager.Sample).
- Tooling ready for a re-run once a level exists: tools/bo/campaign.py (30 Sobol + 70 GP points vs 100 random draws,
  reference drift control every 20 evaluations), tools/bo/analysis.py, tools/bo/confirm.py (seeds 8001-8300). Python:
  numpy + torch from the ml-agents venv (no scipy / scikit-learn there; none added).
