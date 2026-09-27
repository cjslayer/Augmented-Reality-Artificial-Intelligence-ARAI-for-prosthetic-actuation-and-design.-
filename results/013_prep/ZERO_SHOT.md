# Zero-shot evaluation of the run-012 policy on the superquadric object bank (2026-09-27)

Question: does object shape create range in the grasp objective for the deployed policy, which was trained on one cylinder
(Ø 50 × 280 mm)? Method: the pre-cooked bank of `Assets/Objects/Bank/` (seed 13013; 64 train + 16 held-out superellipsoids with
cross-sections 30-80 mm, lengths 80-280 mm, e1, e2 ∈ [0.3, 1.5]; index 0 = the anchor cylinder) swapped into the scene per episode
by `ObjectBank` (commit 76d92c6), evaluated with the Editor instrument (`ArticulatedGates` mode eval: deterministic head, one job
worker, per-episode reseed `DerivedSeed(seed, passIndex)`, throwaway pass after the launch, K = 50, perturbation 1.0, mu 1.0,
mass log-uniform 0.2-1.5 kg exactly as run 012, theta random from the training distribution unless pinned). Shape assignment is
stratified by the harness (`objectStratify`): bank index = first + (episode ordinal + passIndex × episodes) mod count, so every
shape gets the same number of episodes and no random draw is consumed. Rows follow `tools/eval/summarize_pass.py` (reach failure =
MaxStep with no gate, counted as a failure; a first-episode zero-step row is excluded as a warm-up abort by that convention, which here
removed two genuine shape-1 reach failures, hence n = 898 for the train bank). Pass CSVs and `passes.json` are in `passes/`.
The 2a regression gate (`ANCHOR_REGRESSION.md`) passed before these passes: the anchor through the swap path reproduces the recorded
instrument byte for byte.

**One property of the instrument dominates the numbers and must be read first.** The spawn rule (`ArmGraspAgent.SpawnCylinder`,
unchanged, inventoried in `docs/design/013_object_randomization.md`) keeps the object's X/Z tilt from the previous episode's end pose
and randomizes only the yaw. The object is kinematic until the contact gate, so an object that fell over in a drop episode is spawned
lying on the pedestal in the next episode, and stays lying through every following reach-failure episode (no release, no change of
orientation). The tilt is logged per episode (`objSpawnTiltDeg`). Measured on the anchor cylinder itself
(`passes/anchor_fixed0_tilt_7001_7300.csv`, 300 episodes, success 0.990): 58 % of episodes start with the cylinder tilted by ≥ 10°
(median 11°, 90th percentile 22°, maximum 38°, none ≥ 60°), because a successfully held cylinder ends the episode tilted in the hand
and the next spawn keeps that tilt; the policy handles it (success 1.000 on the tilted 10-60° episodes, 0.976 on the upright ones), so
moderate tilt is part of the instrument the policy was trained and evaluated on. With the bank the state is different in kind:
**95 % of bank episodes start tilted by ≥ 10° and 53 % start lying (≥ 60°)**, a state the cylinder never reaches, and even the
10-60° bank episodes succeed at only 0.40. Every table below is followed by the split by spawn tilt; the "< 60°" rows are the
figures within the anchor instrument's own orientation range, the "lying" rows are a novel state that the carried-tilt rule creates
out of the first drop.


## 2a. Regression gate

PASS, see `ANCHOR_REGRESSION.md`: the anchor through the swap path is byte-identical on all 49 reference columns to the recorded repeats of the reference mu 1.0 pass; the anchor pass with the tilt column run at the end of this session (after 33 bank passes) gave 0.990 [0.971, 0.997] with 2 reach failures, another realisation of the same instrument inside the reference interval.

## 2b. Zero-shot, run 012 policy, random theta (training distribution), K = 50, perturb 1.0, mu 1.0, mass as 012

### Train bank (shapes 1-64), seeds 8001-8300 x passIndex 0-2, stratified: 898 episodes

- success 0.264 [0.236, 0.294]; lifted 0.370 [0.339, 0.402] (n=898); palm at end 0.767 [0.739, 0.794] (n=898); thumb at end 0.530 [0.497, 0.563] (n=898); reach-failure rate 0.510; mean contacts at end 2.79; mean hold steps 140
- ends: {'success': 237, 'reach failure': 458, 'drop': 201, 'liftBudget': 2}; drop phases: {'between pulses': 15, 'lift phase': 111, 'before first pulse': 66, 'during pulse 2': 2, 'during pulse 1': 3, 'after last pulse': 4}
- per-shape success: 64 shapes, min 0.00, median 0.29, max 0.57, SD across shapes 0.178; shapes <= 0.70: 64; shapes < 1.0: 64; episodes per shape min 13 max 15

Per-shape success, ranked (worst 20; the full table follows the family tables):

| shape | success | n | lifted | palm | 2·max(a1,a2) mm | length mm | e1 | e2 | volume cm³ | ends |
|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 0.00 | 15 | 0.00 | 0.73 | 43 | 81 | 0.61 | 0.93 | 79 | {'reach': 15} |
| 10 | 0.00 | 14 | 0.14 | 0.86 | 67 | 116 | 1.29 | 0.79 | 239 | {'drop': 4, 'reach': 10} |
| 14 | 0.00 | 14 | 0.00 | 0.86 | 78 | 90 | 0.77 | 0.74 | 176 | {'reach': 14} |
| 18 | 0.00 | 14 | 0.00 | 0.71 | 66 | 88 | 1.05 | 1.06 | 162 | {'reach': 13, 'drop': 1} |
| 23 | 0.00 | 14 | 0.14 | 0.57 | 60 | 112 | 0.38 | 0.70 | 297 | {'reach': 6, 'drop': 8} |
| 24 | 0.00 | 14 | 0.00 | 0.86 | 71 | 106 | 0.43 | 1.21 | 224 | {'reach': 12, 'drop': 2} |
| 27 | 0.00 | 14 | 0.00 | 0.64 | 48 | 83 | 1.12 | 0.77 | 85 | {'reach': 14} |
| 30 | 0.00 | 14 | 0.00 | 0.86 | 64 | 119 | 1.30 | 1.18 | 176 | {'reach': 9, 'drop': 5} |
| 44 | 0.00 | 14 | 0.00 | 0.86 | 79 | 82 | 1.05 | 1.01 | 243 | {'reach': 13, 'drop': 1} |
| 54 | 0.00 | 14 | 0.14 | 0.86 | 69 | 111 | 0.65 | 0.87 | 216 | {'reach': 10, 'drop': 4} |
| 55 | 0.00 | 14 | 0.00 | 0.86 | 75 | 90 | 1.29 | 0.92 | 148 | {'reach': 14} |
| 7 | 0.07 | 14 | 0.14 | 0.86 | 69 | 85 | 0.51 | 0.58 | 255 | {'reach': 8, 'drop': 5} |
| 46 | 0.07 | 14 | 0.14 | 0.86 | 75 | 136 | 1.13 | 0.59 | 414 | {'drop': 5, 'reach': 8} |
| 47 | 0.07 | 14 | 0.29 | 0.93 | 50 | 118 | 0.58 | 0.52 | 214 | {'reach': 9, 'drop': 4} |
| 48 | 0.07 | 14 | 0.21 | 0.86 | 76 | 103 | 0.70 | 0.63 | 254 | {'drop': 5, 'reach': 8} |
| 52 | 0.07 | 14 | 0.07 | 1.00 | 50 | 99 | 0.45 | 0.76 | 169 | {'reach': 13} |
| 61 | 0.07 | 14 | 0.14 | 0.79 | 63 | 114 | 1.30 | 0.73 | 202 | {'reach': 10, 'drop': 3} |
| 11 | 0.14 | 14 | 0.43 | 0.79 | 48 | 190 | 1.23 | 0.94 | 156 | {'reach': 7, 'drop': 5} |
| 22 | 0.14 | 14 | 0.21 | 0.79 | 72 | 125 | 1.08 | 1.39 | 177 | {'reach': 9, 'drop': 3} |
| 25 | 0.14 | 14 | 0.29 | 0.64 | 72 | 145 | 0.45 | 0.87 | 296 | {'reach': 10, 'drop': 2} |

