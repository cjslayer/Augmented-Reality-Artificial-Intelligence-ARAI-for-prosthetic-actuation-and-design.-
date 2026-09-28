# Pulse-limited or grasp-limited? Lying-object success of run 013 with and without the perturbation pulses (2026-09-28)

Question: training success on rest-pose (lying) presentations sat at about 0.40 from 11M to 20M in both phase C and phase D. Is that ceiling
set by the perturbation pulses breaking an otherwise adequate grasp, or by the lying grasp itself?

Method: 013C (`results/013/013C/Prosthetic.onnx`, Prosthetic-13499981, end of phase C) and 013D (`results/013/013D/Prosthetic.onnx`,
Prosthetic-20000063, end of phase D), random-stable presentation, held-out shapes 65-80, seeds 10001-10300, passIndex 0, K = 50, mu 1.0, mass
as 012, deterministic head, one job worker, per-episode reseed, idle machine. Perturbation 0 (`results/013/eval/pulse/`, two new passes)
against the Part 3 perturbation-1.0 pass of the same model (`results/013/eval/<model>/heldout_randomStable_10001_10300_p0.csv`). Rows pair
one to one on (seed, shape): shape assignment, theta, mass, spawn, yaw and rest pose are drawn identically, and at perturbation 0 the three
pulses are still scheduled at the same hold steps with zero force, so the drop-phase split uses the same step times. The pair covers
passIndex 0 only (300 episodes, 18-19 per shape), not the second Part 3 pass, to stay within the two-pass limit. Tables:
`tools/eval/pulse_vs_grasp.py`.

## Verdict table

| model | success, perturbation 0 | success, perturbation 1.0 | paired difference [95 % CI] | verdict |
|---|---|---|---|---|
| 013C | 0.550 | 0.537 | +0.013 [-0.039, +0.066] | **GRASP-LIMITED** |
| 013D | 0.530 | 0.497 | +0.033 [-0.019, +0.085] | **GRASP-LIMITED** |

### 013C: 300 rows paired (perturbation 0 vs 1.0)

| quantity | perturbation 0 | perturbation 1.0 |
|---|---|---|
| success [95 % Wilson] | 0.550 [0.493, 0.605] (n=300) | 0.537 [0.480, 0.592] (n=300) |
| lifted | 0.610 [0.554, 0.663] (n=300) | 0.603 [0.547, 0.657] (n=300) |
| lifted then dropped (share of lifted episodes) | 0.087 (16/183) | 0.105 (19/181) |
| lifted then dropped (share of all episodes) | 0.053 | 0.063 |
| reach failures | 9 | 10 |
| ends | {'liftBudget': 92, 'drop': 32, 'success': 165, 'reach': 9, 'taskBudget': 2} | {'liftBudget': 89, 'drop': 39, 'success': 161, 'reach': 10, 'taskBudget': 1} |
| drops: lift phase | 19 | 21 |
| drops: before first pulse | 8 | 8 |
| drops: during pulse | 2 | 1 |
| drops: between pulses | 1 | 7 |
| drops: after last pulse | 2 | 2 |
| median hold step at drop (of 500) | 0 | 0 |
| contacts at drop / at success | 4.28 / 5.57 | 3.95 / 5.47 |
| palm flag at drop / at success | 0.53 / 0.78 | 0.41 / 0.76 |
| thumb flag at drop / at success | 0.94 / 0.99 | 0.82 / 1.00 |
| **paired success difference, perturbation 0 − 1.0 [95 % CI]** | **+0.013 [-0.039, +0.066]** | |

Verdict by the rule: **GRASP-LIMITED**.

| subset | n | perturbation 0 | perturbation 1.0 | paired difference [95 % CI] |
|---|---|---|---|---|
| length < 130 mm | 94 | 0.479 [0.381, 0.579] (n=94) | 0.479 [0.381, 0.579] (n=94) | +0.000 [-0.093, +0.093] |
| length 130-210 mm | 169 | 0.627 [0.552, 0.697] (n=169) | 0.604 [0.528, 0.674] (n=169) | +0.024 [-0.046, +0.093] |
| length 210-280 mm | 37 | 0.378 [0.241, 0.539] (n=37) | 0.378 [0.241, 0.539] (n=37) | +0.000 [-0.150, +0.150] |
| pose class lying | 260 | 0.538 [0.478, 0.598] (n=260) | 0.538 [0.478, 0.598] (n=260) | +0.000 [-0.055, +0.055] |
| pose class upright | 2 | 0.000 [0.000, 0.658] (n=2) | 0.000 [0.000, 0.658] (n=2) | +0.000 [+0.000, +0.000] |
| pose class tilted rest | 38 | 0.658 [0.499, 0.788] (n=38) | 0.553 [0.397, 0.699] (n=38) | +0.105 [-0.054, +0.265] |

