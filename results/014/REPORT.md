# Run 014: evaluation report (Part 4b, 2026-10-08)

**Do not deploy.** The scene model stays 012 (`Assets/Models/Prosthetic.onnx`, sha256 9bda654f…, unchanged). This report is for CJ's decision on an extension.

Models: 014 = `results/014/Prosthetic.onnx` (014_r2 at 24M, sha256 164d4273…); 013E = `results/013/013E/Prosthetic.onnx` (8a3097a9…). Both 013E and 013 / 013D (96ac32e6…) are unchanged.

The driver is `tools/eval/eval_014.py` (stages block1–block4). It ran 43 passes, all n = 300, and all ended DONE; logs are in `eval/passes.json` and `eval/run_blocks.log`. Tables come from `tools/eval/report_014.py`.

## Summary

- **Upright, 5-digit: 014 beats 013E by +0.170** [+0.111, +0.229], paired (0.903 vs 0.733, n = 300, McNemar p 5e-8). At lift budget 200 the difference is +0.180.
- **Lying, 5-digit: 014 is far behind 013E, by −0.400** [−0.465, −0.335] (0.157 vs 0.557, p 3e-26). At lift budget 200 it is −0.383.
  - 014 reaches the gate on lying objects (0.930) but lifts only 0.483.
  - Its episodes end on a drop in 0.390 of cases and on a lift-budget timeout in 0.383.
- **Anchor cylinder 7001–7300: 014 0.990 vs 013E 0.893, a gain of +0.097** [+0.060, +0.134].
  - Results are identical at lift budgets 200 and 300, because the budget never binds: every lifted row lifts within 43 steps of the gate.
- **Composition (014, deterministic):**
  - Upright: the 5-digit hand is best at 0.903, and 4 digits is level with it (−0.023, not significant). 6a, 6b and 7 lose 0.08–0.12.
  - Upright 3-digit loses 0.313 (0.590): 26 % of its episodes end on MaxStep, which means it stalls before the gate.
  - Lying: success runs from 0.02 (3) to 0.18 (7). 6a and 7 are level with 5; 3, 4 and 6b are below.
- **Pulse test (lying):** perturbation 1.0 vs 0 changes success by −0.057 to +0.003.
  - Only 6a's CI excludes 0: −0.057 [−0.098, −0.015]. That is one of six tests, uncorrected.
  - Drop rate changes by −0.007 to +0.047, and no CI excludes 0.
  - The perturbation pulse does not cause the lying deficit.
- **Sampled head:** below deterministic in every cell (upright by 0.05 to 0.19, lying by 0.02 to 0.12).
- **Matched-rows sensitivity:** agrees with the headline (upright +0.171, lying −0.371, anchor +0.102).

## Pairing and seed ledger

