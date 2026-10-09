# Run 014 C/D evaluation (2026-10-08)

The model is `results/014/CD/Prosthetic_CD.onnx`, the 014_r2 checkpoint at step 32999885, sha256 `68d86b813cc8ab671696f297c53a7e7f08ad2c091a3f0203d4ddbbfdaea8a9cf`. It was compared with:
- 014 r2 at 24M (`results/014/Prosthetic.onnx`, 164d4273…);
- 013E (8a3097a9…).

The scripts are `tools/eval/eval_014.py` (stages `cdpair`, `cd1`, `cd2`, `cd3` and `cd4`) and `tools/eval/report_014.py cd`. The earlier blocks are unchanged. The log is `run_cd.log`, and every pass is recorded in `passes.json`.

## Setup

- **Harness:** Editor ArticulatedGates via `tools/bo/gates_runner.run_pass`, with:
  - spawnStream reseeding;
  - forceSpawnEnvelope in both layouts (the 013E legacy layout and the 014 slots layout);
  - perturbation 1.0;
  - held-out shapes 65–80.
- **Seeds:**
  - held-out cells: **12701–13000** (fresh, never used before this eval);
  - anchor cylinder: 7001–7300.
- **Heads:** deterministic for the headline. Sampled heads run alongside for 014CD in block 1.
- **Lift budget:** 300 for the headline. Budget 200 was also run for the 5-digit cells (014CD and 013E) and for the 014CD anchor.
- **Completion:** 46 policy passes plus 16 spawn-only cdpair passes, all DONE with n = 300 and exit 0.

## Placement / containment check on the new seeds (cdpair, spawn only, no policy step)

Disagreement is defined as in `CONTAIN.md`: the envelope chose an attempt that 013E's own filter rejects (`chosen ≥ 0` and (`handFirst < 0` or `handFirst > chosen`)). In the legacy 013E rows, `handFirst` is 013E's own filter. In a slots row, `handFirst` is that composition's own hand filter, shown for information only.

| cell | rows | disagreements vs own filter | fallbacks | placement identical to 013E (object, pose, x/y/z) | own filter picks same attempt |
|---|---|---|---|---|---|
| upright 013E (legacy) | 300 | **0** | 0 | — | 299 |
| upright slots 3 / 4 / 5 / 6a / 6b / 7 | 300 each | 0 / 0 / 0 / 0 / 2 / 0 | 0 | 300 / 300 each | 299 / 299 / 299 / 299 / 298 / 300 |
| lying 013E (legacy) | 300 | **0** | 0 | — | 257 |
| lying slots 3 / 4 / 5 / 6a / 6b / 7 | 300 each | 0 each | 0 | 300 / 300 each | 257 / 257 / 257 / 278 / 261 / 298 |
| anchor 013E (legacy) | 300 | **0** | 0 | — | 295 |
| anchor slots 5 | 300 | 0 | 0 | 300 / 300 | 295 |

**What this shows:**
- The pairing condition from `CONTAIN.md` holds, so all 300 rows are used in every 014CD − 013E comparison:
  - 0 rows where the envelope accepts a placement that 013E's own filter rejects;
  - 0 fallbacks;
  - identical placements in every layout and composition.
- The 2 rows in 6b canonical are informational: the envelope chose an attempt that the 6b hand's own filter would have rejected first. Both layouts use the same envelope-chosen placement, so no pairing is affected.

**Policy-pass placement check:**
- `report_014 cd` compares object index, pose mode, rest-pose index and spawn yaw row by row, for every paired pass.
- **43 pass pairs, 0 mismatched rows.**

## Seed ledger

| seeds | use |
|---|---|
| 12701–13000 | cdpair spawn-only passes, then every held-out cell of this eval (cd1, cd2, cd3); now **spent** |
| 7001–7300 | anchor (already spent; reused by design, as in every 013/014 anchor) |

The next fresh held-out block is **13001+**.

## Results (`python -m tools.eval.report_014 cd`, verbatim)

### 014CD, upright (held-out shapes 65–80, seeds 12701–13000, lift budget 300, perturbation 1.0)

| composition | n | success, deterministic [95 % Wilson] | gate | lift | hold entered | end success | end drop | end liftBudget | end taskBudget | end maxStep | success, sampled [95 %] |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 300 | **0.560** [0.503, 0.615] | 0.753 | 0.660 | 0.653 | 0.560 | 0.183 | 0.010 | 0.000 | 0.247 | 0.487 [0.431, 0.543] |
| 4 | 300 | **0.780** [0.730, 0.823] | 0.963 | 0.883 | 0.873 | 0.780 | 0.177 | 0.007 | 0.000 | 0.037 | 0.687 [0.632, 0.737] |
| 5 | 300 | **0.853** [0.809, 0.889] | 0.977 | 0.920 | 0.910 | 0.853 | 0.120 | 0.003 | 0.000 | 0.023 | 0.707 [0.653, 0.755] |
| 6a | 300 | **0.740** [0.688, 0.786] | 0.967 | 0.833 | 0.813 | 0.740 | 0.213 | 0.013 | 0.000 | 0.033 | 0.637 [0.581, 0.689] |
| 6b | 300 | **0.837** [0.791, 0.874] | 0.977 | 0.927 | 0.927 | 0.837 | 0.130 | 0.010 | 0.000 | 0.023 | 0.683 [0.629, 0.733] |
| 7 | 300 | **0.777** [0.726, 0.820] | 0.973 | 0.867 | 0.867 | 0.777 | 0.167 | 0.030 | 0.000 | 0.027 | 0.650 [0.594, 0.702] |