### 013D: 300 rows paired (perturbation 0 vs 1.0)

| quantity | perturbation 0 | perturbation 1.0 |
|---|---|---|
| success [95 % Wilson] | 0.530 [0.473, 0.586] (n=300) | 0.497 [0.440, 0.553] (n=300) |
| lifted | 0.627 [0.571, 0.679] (n=300) | 0.610 [0.554, 0.663] (n=300) |
| lifted then dropped (share of lifted episodes) | 0.144 (27/188) | 0.180 (33/183) |
| lifted then dropped (share of all episodes) | 0.090 | 0.110 |
| reach failures | 9 | 8 |
| ends | {'liftBudget': 74, 'success': 159, 'drop': 56, 'reach': 9, 'taskBudget': 2} | {'liftBudget': 76, 'success': 149, 'drop': 66, 'taskBudget': 1, 'reach': 8} |
| drops: lift phase | 32 | 35 |
| drops: before first pulse | 5 | 9 |
| drops: during pulse | 1 | 3 |
| drops: between pulses | 13 | 15 |
| drops: after last pulse | 5 | 4 |
| median hold step at drop (of 500) | 0 | 0 |
| contacts at drop / at success | 4.04 / 5.74 | 4.08 / 5.80 |
| palm flag at drop / at success | 0.43 / 0.79 | 0.53 / 0.88 |
| thumb flag at drop / at success | 0.89 / 1.00 | 0.85 / 1.00 |
| **paired success difference, perturbation 0 − 1.0 [95 % CI]** | **+0.033 [-0.019, +0.085]** | |

Verdict by the rule: **GRASP-LIMITED**.

| subset | n | perturbation 0 | perturbation 1.0 | paired difference [95 % CI] |
|---|---|---|---|---|
| length < 130 mm | 94 | 0.457 [0.360, 0.558] (n=94) | 0.394 [0.301, 0.495] (n=94) | +0.064 [-0.042, +0.169] |
| length 130-210 mm | 169 | 0.609 [0.534, 0.680] (n=169) | 0.568 [0.493, 0.640] (n=169) | +0.041 [-0.025, +0.108] |
| length 210-280 mm | 37 | 0.351 [0.218, 0.512] (n=37) | 0.432 [0.287, 0.591] (n=37) | -0.081 [-0.197, +0.034] |
| pose class lying | 260 | 0.527 [0.466, 0.587] (n=260) | 0.512 [0.451, 0.572] (n=260) | +0.015 [-0.041, +0.072] |
| pose class upright | 2 | 1.000 [0.342, 1.000] (n=2) | 1.000 [0.342, 1.000] (n=2) | +0.000 [+0.000, +0.000] |
| pose class tilted rest | 38 | 0.526 [0.373, 0.675] (n=38) | 0.368 [0.234, 0.527] (n=38) | +0.158 [+0.021, +0.295] |

## Mechanical reading

Removing the pulses barely moves either model: +0.013 [−0.039, +0.066] for 013C and +0.033 [−0.019, +0.085] for 013D, both inside
noise, so both are **GRASP-LIMITED** by the rule. The failures are not drops of a held object. The largest single ending is the lift
budget: 89-92 of 300 episodes for 013C and 74-76 for 013D meet the contact gate and then never raise the object 3 cm within the
200-step lift budget. The next largest is a drop in the lift phase (hold step 0; 19-21 and 32-35 episodes), before any pulse has fired.
Of the lifted episodes only 9-11 % (013C) and 14-18 % (013D) are dropped afterwards, and drops during a pulse are 1-3 per model. At a
drop the grasp encloses less of the object than at a success: 4.0-4.3 finger groups in contact against 5.5-5.8, the palm touching in
41-53 % against 76-88 %, and the thumb slightly less often (0.82-0.94 against 0.99-1.00). The ceiling is forming a grasp on a lying
object that can take its weight off the pedestal, which is the same enclosure deficit that the upright long bars show. The one pulse
effect is on the 38 tilted rest poses for 013D (+0.158 [+0.021, +0.295]), a small subset. The ~40 % "lifted then dropped" read from the
training stats does not survive the deterministic head: in training the sampled head lifts 0.66 and succeeds 0.39, while under evaluation
the deterministic head lifts 0.60-0.63 and loses only 9-18 % of what it lifts. The gap between gate and success is the lift budget, not the
hold.