- **Seeds:**

  | seeds | use |
  |---|---|
  | 12001–12002 | layout check |
  | 12101–12400 | spawn-only draws at checkpoint 4 (spent) |
  | 12401–12700 | resets only in the containment probe and the decision-agreement passes (no policy step, no outcome observed); then every held-out cell of this eval (blocks 1, 2, 4) |
  | 7001–7300 | anchor cylinder (block 3; the same block 013E's substrate gate used) |

  The next fresh held-out block is 12701+.
- **Placements.** With spawnStream on, the harness reseeds `UnityEngine.Random` with DerivedSeed(seed, 0) ^ 0x5A17 right before the object and spawn draw. Object, rest pose, yaw and position therefore do not depend on the layout's θ draws.
  - Checked in the result rows: objIndex, poseMode, poseIndex and spawnYaw match 300 / 300 between each composition and the 5-digit pass. This holds in both poses, for deterministic and sampled passes, and for the perturbation-0 passes.
  - They also match 300 / 300 between 014 and 013E in every paired cell.
- **Decision agreement** (CJ 2026-10-08, `eval/CONTAIN.md`): on every paired row (5-digit upright 300, lying 300, anchor 300), both the spawn envelope and 013E's own-hand filter were evaluated on that row's candidates.
  - Result: **0 disagreements, 0 fallbacks.**
  - So the headline 014 − 013E pairing uses all 300 rows per cell.
  - 013E was run in the legacy layout with the envelope filter forced. Its placements match 014's 5-digit hand 300 / 300.
- **Geometric containment does not hold.** The frozen-pose envelope misses by about 0.9 mm typically and up to 12.9 mm at the forearm. The backlog item is recorded in `eval/CONTAIN.md`.
- **Common settings:**
  - mode eval, one job worker, per-episode reseed;
  - K = 50, μ 1.0, randomized θ per episode;
  - held-out shapes 65–80, stratified (anchor: cylinder only);
  - perturbation 1.0 and lift budget 300 unless stated.

## 1. Headline: 014 by composition × pose (held-out, lift budget 300)

The deterministic head is the headline. Gate = contact transition; lift = lifted; hold = hold phase entered. The end-reason split sums to 1.

### Upright (held-out shapes 65–80, seeds 12401–12700, lift budget 300, perturbation 1.0)

| composition | n | success, deterministic [95 % Wilson] | gate | lift | hold entered | end success | end drop | end liftBudget | end taskBudget | end maxStep | success, sampled [95 %] |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 300 | **0.590** [0.534, 0.644] | 0.740 | 0.660 | 0.653 | 0.590 | 0.133 | 0.017 | 0.000 | 0.260 | 0.513 [0.457, 0.569] |
| 4 | 300 | **0.880** [0.838, 0.912] | 0.980 | 0.930 | 0.923 | 0.880 | 0.097 | 0.003 | 0.000 | 0.020 | 0.710 [0.656, 0.758] |
| 5 | 300 | **0.903** [0.865, 0.932] | 0.987 | 0.950 | 0.943 | 0.903 | 0.080 | 0.003 | 0.000 | 0.013 | 0.803 [0.755, 0.844] |
| 6a | 300 | **0.783** [0.733, 0.826] | 0.983 | 0.843 | 0.837 | 0.783 | 0.187 | 0.013 | 0.000 | 0.017 | 0.667 [0.612, 0.718] |
| 6b | 300 | **0.823** [0.776, 0.862] | 1.000 | 0.943 | 0.943 | 0.823 | 0.133 | 0.043 | 0.000 | 0.000 | 0.760 [0.709, 0.805] |
| 7 | 300 | **0.790** [0.740, 0.832] | 0.997 | 0.907 | 0.900 | 0.790 | 0.173 | 0.033 | 0.000 | 0.003 | 0.603 [0.547, 0.657] |

### Lying (held-out shapes 65–80, seeds 12401–12700, lift budget 300, perturbation 1.0)

| composition | n | success, deterministic [95 % Wilson] | gate | lift | hold entered | end success | end drop | end liftBudget | end taskBudget | end maxStep | success, sampled [95 %] |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 300 | **0.023** [0.011, 0.047] | 0.497 | 0.147 | 0.140 | 0.023 | 0.187 | 0.283 | 0.000 | 0.507 | 0.007 [0.002, 0.024] |
| 4 | 300 | **0.067** [0.044, 0.101] | 0.840 | 0.267 | 0.263 | 0.067 | 0.283 | 0.483 | 0.003 | 0.163 | 0.030 [0.016, 0.056] |
| 5 | 300 | **0.157** [0.120, 0.202] | 0.930 | 0.483 | 0.463 | 0.157 | 0.390 | 0.383 | 0.000 | 0.070 | 0.060 [0.038, 0.093] |
| 6a | 300 | **0.147** [0.111, 0.191] | 0.933 | 0.470 | 0.443 | 0.147 | 0.407 | 0.373 | 0.007 | 0.067 | 0.057 [0.036, 0.089] |
| 6b | 300 | **0.103** [0.074, 0.143] | 0.913 | 0.490 | 0.480 | 0.103 | 0.500 | 0.303 | 0.007 | 0.087 | 0.030 [0.016, 0.056] |
| 7 | 300 | **0.180** [0.141, 0.227] | 0.923 | 0.507 | 0.493 | 0.180 | 0.383 | 0.357 | 0.003 | 0.077 | 0.057 [0.036, 0.089] |

How to read these tables:
- **Upright losses** are drops after lift (0.08–0.19). The 3-digit hand also stalls (MaxStep 0.26, gate 0.74).
- **Lying losses** split between never lifting within the budget (liftBudget 0.30–0.48) and drops (0.19–0.50).
- **Gate on lying** is reached in 0.84–0.93 of episodes (3-digit: 0.50).

### Composition minus 5-digit, paired by row (deterministic, lift budget 300)

| pose | composition | n | d success [95 %] | b / c | McNemar p |
|---|---|---|---|---|---|
| upright | 3 | 300 | -0.313 [-0.368, -0.258] | 3 / 97 | 2.63e-25 |
| upright | 4 | 300 | -0.023 [-0.056, +0.009] | 9 / 16 | 0.23 |
| upright | 6a | 300 | -0.120 [-0.166, -0.074] | 9 / 45 | 7.29e-07 |
| upright | 6b | 300 | -0.080 [-0.128, -0.032] | 16 / 40 | 0.00184 |
| upright | 7 | 300 | -0.113 [-0.161, -0.066] | 11 / 45 | 5.38e-06 |
| lying | 3 | 300 | -0.133 [-0.174, -0.093] | 2 / 42 | 1.13e-10 |
| lying | 4 | 300 | -0.090 [-0.133, -0.047] | 9 / 36 | 6.57e-05 |
| lying | 6a | 300 | -0.010 [-0.059, +0.039] | 26 / 29 | 0.788 |
| lying | 6b | 300 | -0.053 [-0.102, -0.005] | 20 / 36 | 0.044 |
| lying | 7 | 300 | +0.023 [-0.026, +0.073] | 32 / 25 | 0.427 |
b = rows where the composition succeeds and the 5-digit hand fails; c = the reverse.

## 2. 014 − 013E on the 5-digit hand (paired)

### 014 − 013E on the 5-digit hand, paired by row (deterministic; 013E with the envelope filter forced, all rows)

| cell | lift budget | n | 014 success [95 %] | 013E success [95 %] | d (014 − 013E) [95 % paired] | b / c | McNemar p | rows identical (object, pose, yaw) |
|---|---|---|---|---|---|---|---|---|
| upright | 300 | 300 | 0.903 [0.865, 0.932] | 0.733 [0.681, 0.780] | **+0.170** [+0.111, +0.229] | 70 / 19 | 4.96e-08 | 300 / 300 |
| upright | 200 | 300 | 0.900 [0.861, 0.929] | 0.720 [0.667, 0.768] | **+0.180** [+0.121, +0.239] | 72 / 18 | 8.07e-09 | 300 / 300 |
| lying | 300 | 300 | 0.157 [0.120, 0.202] | 0.557 [0.500, 0.612] | **-0.400** [-0.465, -0.335] | 13 / 133 | 3.15e-26 | 300 / 300 |
| lying | 200 | 300 | 0.153 [0.117, 0.198] | 0.537 [0.480, 0.592] | **-0.383** [-0.450, -0.317] | 16 / 131 | 1.24e-23 | 300 / 300 |
| anchor 7001–7300 | 300 | 300 | 0.990 [0.971, 0.997] | 0.893 [0.853, 0.923] | **+0.097** [+0.060, +0.134] | 32 / 3 | 4.18e-07 | 300 / 300 |
| anchor 7001–7300 | 200 | 300 | 0.990 [0.971, 0.997] | 0.893 [0.853, 0.923] | **+0.097** [+0.060, +0.134] | 32 / 3 | 4.18e-07 | 300 / 300 |

### Sensitivity: matched rows only, 013E on its own-hand filter (lift budget 300)

| cell | matched rows | d (014 − 013E own filter) [95 % paired] | b / c | McNemar p |
|---|---|---|---|---|
| upright | 299 | +0.171 [+0.112, +0.229] | 70 / 19 | 4.96e-08 |
| lying | 245 | -0.371 [-0.446, -0.297] | 15 / 106 | 4.72e-18 |
| anchor | 295 | +0.102 [+0.063, +0.140] | 33 / 3 | 2.27e-07 |

**How the sensitivity line is built:**
- It runs 013E on its own-hand filter, with no forced envelope.
- It keeps only the rows where that filter picks the same spawn attempt as the envelope, taken from the decision-agreement passes.
- The 014 side is the headline pass.

It agrees with the headline in sign and size.

013E is consistent with its own earlier evaluation (`results/013/013E/GATE.md`, different seeds), so the forced envelope and spawnStream do not move it materially:

| cell | earlier 013E eval | this eval |
|---|---|---|
| upright | 0.755 | 0.733 |
| lying | 0.567 | 0.557 |
| anchor | 0.883 | 0.893 |

Lift budget 200 vs 300 (014 5-digit, deterministic): upright 0.900 vs 0.903, lying 0.153 vs 0.157. The budget does not limit 014 on lying.

## 3. Pulse test (lying, all compositions, perturbation 0 vs 1.0, identical rows)

### Pulse test: lying, perturbation 0 vs 1.0 on identical rows (deterministic, lift budget 300)

| composition | n | success p0 | success p1 | d success (p1 − p0) [95 %] | drop p0 | drop p1 | d drop (p1 − p0) [95 %] | lift p0 | lift p1 |
|---|---|---|---|---|---|---|---|---|---|
| 3 | 300 | 0.020 | 0.023 | +0.003 [-0.011, +0.018] | 0.170 | 0.187 | +0.017 [-0.027, +0.061] | 0.167 | 0.147 |
| 4 | 300 | 0.073 | 0.067 | -0.007 [-0.034, +0.021] | 0.283 | 0.283 | +0.000 [-0.059, +0.059] | 0.277 | 0.267 |
| 5 | 300 | 0.170 | 0.157 | -0.013 [-0.055, +0.028] | 0.363 | 0.390 | +0.027 [-0.030, +0.083] | 0.467 | 0.483 |
| 6a | 300 | 0.203 | 0.147 | -0.057 [-0.098, -0.015] | 0.360 | 0.407 | +0.047 [-0.007, +0.100] | 0.517 | 0.470 |
| 6b | 300 | 0.127 | 0.103 | -0.023 [-0.066, +0.019] | 0.493 | 0.500 | +0.007 [-0.056, +0.069] | 0.523 | 0.490 |
| 7 | 300 | 0.187 | 0.180 | -0.007 [-0.052, +0.039] | 0.390 | 0.383 | -0.007 [-0.061, +0.048] | 0.530 | 0.507 |

The pulse has at most a small effect on lying success and drop rate:
- With the pulse off, 5-digit lying success is still only 0.170. For comparison, 013E with the pulse on reaches 0.557.
- The drops do not come from the pulse: at perturbation 0 the drop rate is already 0.17–0.49.

## 4. Training statistics at 24M vs eval

- **Training:** phase B episode logs, last 1M steps (23M–24M), sampled head, training shapes 1–64 plus 10 % cylinder, with the policy still moving during the window (`B_CHECKS.md`).
- **Eval:** held-out shapes 65–80, frozen 24M policy. Sampled is the like-for-like head; deterministic is shown for reference.

| pose | composition | training success (sampled) | eval success, sampled | eval success, deterministic | training lift | eval lift (det) |
|---|---|---|---|---|---|---|
| upright | 3 | 0.673 | 0.513 | 0.590 | 0.788 | 0.660 |
| upright | 4 | 0.764 | 0.710 | 0.880 | 0.893 | 0.930 |
| upright | 5 | 0.789 | 0.803 | 0.903 | 0.904 | 0.950 |
| upright | 6a | 0.740 | 0.667 | 0.783 | 0.877 | 0.843 |
| upright | 6b | 0.780 | 0.760 | 0.823 | 0.935 | 0.943 |
| upright | 7 | 0.669 | 0.603 | 0.790 | 0.861 | 0.907 |
| lying | 3 | 0.062 | 0.007 | 0.023 | 0.279 | 0.147 |
| lying | 4 | 0.086 | 0.030 | 0.067 | 0.384 | 0.267 |
| lying | 5 | 0.115 | 0.060 | 0.157 | 0.468 | 0.483 |
| lying | 6a | 0.111 | 0.057 | 0.147 | 0.469 | 0.470 |
| lying | 6b | 0.078 | 0.030 | 0.103 | 0.458 | 0.490 |
| lying | 7 | 0.092 | 0.057 | 0.180 | 0.459 | 0.507 |

What the comparison shows:
- **Sampled eval vs training:** sampled eval success sits 0.02–0.16 below training upright and 0.03–0.06 below on lying. The eval uses held-out shapes and has no cylinder share. The one exception is 5-digit upright, at +0.014.
- **Deterministic head:** it recovers that gap and more. The eval ranking matches the training ranking, with 3 digits the weakest.
- **Lying:** pooled training success was 0.10 at 24M, so the lying result was already visible in training. The held-out eval confirms it and sets it against 013E.

## 5. Confound note

014 and 013E had different training histories, so the 5-digit gap is **not** a pure multi-topology interference cost.

| | training history |
|---|---|
| 013 lineage | a lying-only phase (013 phase C), then a 50/50 upright/lying mix, all on one hand (5-digit, legacy layout). 013E continued that lineage. |
| 014_r2 | from scratch, upright only through A1 / A2 (2.25M). Then lying was introduced cold at 2.25M in phase B, together with all compositions, about 45 % of draws on the 5-digit hand (design digit shares), and perturbation 1.0 from the start of B. |

The upright gain (+0.17) and the lying deficit (−0.40) could come from the curriculum (no lying-only phase), from sharing capacity across topologies, or both. This eval cannot separate them. Separating them would need one of:
- a 5-digit-only control trained on 014's curriculum;
- 014 given a lying-focused phase.

## Files

| files | contents |
|---|---|
| `eval/b1_*` | block 1, plus 014 5-digit at lift budget 200 |
| `eval/b2_*` | 013E held-out, including `_ownfilter` |
| `eval/b3_*` | anchor |
| `eval/b4_*` | perturbation 0 |
| `eval/pair_*`, `eval/contain_*` | resets only |
| `eval/CONTAIN.md` | containment and decision agreement |
| `eval/passes.json` | config and hash per pass |