By e1 (axial profile):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e1 0.3-0.7 | 28 | 393 | 0.265 [0.223, 0.310] (n=393) | 0.379 | 0.738 | 2.96 | 0.483 | 31 / 3 / 7 / 2 / 54 |
| e1 0.7-1.1 | 16 | 224 | 0.295 [0.239, 0.357] (n=224) | 0.375 | 0.826 | 2.69 | 0.536 | 15 / 1 / 1 / 1 / 20 |
| e1 1.1-1.5 | 20 | 281 | 0.238 [0.192, 0.292] (n=281) | 0.352 | 0.762 | 2.65 | 0.527 | 20 / 1 / 7 / 1 / 37 |

By e2 (cross-section shape):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e2 0.3-0.7 | 21 | 295 | 0.288 [0.239, 0.342] (n=295) | 0.403 | 0.736 | 2.90 | 0.431 | 25 / 2 / 5 / 1 / 50 |
| e2 0.7-1.1 | 21 | 296 | 0.172 [0.134, 0.219] (n=296) | 0.270 | 0.767 | 2.31 | 0.666 | 19 / 3 / 5 / 0 / 21 |
| e2 1.1-1.5 | 22 | 307 | 0.329 [0.279, 0.383] (n=307) | 0.433 | 0.798 | 3.16 | 0.436 | 22 / 0 / 5 / 3 / 40 |

By cross-section 2·max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| cross-section 50-65 mm | 15 | 210 | 0.257 [0.203, 0.320] (n=210) | 0.376 | 0.805 | 2.76 | 0.490 | 19 / 1 / 3 / 0 / 30 |
| cross-section 65-80 mm | 40 | 561 | 0.258 [0.224, 0.296] (n=561) | 0.358 | 0.745 | 2.79 | 0.504 | 36 / 3 / 12 / 3 / 77 |
| cross-section < 50 mm | 9 | 127 | 0.299 [0.226, 0.384] (n=127) | 0.409 | 0.803 | 2.87 | 0.567 | 11 / 1 / 0 / 1 / 4 |

By length 2·a3:

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| length 130-210 mm | 28 | 394 | 0.330 [0.285, 0.378] (n=394) | 0.470 | 0.784 | 3.39 | 0.424 | 41 / 2 / 5 / 2 / 46 |
| length 210-280 mm | 15 | 210 | 0.419 [0.354, 0.487] (n=210) | 0.524 | 0.671 | 3.44 | 0.352 | 12 / 2 / 7 / 1 / 26 |
| length < 130 mm | 21 | 294 | 0.065 [0.042, 0.099] (n=294) | 0.126 | 0.813 | 1.53 | 0.738 | 13 / 1 / 3 / 1 / 39 |

By aspect a3 / max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| aspect 1.5-3 | 34 | 479 | 0.223 [0.188, 0.263] (n=479) | 0.332 | 0.775 | 2.71 | 0.534 | 37 / 2 / 8 / 2 / 67 |
| aspect < 1.5 | 8 | 111 | 0.054 [0.025, 0.113] (n=111) | 0.081 | 0.838 | 1.35 | 0.802 | 3 / 0 / 0 / 0 / 12 |
| aspect >= 3 | 22 | 308 | 0.403 [0.349, 0.458] (n=308) | 0.532 | 0.731 | 3.44 | 0.367 | 26 / 3 / 7 / 2 / 32 |