## 16.5M interim check (from `results/013/interim/INTERIM_16p5M.md`)

Checkpoint Prosthetic-16499988 (phase D, 3M steps into the fixed 50/50 mix) against 012 and 013C on the same 192 (seed, shape, theta) rows:
train shapes 1-64 × 2 on seeds 9001-9128 and held-out shapes 65-80 × 4 on 9129-9192, passIndex 0, both presentations. These passes ran during
training and are not byte-reproducible (the load sensitivity documented in REPORT.md), but the rows are paired by construction.

### canonical (192 rows: train shapes 1-64 x2 on 9001-9128, held-out 65-80 x4 on 9129-9192)

| subset | 012 | 013C | 013 @16.5M | 16.5M − 012 | 16.5M − 013C |
|---|---|---|---|---|---|
| all (n=192) | 0.615 [0.544, 0.681] | 0.031 [0.014, 0.066] | 0.521 [0.450, 0.590] | -0.094 [-0.179, -0.008] | +0.490 [+0.415, +0.565] |
| train shapes (n=128) | 0.633 [0.547, 0.711] | 0.023 [0.008, 0.067] | 0.531 [0.445, 0.616] | -0.102 [-0.209, +0.006] | +0.508 [+0.419, +0.597] |
| held-out shapes (n=64) | 0.578 [0.456, 0.691] | 0.047 [0.016, 0.129] | 0.500 [0.381, 0.619] | -0.078 [-0.217, +0.061] | +0.453 [+0.317, +0.590] |
| length < 130 mm (n=62) | 0.081 [0.035, 0.175] | 0.097 [0.045, 0.195] | 0.403 [0.290, 0.527] | +0.323 [+0.198, +0.447] | +0.306 [+0.168, +0.445] |
| length 130-210 mm (n=92) | 0.837 [0.748, 0.899] | 0.000 [-0.000, 0.040] | 0.598 [0.496, 0.692] | -0.239 [-0.349, -0.129] | +0.598 [+0.498, +0.698] |
| length 210-280 mm (n=38) | 0.947 [0.827, 0.985] | 0.000 [0.000, 0.092] | 0.526 [0.373, 0.675] | -0.421 [-0.594, -0.248] | +0.526 [+0.368, +0.685] |
- 012: gate 0.750, lifted 0.677, palm 0.844, thumb 0.828, reach failures 48
- 013C: gate 0.839, lifted 0.104, palm 0.302, thumb 0.688, reach failures 31
- 013 @16.5M: gate 0.948, lifted 0.630, palm 0.583, thumb 0.859, reach failures 11

### randomStable (192 rows: train shapes 1-64 x2 on 9001-9128, held-out 65-80 x4 on 9129-9192)

| subset | 012 | 013C | 013 @16.5M | 16.5M − 012 | 16.5M − 013C |
|---|---|---|---|---|---|
| all (n=192) | 0.005 [0.001, 0.029] | 0.583 [0.513, 0.651] | 0.536 [0.466, 0.606] | +0.531 [+0.459, +0.603] | -0.047 [-0.110, +0.017] |
| train shapes (n=128) | 0.008 [0.001, 0.043] | 0.594 [0.507, 0.675] | 0.547 [0.461, 0.630] | +0.539 [+0.450, +0.628] | -0.047 [-0.121, +0.028] |
| held-out shapes (n=64) | 0.000 [0.000, 0.057] | 0.562 [0.441, 0.677] | 0.516 [0.396, 0.634] | +0.516 [+0.393, +0.638] | -0.047 [-0.165, +0.071] |
| length < 130 mm (n=62) | 0.016 [0.003, 0.086] | 0.565 [0.441, 0.681] | 0.468 [0.349, 0.590] | +0.452 [+0.320, +0.583] | -0.097 [-0.204, +0.010] |
| length 130-210 mm (n=92) | 0.000 [-0.000, 0.040] | 0.598 [0.496, 0.692] | 0.598 [0.496, 0.692] | +0.598 [+0.498, +0.698] | +0.000 [-0.095, +0.095] |
| length 210-280 mm (n=38) | 0.000 [0.000, 0.092] | 0.579 [0.422, 0.721] | 0.500 [0.348, 0.652] | +0.500 [+0.341, +0.659] | -0.079 [-0.213, +0.055] |
- 012: gate 0.078, lifted 0.016, palm 0.786, thumb 0.057, reach failures 177
- 013C: gate 0.943, lifted 0.635, palm 0.615, thumb 0.969, reach failures 12
- 013 @16.5M: gate 0.964, lifted 0.641, palm 0.583, thumb 0.948, reach failures 7