### 014CD, lying (held-out shapes 65–80, seeds 12701–13000, lift budget 300, perturbation 1.0)

| composition | n | success, deterministic [95 % Wilson] | gate | lift | hold entered | end success | end drop | end liftBudget | end taskBudget | end maxStep | success, sampled [95 %] |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 300 | **0.220** [0.177, 0.270] | 0.677 | 0.297 | 0.280 | 0.220 | 0.180 | 0.270 | 0.003 | 0.327 | 0.197 [0.156, 0.245] |
| 4 | 300 | **0.440** [0.385, 0.497] | 0.887 | 0.543 | 0.540 | 0.440 | 0.210 | 0.237 | 0.000 | 0.113 | 0.327 [0.276, 0.382] |
| 5 | 300 | **0.493** [0.437, 0.550] | 0.930 | 0.587 | 0.577 | 0.493 | 0.183 | 0.247 | 0.003 | 0.073 | 0.323 [0.273, 0.378] |
| 6a | 300 | **0.503** [0.447, 0.560] | 0.947 | 0.607 | 0.590 | 0.503 | 0.183 | 0.250 | 0.007 | 0.057 | 0.373 [0.321, 0.429] |
| 6b | 300 | **0.453** [0.398, 0.510] | 0.933 | 0.667 | 0.657 | 0.453 | 0.277 | 0.197 | 0.007 | 0.067 | 0.330 [0.279, 0.385] |
| 7 | 300 | **0.410** [0.356, 0.466] | 0.947 | 0.587 | 0.577 | 0.410 | 0.257 | 0.280 | 0.000 | 0.053 | 0.317 [0.267, 0.371] |

### 014CD: composition minus 5-digit, paired by row (deterministic, lift budget 300)

| pose | composition | n | d success [95 %] | b / c | McNemar p |
|---|---|---|---|---|---|
| upright | 3 | 300 | -0.293 [-0.349, -0.238] | 5 / 93 | 4.52e-22 |
| upright | 4 | 300 | -0.073 [-0.114, -0.033] | 9 / 31 | 0.00068 |
| upright | 6a | 300 | -0.113 [-0.158, -0.069] | 8 / 42 | 1.16e-06 |
| upright | 6b | 300 | -0.017 [-0.064, +0.031] | 24 / 29 | 0.583 |
| upright | 7 | 300 | -0.077 [-0.127, -0.026] | 19 / 42 | 0.00444 |
| lying | 3 | 300 | -0.273 [-0.331, -0.216] | 9 / 91 | 3.32e-18 |
| lying | 4 | 300 | -0.053 [-0.105, -0.002] | 23 / 39 | 0.0559 |
| lying | 6a | 300 | +0.010 [-0.040, +0.060] | 31 / 28 | 0.795 |
| lying | 6b | 300 | -0.040 [-0.097, +0.017] | 32 / 44 | 0.207 |
| lying | 7 | 300 | -0.083 [-0.141, -0.026] | 27 / 52 | 0.00655 |

### 014CD − 014 r2 (24M), paired by row (deterministic, lift budget 300): what C / D did