By spawn tilt of the length axis (carried from the previous episode's end pose; the spawn randomizes only the yaw):

| spawn tilt | episodes | success | lifted | palm | reach fail |
|---|---|---|---|---|---|
| upright (< 10°) | 42 | 0.619 [0.468, 0.750] (n=42) | 0.667 | 0.738 | 0.262 |
| tilted (10-60°) | 382 | 0.401 [0.353, 0.450] (n=382) | 0.552 | 0.812 | 0.366 |
| all < 60° (the anchor instrument's own range, see the anchor row) | 424 | 0.422 [0.376, 0.470] (n=424) | 0.564 | 0.804 | 0.356 |
| lying (>= 60°) | 474 | 0.122 [0.096, 0.155] (n=474) | 0.196 | 0.734 | 0.648 |

Episodes starting with the object tilted >= 10°: 856 of 898 (95.3%); the upright-only success is the figure comparable with the anchor instrument's usual state.

By volume tercile (cuts 224, 314 cm³):

| tercile | shapes | episodes | success | lifted | palm |
|---|---|---|---|---|---|
| low | 21 | 294 | 0.163 [0.125, 0.210] (n=294) | 0.259 | 0.830 |
| mid | 21 | 295 | 0.254 [0.208, 0.307] (n=295) | 0.369 | 0.773 |
| high | 22 | 309 | 0.369 [0.317, 0.424] (n=309) | 0.476 | 0.702 |

Full per-shape table:

| shape | success | n | lifted | palm | cross mm | length mm | e1 | e2 |
|---|---|---|---|---|---|---|---|---|
| 3 | 0.00 | 15 | 0.00 | 0.73 | 43 | 81 | 0.61 | 0.93 |
| 10 | 0.00 | 14 | 0.14 | 0.86 | 67 | 116 | 1.29 | 0.79 |
| 14 | 0.00 | 14 | 0.00 | 0.86 | 78 | 90 | 0.77 | 0.74 |
| 18 | 0.00 | 14 | 0.00 | 0.71 | 66 | 88 | 1.05 | 1.06 |
| 23 | 0.00 | 14 | 0.14 | 0.57 | 60 | 112 | 0.38 | 0.70 |
| 24 | 0.00 | 14 | 0.00 | 0.86 | 71 | 106 | 0.43 | 1.21 |
| 27 | 0.00 | 14 | 0.00 | 0.64 | 48 | 83 | 1.12 | 0.77 |
| 30 | 0.00 | 14 | 0.00 | 0.86 | 64 | 119 | 1.30 | 1.18 |
| 44 | 0.00 | 14 | 0.00 | 0.86 | 79 | 82 | 1.05 | 1.01 |
| 54 | 0.00 | 14 | 0.14 | 0.86 | 69 | 111 | 0.65 | 0.87 |
| 55 | 0.00 | 14 | 0.00 | 0.86 | 75 | 90 | 1.29 | 0.92 |
| 7 | 0.07 | 14 | 0.14 | 0.86 | 69 | 85 | 0.51 | 0.58 |
| 46 | 0.07 | 14 | 0.14 | 0.86 | 75 | 136 | 1.13 | 0.59 |
| 47 | 0.07 | 14 | 0.29 | 0.93 | 50 | 118 | 0.58 | 0.52 |
| 48 | 0.07 | 14 | 0.21 | 0.86 | 76 | 103 | 0.70 | 0.63 |
| 52 | 0.07 | 14 | 0.07 | 1.00 | 50 | 99 | 0.45 | 0.76 |
| 61 | 0.07 | 14 | 0.14 | 0.79 | 63 | 114 | 1.30 | 0.73 |
| 11 | 0.14 | 14 | 0.43 | 0.79 | 48 | 190 | 1.23 | 0.94 |
| 22 | 0.14 | 14 | 0.21 | 0.79 | 72 | 125 | 1.08 | 1.39 |
| 25 | 0.14 | 14 | 0.29 | 0.64 | 72 | 145 | 0.45 | 0.87 |
| 29 | 0.14 | 14 | 0.21 | 0.86 | 77 | 124 | 1.24 | 0.47 |
| 42 | 0.14 | 14 | 0.29 | 0.71 | 74 | 143 | 0.34 | 1.36 |
| 9 | 0.21 | 14 | 0.36 | 0.71 | 52 | 127 | 1.24 | 0.53 |
| 13 | 0.21 | 14 | 0.43 | 0.64 | 67 | 158 | 0.35 | 1.43 |
| 19 | 0.21 | 14 | 0.29 | 0.79 | 71 | 121 | 0.45 | 0.42 |
| 31 | 0.21 | 14 | 0.36 | 0.71 | 69 | 276 | 1.21 | 0.35 |
| 38 | 0.21 | 14 | 0.36 | 0.79 | 76 | 166 | 0.93 | 1.26 |
| 51 | 0.21 | 14 | 0.43 | 0.71 | 49 | 148 | 0.59 | 0.78 |
| 53 | 0.21 | 14 | 0.43 | 0.71 | 78 | 199 | 1.34 | 1.16 |
| 56 | 0.21 | 14 | 0.50 | 0.79 | 64 | 178 | 1.14 | 0.90 |
| 20 | 0.29 | 14 | 0.50 | 0.50 | 71 | 267 | 0.52 | 0.99 |
| 57 | 0.29 | 14 | 0.57 | 0.50 | 72 | 219 | 0.56 | 0.41 |
| 63 | 0.29 | 14 | 0.50 | 0.71 | 75 | 191 | 0.90 | 0.99 |
| 45 | 0.31 | 13 | 0.31 | 0.85 | 73 | 103 | 0.40 | 1.49 |
| 4 | 0.33 | 15 | 0.47 | 0.73 | 75 | 192 | 1.46 | 0.80 |
| 1 | 0.36 | 14 | 0.50 | 0.93 | 62 | 145 | 1.34 | 1.44 |
| 12 | 0.36 | 14 | 0.43 | 0.64 | 73 | 255 | 1.47 | 0.34 |
| 21 | 0.36 | 14 | 0.43 | 0.71 | 59 | 144 | 0.47 | 0.64 |
| 26 | 0.36 | 14 | 0.36 | 0.93 | 60 | 162 | 0.84 | 1.06 |
| 28 | 0.36 | 14 | 0.50 | 0.79 | 62 | 198 | 0.58 | 1.18 |
| 40 | 0.36 | 14 | 0.50 | 0.79 | 71 | 210 | 0.88 | 1.23 |
| 41 | 0.36 | 14 | 0.43 | 0.86 | 70 | 180 | 1.03 | 0.54 |
| 58 | 0.36 | 14 | 0.43 | 0.71 | 75 | 247 | 0.60 | 1.31 |
| 59 | 0.36 | 14 | 0.64 | 0.86 | 56 | 141 | 0.36 | 0.34 |
| 5 | 0.43 | 14 | 0.43 | 0.86 | 40 | 138 | 0.36 | 1.28 |
| 6 | 0.43 | 14 | 0.50 | 0.79 | 66 | 178 | 0.40 | 0.49 |
| 15 | 0.43 | 14 | 0.64 | 0.86 | 53 | 196 | 1.15 | 0.59 |
| 32 | 0.43 | 14 | 0.50 | 0.93 | 73 | 198 | 0.59 | 1.15 |
| 35 | 0.43 | 14 | 0.57 | 1.00 | 44 | 160 | 1.02 | 1.23 |
| 39 | 0.43 | 14 | 0.50 | 0.36 | 76 | 250 | 0.54 | 0.70 |
| 49 | 0.43 | 14 | 0.43 | 0.64 | 74 | 206 | 0.64 | 1.33 |
| 50 | 0.43 | 14 | 0.50 | 0.57 | 64 | 190 | 0.51 | 0.68 |
| 60 | 0.43 | 14 | 0.71 | 0.86 | 45 | 157 | 0.65 | 1.45 |
| 62 | 0.43 | 14 | 0.57 | 0.79 | 77 | 262 | 1.04 | 1.23 |
| 64 | 0.43 | 14 | 0.57 | 0.57 | 78 | 251 | 1.34 | 0.73 |
| 2 | 0.47 | 15 | 0.53 | 0.53 | 70 | 162 | 0.62 | 0.39 |
| 8 | 0.50 | 14 | 0.57 | 0.71 | 72 | 249 | 1.23 | 1.32 |
| 16 | 0.50 | 14 | 0.57 | 0.57 | 71 | 261 | 1.39 | 1.35 |
| 33 | 0.50 | 14 | 0.57 | 0.79 | 66 | 268 | 1.02 | 1.02 |
| 36 | 0.50 | 14 | 0.50 | 0.93 | 46 | 213 | 0.88 | 0.55 |
| 37 | 0.50 | 14 | 0.64 | 0.93 | 65 | 206 | 0.60 | 1.12 |
| 43 | 0.50 | 14 | 0.57 | 0.86 | 71 | 206 | 1.09 | 1.19 |
| 17 | 0.57 | 14 | 0.64 | 0.71 | 48 | 227 | 1.06 | 0.36 |
| 34 | 0.57 | 14 | 0.57 | 0.79 | 54 | 226 | 1.17 | 1.00 |

### Held-out bank (shapes 65-80), seeds 8001-8300 x passIndex 0-1, stratified: 600 episodes

- success 0.107 [0.084, 0.134]; lifted 0.173 [0.145, 0.206] (n=600); palm at end 0.757 [0.721, 0.789] (n=600); thumb at end 0.248 [0.215, 0.284] (n=600); reach-failure rate 0.707; mean contacts at end 1.67; mean hold steps 59
- ends: {'success': 64, 'drop': 111, 'reach failure': 424, 'liftBudget': 1}; drop phases: {'during pulse 2': 1, 'lift phase': 71, 'between pulses': 8, 'before first pulse': 26, 'during pulse 3': 1, 'after last pulse': 2, 'during pulse 1': 2}
- per-shape success: 16 shapes, min 0.00, median 0.12, max 0.22, SD across shapes 0.068; shapes <= 0.70: 16; shapes < 1.0: 16; episodes per shape min 37 max 38

Per-shape success, ranked (worst 20; the full table follows the family tables):

| shape | success | n | lifted | palm | 2·max(a1,a2) mm | length mm | e1 | e2 | volume cm³ | ends |
|---|---|---|---|---|---|---|---|---|---|---|
| 72 | 0.00 | 38 | 0.00 | 0.84 | 64 | 99 | 1.44 | 0.93 | 95 | {'reach': 36, 'drop': 2} |
| 67 | 0.03 | 38 | 0.11 | 0.97 | 68 | 111 | 1.45 | 0.56 | 167 | {'drop': 4, 'reach': 33} |
| 69 | 0.03 | 38 | 0.16 | 0.84 | 59 | 121 | 1.11 | 1.01 | 122 | {'reach': 28, 'drop': 9} |
| 80 | 0.03 | 37 | 0.03 | 0.81 | 59 | 93 | 0.54 | 0.32 | 137 | {'reach': 33, 'drop': 3} |
| 73 | 0.05 | 37 | 0.11 | 0.81 | 71 | 92 | 0.58 | 0.78 | 258 | {'reach': 29, 'drop': 6} |
| 74 | 0.05 | 37 | 0.16 | 0.81 | 66 | 134 | 0.80 | 1.15 | 212 | {'drop': 9, 'reach': 26} |
| 68 | 0.08 | 38 | 0.16 | 0.84 | 66 | 134 | 1.50 | 1.33 | 171 | {'drop': 7, 'reach': 28} |
| 65 | 0.11 | 38 | 0.24 | 0.61 | 73 | 272 | 0.69 | 0.47 | 660 | {'reach': 21, 'drop': 13} |
| 71 | 0.13 | 38 | 0.24 | 0.68 | 67 | 136 | 1.47 | 0.65 | 122 | {'reach': 26, 'drop': 7} |
| 75 | 0.14 | 37 | 0.16 | 0.59 | 72 | 139 | 0.93 | 1.41 | 173 | {'reach': 24, 'drop': 8} |
| 77 | 0.14 | 37 | 0.16 | 0.81 | 71 | 151 | 0.54 | 1.50 | 387 | {'drop': 9, 'reach': 23} |
| 70 | 0.16 | 38 | 0.26 | 0.79 | 55 | 186 | 0.70 | 1.30 | 236 | {'reach': 26, 'drop': 6} |
| 76 | 0.16 | 37 | 0.24 | 0.78 | 57 | 149 | 1.11 | 0.58 | 204 | {'reach': 25, 'drop': 6} |
| 66 | 0.18 | 38 | 0.18 | 0.76 | 57 | 175 | 1.05 | 0.64 | 255 | {'reach': 21, 'drop': 9, 'liftBudget': 1} |
| 78 | 0.22 | 37 | 0.27 | 0.54 | 75 | 267 | 0.55 | 0.83 | 533 | {'reach': 21, 'drop': 8} |
| 79 | 0.22 | 37 | 0.30 | 0.59 | 68 | 203 | 1.09 | 1.39 | 178 | {'reach': 24, 'drop': 5} |

By e1 (axial profile):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e1 0.3-0.7 | 5 | 186 | 0.108 [0.071, 0.160] (n=186) | 0.161 | 0.715 | 1.65 | 0.683 | 7 / 0 / 3 / 0 / 29 |
| e1 0.7-1.1 | 5 | 187 | 0.150 [0.106, 0.208] (n=187) | 0.214 | 0.711 | 1.95 | 0.647 | 9 / 0 / 3 / 0 / 25 |
| e1 1.1-1.5 | 6 | 227 | 0.070 [0.044, 0.111] (n=227) | 0.150 | 0.828 | 1.46 | 0.775 | 10 / 4 / 2 / 2 / 17 |

By e2 (cross-section shape):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e2 0.3-0.7 | 6 | 226 | 0.106 [0.072, 0.153] (n=226) | 0.173 | 0.770 | 1.59 | 0.704 | 7 / 4 / 3 / 1 / 27 |
| e2 0.7-1.1 | 4 | 150 | 0.073 [0.041, 0.127] (n=150) | 0.133 | 0.760 | 1.47 | 0.760 | 6 / 0 / 2 / 1 / 16 |
| e2 1.1-1.5 | 6 | 224 | 0.129 [0.092, 0.180] (n=224) | 0.201 | 0.741 | 1.88 | 0.674 | 13 / 0 / 3 / 0 / 28 |

By cross-section 2·max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| cross-section 50-65 mm | 6 | 226 | 0.093 [0.062, 0.138] (n=226) | 0.146 | 0.805 | 1.68 | 0.748 | 9 / 1 / 1 / 1 / 23 |
| cross-section 65-80 mm | 10 | 374 | 0.115 [0.086, 0.151] (n=374) | 0.190 | 0.727 | 1.67 | 0.682 | 17 / 3 / 7 / 1 / 48 |

By length 2·a3:

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| length 130-210 mm | 9 | 337 | 0.139 [0.107, 0.181] (n=337) | 0.208 | 0.742 | 1.83 | 0.662 | 15 / 3 / 4 / 1 / 43 |
| length 210-280 mm | 2 | 75 | 0.160 [0.094, 0.259] (n=75) | 0.253 | 0.573 | 2.16 | 0.560 | 5 / 0 / 2 / 0 / 14 |
| length < 130 mm | 5 | 188 | 0.027 [0.011, 0.061] (n=188) | 0.080 | 0.856 | 1.19 | 0.846 | 6 / 1 / 2 / 1 / 14 |

By aspect a3 / max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| aspect 1.5-3 | 11 | 412 | 0.090 [0.066, 0.121] (n=412) | 0.155 | 0.782 | 1.51 | 0.743 | 16 / 4 / 5 / 2 / 42 |
| aspect < 1.5 | 1 | 37 | 0.054 [0.015, 0.177] (n=37) | 0.108 | 0.811 | 0.92 | 0.784 | 1 / 0 / 1 / 0 / 4 |
| aspect >= 3 | 4 | 151 | 0.166 [0.115, 0.233] (n=151) | 0.238 | 0.675 | 2.30 | 0.589 | 9 / 0 / 2 / 0 / 25 |

By spawn tilt of the length axis (carried from the previous episode's end pose; the spawn randomizes only the yaw):

| spawn tilt | episodes | success | lifted | palm | reach fail |
|---|---|---|---|---|---|
| upright (< 10°) | 23 | 0.435 [0.256, 0.632] (n=23) | 0.478 | 0.913 | 0.435 |
| tilted (10-60°) | 145 | 0.345 [0.272, 0.425] (n=145) | 0.552 | 0.738 | 0.269 |
| all < 60° (the anchor instrument's own range, see the anchor row) | 168 | 0.357 [0.289, 0.432] (n=168) | 0.542 | 0.762 | 0.292 |
| lying (>= 60°) | 432 | 0.009 [0.004, 0.024] (n=432) | 0.030 | 0.755 | 0.868 |

Episodes starting with the object tilted >= 10°: 577 of 600 (96.2%); the upright-only success is the figure comparable with the anchor instrument's usual state.

By volume tercile (cuts 171, 236 cm³):

| tercile | shapes | episodes | success | lifted | palm |
|---|---|---|---|---|---|
| low | 5 | 189 | 0.042 [0.022, 0.081] (n=189) | 0.106 | 0.831 |
| mid | 5 | 186 | 0.129 [0.088, 0.185] (n=186) | 0.204 | 0.726 |
| high | 6 | 225 | 0.142 [0.103, 0.194] (n=225) | 0.204 | 0.720 |

Full per-shape table:

| shape | success | n | lifted | palm | cross mm | length mm | e1 | e2 |
|---|---|---|---|---|---|---|---|---|
| 72 | 0.00 | 38 | 0.00 | 0.84 | 64 | 99 | 1.44 | 0.93 |
| 67 | 0.03 | 38 | 0.11 | 0.97 | 68 | 111 | 1.45 | 0.56 |
| 69 | 0.03 | 38 | 0.16 | 0.84 | 59 | 121 | 1.11 | 1.01 |
| 80 | 0.03 | 37 | 0.03 | 0.81 | 59 | 93 | 0.54 | 0.32 |
| 73 | 0.05 | 37 | 0.11 | 0.81 | 71 | 92 | 0.58 | 0.78 |
| 74 | 0.05 | 37 | 0.16 | 0.81 | 66 | 134 | 0.80 | 1.15 |
| 68 | 0.08 | 38 | 0.16 | 0.84 | 66 | 134 | 1.50 | 1.33 |
| 65 | 0.11 | 38 | 0.24 | 0.61 | 73 | 272 | 0.69 | 0.47 |
| 71 | 0.13 | 38 | 0.24 | 0.68 | 67 | 136 | 1.47 | 0.65 |
| 75 | 0.14 | 37 | 0.16 | 0.59 | 72 | 139 | 0.93 | 1.41 |
| 77 | 0.14 | 37 | 0.16 | 0.81 | 71 | 151 | 0.54 | 1.50 |
| 70 | 0.16 | 38 | 0.26 | 0.79 | 55 | 186 | 0.70 | 1.30 |
| 76 | 0.16 | 37 | 0.24 | 0.78 | 57 | 149 | 1.11 | 0.58 |
| 66 | 0.18 | 38 | 0.18 | 0.76 | 57 | 175 | 1.05 | 0.64 |
| 78 | 0.22 | 37 | 0.27 | 0.54 | 75 | 267 | 0.55 | 0.83 |
| 79 | 0.22 | 37 | 0.30 | 0.59 | 68 | 203 | 1.09 | 1.39 |

### Reading of 2b

- **Level.** Train bank 0.264 [0.236, 0.294] over 898 episodes, held-out bank 0.107 [0.084, 0.134] over 600, against 0.993 for
  the anchor on the same instrument. Half of the train episodes (51 %) and 71 % of the held-out episodes never meet the contact gate
  in 5,000 steps; of the episodes that do, 46 % (train) to 63 % (held-out) are dropped, mostly in the lift phase or before the first pulse (the early-slip
  signature of the hard hands in `results/bo/partB/HARD_HANDS.md`); the pulses play no role (9 of 312 drops during a pulse).
- **What the gate failures look like.** At the end of a reach-failure episode the palm touches the object in 75-80 % of cases but only
  1.5-2.9 finger groups do and the thumb in 25-53 %: the hand reaches the object and lies on it without closing six segments and the
  thumb around it. On a lying object the fingers meet a surface that is 30-80 mm high instead of a 280 mm upright column.
- **Families.** Length is the strongest axis: objects shorter than 130 mm succeed in 6.5 % of episodes, 130-210 mm in 33 %, 210-280 mm in
  42 % (train bank); aspect a3 / max(a1, a2) < 1.5 gives 5.4 %, ≥ 3 gives 40 %; the low-volume tercile 16 % against 37 % for the high.
  Cross-section width (30-80 mm) is flat at 26-30 %, e1 is flat (24-30 %), and e2 is non-monotone (round 0.7-1.1: 17 %; rounded
  square 0.3-0.7: 29 %; rounded diamond 1.1-1.5: 33 %). Length and aspect are also the axes that decide how a fallen object presents
  itself: a long lying object is still a graspable bar, a short one is a puck.
- **Spawn orientation.** Upright spawns succeed in 62 % (n = 42), tilted 10-60° in 40 % (n = 382), lying in 12 % (n = 474). The
  held-out bank is worse than the train bank (0.11 vs 0.26) not because its shapes are harder (same distribution, 16 draws) but
  because its two passes fell into the lying state earlier: pass 0 (0.017, 261 reach failures) versus pass 1 (0.197, 163), same seeds,
  same shapes, different `passIndex`. The three train passes (0.234 / 0.338 / 0.220) show the same pass-level spread. Per-shape
  numbers (13-15 episodes per train shape, 37-38 per held-out shape) inherit this and rank shapes by luck of orientation as much as
  by geometry.
- **Per-shape.** Train: 64 shapes from 0.00 to 0.57 (median 0.29, SD 0.18), all 64 ≤ 0.70, 11 at 0.00; held-out: 16 shapes from 0.00
  to 0.22. The eleven train shapes at 0.00 are all short: 81-119 mm long, aspect 1.0-1.8.

## 2c. Range verdict

Rule: SHAPE-RANGE if overall success ≤ 0.85 or ≥ 8 of 80 shapes ≤ 0.70; SHAPE-FLAT if overall ≥ 0.95 and min shape ≥ 0.85; else
SHAPE-MARGINAL. Overall (train + held-out, 1,498 episodes): 0.201 [0.181, 0.222]; 80 of 80 shapes ≤ 0.70; min shape 0.00.
**Verdict: SHAPE-RANGE**, driven by object length / aspect (short, puck-like objects) and, above everything, by the carried spawn
orientation, which turns every family into a mostly-lying object once the first drop has happened. The verdict is a statement about
the policy on this instrument: the policy has no grasp for a short or lying object, and the instrument keeps the object lying. Whether
range remains after the object is respawned upright each episode is the first thing to measure (within the anchor instrument's own
orientation range, tilt < 60°, the bank scores 0.42 on the train split and 0.36 on the held-out split; upright-only rows suggest 0.6 on
small n; all far below the anchor's 0.99), and whether 013 should spawn upright, random, or carried is a task decision the brief did not take;
the harness would need a spawn-orientation rule (a task change, not made here).

## 2d. Hand x shape grid: fixed theta, 80 shapes x 5 episodes per hand, seeds 8301-8400 x passIndex 0-3 (paired across hands on seed and shape)

| hand | thumb base | episodes | success | lifted | palm | contacts | reach fail | drops (lift phase / before first pulse / during / between / after) | spawned tilted >= 10° | success, upright spawns only |
|---|---|---|---|---|---|---|---|---|---|---|
| 5 | masked | 398 | 0.080 [0.058, 0.111] (n=398) | 0.143 | 0.724 | 1.71 | 0.643 | 86 / 19 / 1 / 2 / 1 | 98.0% | 0.750 [0.409, 0.929] (n=8) |
| 11 | masked | 397 | 0.063 [0.043, 0.091] (n=397) | 0.144 | 0.791 | 1.60 | 0.718 | 54 / 25 / 1 / 6 / 0 | 98.2% | 0.429 [0.158, 0.750] (n=7) |
| 14 | masked | 398 | 0.168 [0.135, 0.208] (n=398) | 0.276 | 0.714 | 2.17 | 0.580 | 56 / 22 / 4 / 14 / 2 | 98.5% | 0.667 [0.300, 0.903] (n=6) |
| 1 | active | 399 | 0.246 [0.206, 0.290] (n=399) | 0.328 | 0.784 | 2.52 | 0.599 | 29 / 21 / 1 / 8 / 1 | 94.7% | 0.667 [0.454, 0.828] (n=21) |
| 2 | active | 399 | 0.281 [0.239, 0.327] (n=399) | 0.388 | 0.832 | 2.91 | 0.514 | 40 / 23 / 2 / 13 / 4 | 97.0% | 0.667 [0.391, 0.862] (n=12) |
| 3 | active | 400 | 0.168 [0.134, 0.207] (n=400) | 0.240 | 0.703 | 2.02 | 0.590 | 66 / 21 / 1 / 5 / 2 | 96.8% | 0.692 [0.424, 0.873] (n=13) |

Per hand x e1 family (success; n per cell):

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | hard mean | easy mean | hard − easy |
|---|---|---|---|---|---|---|---|---|---|
| e1 0.3-0.7 | 0.08 (165) | 0.07 (165) | 0.17 (165) | 0.19 (165) | 0.28 (165) | 0.17 (165) | 0.11 | 0.21 | -0.11 |
| e1 0.7-1.1 | 0.11 (105) | 0.11 (104) | 0.24 (105) | 0.34 (104) | 0.35 (105) | 0.17 (105) | 0.15 | 0.29 | -0.13 |
| e1 1.1-1.5 | 0.05 (128) | 0.02 (128) | 0.11 (128) | 0.25 (130) | 0.22 (129) | 0.16 (130) | 0.06 | 0.21 | -0.15 |

Per hand x e2 family (success; n per cell):

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | hard mean | easy mean | hard − easy |
|---|---|---|---|---|---|---|---|---|---|
| e2 0.3-0.7 | 0.09 (135) | 0.04 (134) | 0.19 (135) | 0.21 (134) | 0.30 (135) | 0.21 (135) | 0.10 | 0.24 | -0.13 |
| e2 0.7-1.1 | 0.06 (124) | 0.06 (124) | 0.14 (124) | 0.22 (125) | 0.20 (125) | 0.10 (125) | 0.08 | 0.17 | -0.09 |
| e2 1.1-1.5 | 0.09 (139) | 0.09 (139) | 0.18 (139) | 0.31 (140) | 0.34 (139) | 0.19 (140) | 0.12 | 0.28 | -0.15 |

Per hand x cross-section family (success; n per cell):

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | hard mean | easy mean | hard − easy |
|---|---|---|---|---|---|---|---|---|---|
| cross-section 50-65 mm | 0.06 (103) | 0.05 (103) | 0.13 (103) | 0.23 (105) | 0.25 (104) | 0.12 (105) | 0.08 | 0.20 | -0.12 |
| cross-section 65-80 mm | 0.08 (250) | 0.06 (249) | 0.19 (250) | 0.25 (249) | 0.28 (250) | 0.18 (250) | 0.11 | 0.24 | -0.12 |
| cross-section < 50 mm | 0.11 (45) | 0.09 (45) | 0.16 (45) | 0.27 (45) | 0.38 (45) | 0.18 (45) | 0.12 | 0.27 | -0.16 |

Per hand x length family (success; n per cell):

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | hard mean | easy mean | hard − easy |
|---|---|---|---|---|---|---|---|---|---|
| length 130-210 mm | 0.12 (184) | 0.07 (183) | 0.20 (184) | 0.32 (184) | 0.36 (184) | 0.20 (185) | 0.13 | 0.29 | -0.16 |
| length 210-280 mm | 0.11 (85) | 0.13 (85) | 0.31 (85) | 0.35 (85) | 0.45 (85) | 0.27 (85) | 0.18 | 0.36 | -0.18 |
| length < 130 mm | 0.00 (129) | 0.01 (129) | 0.03 (129) | 0.08 (130) | 0.06 (130) | 0.05 (130) | 0.01 | 0.06 | -0.05 |

Per hand x aspect family (success; n per cell):

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | hard mean | easy mean | hard − easy |
|---|---|---|---|---|---|---|---|---|---|
| aspect 1.5-3 | 0.08 (223) | 0.05 (222) | 0.16 (223) | 0.21 (224) | 0.25 (224) | 0.14 (225) | 0.10 | 0.20 | -0.10 |
| aspect < 1.5 | 0.00 (45) | 0.00 (45) | 0.00 (45) | 0.07 (45) | 0.02 (45) | 0.04 (45) | 0.00 | 0.04 | -0.04 |
| aspect >= 3 | 0.11 (130) | 0.11 (130) | 0.24 (130) | 0.37 (130) | 0.43 (130) | 0.25 (130) | 0.15 | 0.35 | -0.20 |

Per-shape paired difference (mean over the 3 hard hands minus mean over the 3 easy hands, same seeds and shapes), worst 15 shapes for the hard hands:

| shape | hard mean | easy mean | diff | cross mm | length mm | e1 | e2 |
|---|---|---|---|---|---|---|---|
| 1 | 0.00 | 0.22 | -0.22 | 62 | 145 | 1.34 | 1.44 |
| 3 | 0.00 | 0.00 | +0.00 | 43 | 81 | 0.61 | 0.93 |
| 7 | 0.00 | 0.07 | -0.07 | 69 | 85 | 0.51 | 0.58 |
| 10 | 0.00 | 0.00 | +0.00 | 67 | 116 | 1.29 | 0.79 |
| 14 | 0.00 | 0.13 | -0.13 | 78 | 90 | 0.77 | 0.74 |
| 15 | 0.00 | 0.27 | -0.27 | 53 | 196 | 1.15 | 0.59 |
| 18 | 0.00 | 0.07 | -0.07 | 66 | 88 | 1.05 | 1.06 |
| 19 | 0.00 | 0.07 | -0.07 | 71 | 121 | 0.45 | 0.42 |
| 24 | 0.00 | 0.00 | +0.00 | 71 | 106 | 0.43 | 1.21 |
| 27 | 0.00 | 0.00 | +0.00 | 48 | 83 | 1.12 | 0.77 |
| 29 | 0.00 | 0.20 | -0.20 | 77 | 124 | 1.24 | 0.47 |
| 30 | 0.00 | 0.00 | +0.00 | 64 | 119 | 1.30 | 1.18 |
| 31 | 0.00 | 0.20 | -0.20 | 69 | 276 | 1.21 | 0.35 |
| 35 | 0.00 | 0.27 | -0.27 | 44 | 160 | 1.02 | 1.23 |
| 44 | 0.00 | 0.00 | +0.00 | 79 | 82 | 1.05 | 1.01 |

Across the 80 shapes: mean hard 0.103, mean easy 0.231, mean difference -0.128; shapes where hard < easy by >= 0.2: 30; shapes where hard > easy by >= 0.2: 0; shapes where all six hands scored 1.0: 0.

### Reading of 2d (mechanical, with the numbers)

The thumb-base deficit survives the change of object and is, if anything, larger in relative terms. Over the 80 shapes the three
thumb-base-masked hands average 0.103 success against 0.231 for the three easy hands (paired on seed and shape, mean difference
−0.128); on 30 of 80 shapes the hard hands are below the easy hands by 0.2 or more and on none is it the other way round; on the
anchor cylinder at perturbation 1.0 the same hands score 0.91-0.98 against 1.00 (`results/bo/partB/probe.csv`), i.e. the ratio
hard / easy falls from ~0.95 on the cylinder to ~0.45 on the bank. The interaction is with feasibility, not with a particular family:
the gap is widest where the easy hands can still grasp, on long, high-aspect objects (aspect ≥ 3: 0.15 vs 0.35, −0.20; length 210-280
mm: 0.18 vs 0.36, −0.18) and on the rounded-diamond and rounded-square cross-sections (−0.15, −0.13), and it disappears on short,
puck-like objects (aspect < 1.5: 0.00 vs 0.04; length < 130 mm: 0.01 vs 0.06) because nobody grasps those. Mechanically the hard
hands fail on the bank the way they fail on the cylinder, only more often: they meet the gate less (reach-failure rate 0.64 / 0.72 /
0.58 for hands 5 / 11 / 14 against 0.60 / 0.51 / 0.59 for hands 1 / 2 / 3), hold fewer segments at the end (1.6-2.2 against
2.0-2.9), and when they do gate they lose the object in the lift phase (86 / 54 / 56 lift-phase drops against 29 / 40 / 66): without a
driven CMC the thumb cannot oppose a lying bar or a short block any better than an upright cylinder, and a lying object removes the
palm-low / fingertips-high posture that let the pinch hold the cylinder. Two cautions. First, the per-hand totals carry the pass-level
spread seen everywhere in this file (hand 2's four passes: 0.24 / 0.44 / 0.11 / 0.33; hand 5's: 0.13 / 0.01 / 0.04 / 0.14; hand 3, an
easy hand, ends at 0.168, equal to hand 14), so only the pooled paired contrast is a result; the family cells with n ≈ 45 are not.
Second, 95-98.5 % of grid episodes start with the object tilted (hard hands 98 %, easy 95-97 %, because more drops mean more lying
starts), and the upright-only cells (n = 6-21 per hand: 0.43-0.75) are too small to say whether the deficit changes on an upright
bank; that is the first measurement to make once a spawn-orientation rule exists.

## 2e. Wrist-pronation pass

Hand 14 with k_wristPron 1.34 -> 3.16 N m/rad (training-distribution median), everything else as hand 14, anchor object, perturb 2.0 / mass 0.4-3.0 kg, seeds 4001-4100, deterministic head, one worker: success 0.800 [0.711, 0.867], lifted 0.94, palm 0.48, reach failures 0, ends {'success': 80, 'drop': 20}. Hand-14 baselines on the same block and level: 0.57 (probe pass) and 0.64 (cross-session reproduction, HARD_HANDS.md); band ±0.07.

**One line.** Moving hand 14's wrist-pronation stiffness from 1.34 to 3.16 N m/rad (the training median) raises its success at perturbation 2.0 /
0.4-3.0 kg from 0.57 / 0.64 (probe pass / cross-session reproduction) to **0.80 [0.71, 0.87]**, a move of +0.16 to +0.23, outside
the ±0.07 band; reach failures fall from 10 / 7 to 0 and lifted rises to 0.94 while palm contact stays at 0.48 (the thumb-base move
gave 0.98), so the pronation spring acts on holding the lifted object rather than on forming the grasp: a second theta axis with an
effect above noise, after the thumb-base bit.

## Files

All passes: deterministic head, one job worker, per-episode reseed, K = 50, mu 1.0, run-012 model; log `passes.json`, driver `tools/objects/run_zero_shot.py`, bank `Assets/Objects/Bank/bank.csv`.

- `anchor_default_7001_7300.csv`: anchor_default_7001_7300, n 300, success 0.980 [0.957, 0.991], reach failures 1, hash 4347a765ba1334e4
- `anchor_fixed0_7001_7300.csv`: anchor_fixed0_7001_7300, n 300, success 0.993 [0.976, 0.998], reach failures 0, hash 95d98f753b51b261
- `anchor_default_repeat_7001_7300.csv`: anchor_default_repeat_7001_7300, n 300, success 0.993 [0.976, 0.998], reach failures 0, hash 29973fb966b2be03
- `train_8001_8300_p0.csv`: train_8001_8300_p0, n 299, success 0.234 [0.190, 0.285], reach failures 153, hash 8f956b1588019974
- `train_8001_8300_p1.csv`: train_8001_8300_p1, n 299, success 0.338 [0.287, 0.393], reach failures 133, hash d286d2b685c49708
- `train_8001_8300_p2.csv`: train_8001_8300_p2, n 300, success 0.220 [0.177, 0.270], reach failures 172, hash 23c3a3a8f9f5549c
- `heldout_8001_8300_p0.csv`: heldout_8001_8300_p0, n 300, success 0.017 [0.007, 0.038], reach failures 261, hash b5bf37861ed56c43
- `heldout_8001_8300_p1.csv`: heldout_8001_8300_p1, n 300, success 0.197 [0.156, 0.245], reach failures 163, hash a0e8180b5d9390f5
- `grid_h05_8301_8400_p0.csv`: grid_h05_8301_8400_p0, n 99, success 0.131 [0.078, 0.212], reach failures 58, hash df00bb712b7c8199
- `grid_h05_8301_8400_p1.csv`: grid_h05_8301_8400_p1, n 100, success 0.010 [0.002, 0.054], reach failures 71, hash 56f4b8bb37493d07
- `grid_h05_8301_8400_p2.csv`: grid_h05_8301_8400_p2, n 100, success 0.040 [0.016, 0.098], reach failures 77, hash 8ec014702134c5eb
- `grid_h05_8301_8400_p3.csv`: grid_h05_8301_8400_p3, n 99, success 0.141 [0.086, 0.223], reach failures 50, hash 6d3f48762340d42d
- `grid_h11_8301_8400_p0.csv`: grid_h11_8301_8400_p0, n 99, success 0.030 [0.010, 0.085], reach failures 77, hash da151813bea529fe
- `grid_h11_8301_8400_p1.csv`: grid_h11_8301_8400_p1, n 100, success 0.100 [0.055, 0.174], reach failures 63, hash 8dd223b6a094ab57
- `grid_h11_8301_8400_p2.csv`: grid_h11_8301_8400_p2, n 99, success 0.040 [0.016, 0.099], reach failures 81, hash 0d0c133bf4f7a855
- `grid_h11_8301_8400_p3.csv`: grid_h11_8301_8400_p3, n 99, success 0.081 [0.042, 0.151], reach failures 64, hash ce3eaae30835cfb8
- `grid_h14_8301_8400_p0.csv`: grid_h14_8301_8400_p0, n 99, success 0.162 [0.102, 0.247], reach failures 59, hash 81790d570e87da1d
- `grid_h14_8301_8400_p1.csv`: grid_h14_8301_8400_p1, n 100, success 0.220 [0.150, 0.311], reach failures 50, hash 5fd2c3426e503e18
- `grid_h14_8301_8400_p2.csv`: grid_h14_8301_8400_p2, n 100, success 0.160 [0.101, 0.244], reach failures 68, hash a2441d4d2324d4e5
- `grid_h14_8301_8400_p3.csv`: grid_h14_8301_8400_p3, n 99, success 0.131 [0.078, 0.212], reach failures 54, hash 093baa663830ddcb
- `grid_h01_8301_8400_p0.csv`: grid_h01_8301_8400_p0, n 100, success 0.110 [0.063, 0.186], reach failures 81, hash 0d584fea12b41886
- `grid_h01_8301_8400_p1.csv`: grid_h01_8301_8400_p1, n 100, success 0.000 [0.000, 0.037], reach failures 86, hash 0baca172f117fd03
- `grid_h01_8301_8400_p2.csv`: grid_h01_8301_8400_p2, n 99, success 0.354 [0.266, 0.452], reach failures 41, hash 03499abc9de1ea3a
- `grid_h01_8301_8400_p3.csv`: grid_h01_8301_8400_p3, n 100, success 0.520 [0.423, 0.615], reach failures 31, hash dbf0c32f80a6131c
- `grid_h02_8301_8400_p0.csv`: grid_h02_8301_8400_p0, n 99, success 0.242 [0.169, 0.335], reach failures 57, hash e025a9dc403879b6
- `grid_h02_8301_8400_p1.csv`: grid_h02_8301_8400_p1, n 100, success 0.440 [0.347, 0.538], reach failures 35, hash bebe26ffe839d5b0
- `grid_h02_8301_8400_p2.csv`: grid_h02_8301_8400_p2, n 100, success 0.110 [0.063, 0.186], reach failures 67, hash 70db632d12a1f80e
- `grid_h02_8301_8400_p3.csv`: grid_h02_8301_8400_p3, n 100, success 0.330 [0.246, 0.427], reach failures 46, hash 9ece10a84c4d20b7
- `grid_h03_8301_8400_p0.csv`: grid_h03_8301_8400_p0, n 100, success 0.320 [0.237, 0.417], reach failures 39, hash 7cc29917f6180149
- `grid_h03_8301_8400_p1.csv`: grid_h03_8301_8400_p1, n 100, success 0.160 [0.101, 0.244], reach failures 60, hash f1cf16a0faa74e31
- `grid_h03_8301_8400_p2.csv`: grid_h03_8301_8400_p2, n 100, success 0.030 [0.010, 0.085], reach failures 80, hash 4ac26d66a10fef6d
- `grid_h03_8301_8400_p3.csv`: grid_h03_8301_8400_p3, n 100, success 0.160 [0.101, 0.244], reach failures 57, hash ccf9ebaeb70aa4b4
- `wrist_h14_kWristPron_median_4001_4100.csv`: wrist_h14_kWristPron_median_4001_4100, n 100, success 0.800 [0.711, 0.867], reach failures 0, hash 2e3d0bef7f97f30f
- `anchor_fixed0_tilt_7001_7300.csv`: anchor_fixed0_tilt_7001_7300, n 300, success 0.990 [0.971, 0.997], reach failures 2, hash 4072bc92a69f1226