| pose | composition | n | 014CD success [95 %] | 014 r2 success [95 %] | d (CD − r2) [95 % paired] | b / c | McNemar p | 014CD lift | 014 r2 lift | 014CD drop | 014 r2 drop |
|---|---|---|---|---|---|---|---|---|---|---|---|
| upright | 3 | 300 | 0.560 [0.503, 0.615] | 0.597 [0.540, 0.651] | **-0.037** [-0.094, +0.021] | 33 / 44 | 0.254 | 0.660 | 0.667 | 0.183 | 0.147 |
| upright | 4 | 300 | 0.780 [0.730, 0.823] | 0.873 [0.831, 0.906] | **-0.093** [-0.135, -0.051] | 8 / 36 | 2.54e-05 | 0.883 | 0.930 | 0.177 | 0.100 |
| upright | 5 | 300 | 0.853 [0.809, 0.889] | 0.877 [0.835, 0.909] | **-0.023** [-0.063, +0.016] | 15 / 22 | 0.324 | 0.920 | 0.943 | 0.120 | 0.103 |
| upright | 6a | 300 | 0.740 [0.688, 0.786] | 0.780 [0.730, 0.823] | **-0.040** [-0.087, +0.007] | 20 / 32 | 0.126 | 0.833 | 0.837 | 0.213 | 0.183 |
| upright | 6b | 300 | 0.837 [0.791, 0.874] | 0.887 [0.846, 0.918] | **-0.050** [-0.096, -0.004] | 18 / 33 | 0.0489 | 0.927 | 0.963 | 0.130 | 0.103 |
| upright | 7 | 300 | 0.777 [0.726, 0.820] | 0.793 [0.744, 0.835] | **-0.017** [-0.067, +0.034] | 27 / 32 | 0.603 | 0.867 | 0.887 | 0.167 | 0.153 |
| lying | 3 | 300 | 0.220 [0.177, 0.270] | 0.030 [0.016, 0.056] | **+0.190** [+0.143, +0.237] | 60 / 3 | 9.05e-15 | 0.297 | 0.140 | 0.180 | 0.177 |
| lying | 4 | 300 | 0.440 [0.385, 0.497] | 0.090 [0.063, 0.128] | **+0.350** [+0.294, +0.406] | 107 / 2 | 1.85e-29 | 0.543 | 0.313 | 0.210 | 0.287 |
| lying | 5 | 300 | 0.493 [0.437, 0.550] | 0.167 [0.129, 0.213] | **+0.327** [+0.266, +0.387] | 108 / 10 | 6.45e-22 | 0.587 | 0.470 | 0.183 | 0.357 |
| lying | 6a | 300 | 0.503 [0.447, 0.560] | 0.157 [0.120, 0.202] | **+0.347** [+0.287, +0.406] | 111 / 7 | 3.38e-25 | 0.607 | 0.473 | 0.183 | 0.383 |
| lying | 6b | 300 | 0.453 [0.398, 0.510] | 0.150 [0.114, 0.195] | **+0.303** [+0.237, +0.369] | 110 / 19 | 9.11e-17 | 0.667 | 0.513 | 0.277 | 0.463 |
| lying | 7 | 300 | 0.410 [0.356, 0.466] | 0.130 [0.097, 0.173] | **+0.280** [+0.221, +0.339] | 94 / 10 | 2.87e-18 | 0.587 | 0.500 | 0.257 | 0.427 |

### 014CD − 013E on the 5-digit hand, paired by row (deterministic; 013E legacy layout with the envelope filter forced, all rows)

| cell | lift budget | n | 014CD success [95 %] | 013E success [95 %] | d (CD − 013E) [95 % paired] | b / c | McNemar p | rows identical |
|---|---|---|---|---|---|---|---|---|
| upright | 300 | 300 | 0.853 [0.809, 0.889] | 0.727 [0.674, 0.774] | **+0.127** [+0.066, +0.188] | 65 / 27 | 9.26e-05 | 300 / 300 |
| upright | 200 | 300 | 0.823 [0.776, 0.862] | 0.697 [0.642, 0.746] | **+0.127** [+0.060, +0.193] | 73 / 35 | 0.000326 | 300 / 300 |
| lying | 300 | 300 | 0.493 [0.437, 0.550] | 0.570 [0.513, 0.625] | **-0.077** [-0.144, -0.010] | 42 / 65 | 0.0329 | 300 / 300 |
| lying | 200 | 300 | 0.517 [0.460, 0.573] | 0.557 [0.500, 0.612] | **-0.040** [-0.110, +0.030] | 52 / 64 | 0.307 | 300 / 300 |

### Anchor cylinder 7001–7300, 5-digit hand, deterministic

| model | lift budget | n | success [95 %] | gate | lift | hold | end drop | end maxStep |
|---|---|---|---|---|---|---|---|---|
| 014CD | 300 | 300 | 0.980 [0.957, 0.991] | 1.000 | 0.997 | 0.990 | 0.020 | 0.000 |
| 014CD | 200 | 300 | 0.983 [0.962, 0.993] | 1.000 | 0.997 | 0.997 | 0.017 | 0.000 |
| 014 r2 | 300 | 300 | 0.987 [0.966, 0.995] | 1.000 | 0.997 | 0.997 | 0.013 | 0.000 |
| 013E | 300 | 300 | 0.893 [0.853, 0.923] | 0.993 | 0.907 | 0.907 | 0.100 | 0.007 |

| paired (lift budget 300) | n | d [95 % paired] | b / c | McNemar p |
|---|---|---|---|---|
| 014CD − 014 r2 | 300 | **-0.007** [-0.020, +0.006] | 1 / 3 | 0.625 |
| 014CD − 013E | 300 | **+0.087** [+0.049, +0.125] | 31 / 5 | 1.29e-05 |
| 014 r2 − 013E | 300 | **+0.093** [+0.056, +0.131] | 32 / 4 | 1.94e-06 |

### Placement check (object index, pose mode, rest-pose index, spawn yaw; every paired pass)

43 pass pairs compared, **0 mismatched rows** in total.
