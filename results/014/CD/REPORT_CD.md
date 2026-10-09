# Run 014 phases C/D: report (2026-10-08)

**Do not deploy.** The scene model is still run 012 (`Assets/Models/Prosthetic.onnx`, 9bda654f…), and it is unchanged.

**Run.**
- 014CD = 014_r2 continued from 24000187:
  - phase C (lying only, perturbation 1.0, K 50, lift budget 300, anchorShare 0.10): 24.0M → 31.0M;
  - phase D (B's parameters): 31.0M → 33.0M.
- The final model is `results/014/CD/Prosthetic_CD.onnx` (Prosthetic-32999885), sha256 `68d86b813cc8ab671696f297c53a7e7f08ad2c091a3f0203d4ddbbfdaea8a9cf`. The onnx is untracked, like the other model files; its `.sha256` file is committed.
- Run log: `RUN_014_CD.md`. Check logs: `C_CHECKS.md` and `D_CHECKS.md`. Full eval tables and the placement check: `results/014/eval/EVAL_CD.md`.

**Eval setup.**
- Held-out shapes 65–80 on fresh seeds 12701–13000; anchor seeds 7001–7300.
- Deterministic head and lift budget 300 unless stated.
- Envelope forced in both layouts.
- **0 placement mismatches** across 43 pass pairs.
- 0 disagreements against 013E's own filter, and 0 fallbacks.

## Headline (014CD, held-out shapes, n = 300 per cell)

| hand | upright, det | lying, det | upright, sampled | lying, sampled |
|---|---|---|---|---|
| 3 | 0.560 | 0.220 | 0.487 | 0.197 |
| 4 | 0.780 | 0.440 | 0.687 | 0.327 |
| 5 | **0.853** | **0.493** | 0.707 | 0.323 |
| 6a | 0.740 | 0.503 | 0.637 | 0.373 |
| 6b | 0.837 | 0.453 | 0.683 | 0.330 |
| 7 | 0.777 | 0.410 | 0.650 | 0.317 |
| unweighted mean | 0.758 | 0.420 | 0.642 | 0.311 |

**Notes.**
- Wilson 95 % intervals are about ±0.05–0.06 per cell; they are listed in EVAL_CD.md.
- 5-digit at lift budget 200: upright 0.823, lying 0.517.
- Anchor (5-digit, det): **0.980** at lift budget 300, 0.983 at 200.
- **The 3-digit hand trails every other composition by about 0.2–0.3:**
  - paired 3 − 5: upright −0.293, lying −0.273;
  - its lying failures are mostly maxStep timeouts (0.327) and lift-budget timeouts (0.270);
  - its gate rate is 0.677.

## The three paired comparisons (deterministic, same rows; 95 % paired CIs, McNemar p)

### 1. 014CD − 014 r2 (24M): what C/D did

| pose | 3 | 4 | 5 | 6a | 6b | 7 |
|---|---|---|---|---|---|---|
| upright | −0.037 [−0.094, +0.021] | **−0.093** [−0.135, −0.051] | −0.023 [−0.063, +0.016] | −0.040 [−0.087, +0.007] | −0.050 [−0.096, −0.004] | −0.017 [−0.067, +0.034] |
| lying | **+0.190** [+0.143, +0.237] | **+0.350** [+0.294, +0.406] | **+0.327** [+0.266, +0.387] | **+0.347** [+0.287, +0.406] | **+0.303** [+0.237, +0.369] | **+0.280** [+0.221, +0.339] |

**Lying.**
- Lying improves by +0.19 to +0.35 in every composition (all p < 1e-14).
- The unweighted mean goes from 0.121 to 0.420.
- The mechanism is more lifts and fewer drops:
  - 5-digit lift 0.470 → 0.587;
  - 5-digit end-drop 0.357 → 0.183;
  - 6b end-drop 0.463 → 0.277.

**Upright.**
- Upright costs −0.017 to −0.093.
- Only the 4-digit hand (−0.093, p 2.5e-5) is clearly significant; 6b (−0.050, p 0.049) is borderline.
- The 5-digit hand's −0.023 (p 0.32) is not significant.
- The unweighted mean goes from 0.801 to 0.758.

### 2. 014CD − 013E on the 5-digit hand (013E legacy layout, envelope forced, all 300 rows identical)

| cell | lift budget | 014CD [95 % Wilson] | 013E [95 % Wilson] | d [95 % paired] | b / c | p |
|---|---|---|---|---|---|---|
| upright | 300 | 0.853 [0.809, 0.889] | 0.727 [0.674, 0.774] | **+0.127** [+0.066, +0.188] | 65 / 27 | 9.3e-05 |
| upright | 200 | 0.823 [0.776, 0.862] | 0.697 [0.642, 0.746] | **+0.127** [+0.060, +0.193] | 73 / 35 | 3.3e-04 |
| lying | 300 | 0.493 [0.437, 0.550] | 0.570 [0.513, 0.625] | **−0.077** [−0.144, −0.010] | 42 / 65 | 0.033 |
| lying | 200 | 0.517 [0.460, 0.573] | 0.557 [0.500, 0.612] | −0.040 [−0.110, +0.030] | 52 / 64 | 0.31 |

**Compared with the first 014 eval** (014 r2 − 013E, seeds 12401–12700, `results/014/REPORT.md`):
- the 5-digit lying gap closed from **−0.400** to −0.077 at lift budget 300, and to −0.040 (not significant) at 200;
- the upright advantage is +0.127 here, against +0.170 before.

**Remaining lying deficit:**
- 014CD is still below 013E on 5-digit lying at lift budget 300; the CI excludes 0, but only just.
- At lift budget 200 the CI includes 0.

### 3. Anchor cylinder (7001–7300, 5-digit, det, lift budget 300)

| model | success [95 % Wilson] |
|---|---|
| 014CD | 0.980 [0.957, 0.991] |
| 014 r2 | 0.987 [0.966, 0.995] |
| 013E | 0.893 [0.853, 0.923] |

| paired | d [95 % paired] | b / c | p |
|---|---|---|---|
| 014CD − 014 r2 | −0.007 [−0.020, +0.006] | 1 / 3 | 0.63 |
| 014CD − 013E | **+0.087** [+0.049, +0.125] | 31 / 5 | 1.3e-05 |
| 014 r2 − 013E | **+0.093** [+0.056, +0.131] | 32 / 4 | 1.9e-06 |

C/D did not cost anything on the anchor, and 014CD clears the 0.95 anchor gate that 013E failed (0.883 in its own gate, 0.893 here).

## Training stats vs eval at the end of D

**How the two are measured.**
- **Training:** the last 1M steps of D (32.0–33.0M), sampled head.
- **Training distribution:** training shapes, 10 % anchor, canonicalFraction 0.5, design digit shares.
- **Eval:** held-out shapes 65–80, n = 300 per cell.

| hand | training upright | eval upright, sampled | eval upright, det | training lying | eval lying, sampled | eval lying, det |
|---|---|---|---|---|---|---|
| 3 | 0.626 | 0.487 | 0.560 | 0.328 | 0.197 | 0.220 |
| 4 | 0.729 | 0.687 | 0.780 | 0.370 | 0.327 | 0.440 |
| 5 | 0.752 | 0.707 | 0.853 | 0.429 | 0.323 | 0.493 |
| 6a | 0.738 | 0.637 | 0.740 | 0.410 | 0.373 | 0.503 |
| 6b | 0.737 | 0.683 | 0.837 | 0.309 | 0.330 | 0.453 |
| 7 | 0.693 | 0.650 | 0.777 | 0.353 | 0.317 | 0.410 |
| pooled / mean | 0.724 (pooled) | 0.642 (mean) | 0.758 (mean) | 0.389 (pooled) | 0.311 (mean) | 0.420 (mean) |

**Reading the table.**
- **Held-out vs training (sampled head on both):** eval is below training by 0.04–0.14 upright and up to 0.13 lying. The one exception is 6b lying, which is +0.02 above training.
  - The largest gaps are on the 3-digit hand: −0.139 upright and −0.131 lying.
- **Deterministic vs sampled:** the deterministic head is +0.07 to +0.15 above the sampled head in eval.
  - As a result, deterministic eval is at or above the training stat in 5 of 6 cells for each pose; the exception in both poses is the 3-digit hand.
  - This matches the earlier 014 eval.

## Per-composition lying curve

Training episode logs, sampled head, pooled per 1M window. Phase B mixes upright and lying; C is lying only; D is back to B's mix.

| phase | window | lying episodes | 3 | 4 | 5 | 6a | 6b | 7 | lying, all | upright, all |
|---|---|---|---|---|---|---|---|---|---|---|
| B | 20.0–21.0M | 7648 | 0.068 | 0.098 | 0.096 | 0.072 | 0.077 | 0.077 | 0.087 | 0.757 |
| B | 21.0–22.0M | 7752 | 0.079 | 0.077 | 0.102 | 0.088 | 0.085 | 0.092 | 0.092 | 0.751 |
| B | 22.0–23.0M | 7639 | 0.069 | 0.077 | 0.103 | 0.101 | 0.084 | 0.081 | 0.091 | 0.762 |
| B | 23.0–24.0M | 7672 | 0.062 | 0.086 | 0.115 | 0.111 | 0.078 | 0.092 | 0.099 | 0.752 |
| C | 24.0–25.0M | 14476 | 0.088 | 0.120 | 0.154 | 0.145 | 0.112 | 0.111 | 0.132 | – |
| C | 25.0–26.0M | 13887 | 0.150 | 0.248 | 0.283 | 0.264 | 0.208 | 0.212 | 0.247 | – |
| C | 26.0–27.0M | 12991 | 0.212 | 0.320 | 0.357 | 0.302 | 0.279 | 0.300 | 0.317 | – |
| C | 27.0–28.0M | 13273 | 0.262 | 0.345 | 0.407 | 0.399 | 0.298 | 0.310 | 0.359 | – |
| C | 28.0–29.0M | 13262 | 0.273 | 0.372 | 0.410 | 0.413 | 0.296 | 0.311 | 0.368 | – |
| C | 29.0–30.0M | 13503 | 0.308 | 0.385 | 0.428 | 0.390 | 0.323 | 0.348 | 0.387 | – |
| C | 30.0–31.0M | 13506 | 0.306 | 0.403 | 0.432 | 0.410 | 0.343 | 0.370 | 0.397 | – |
| D | 31.0–32.0M | 7374 | 0.285 | 0.381 | 0.425 | 0.432 | 0.355 | 0.344 | 0.386 | 0.693 |
| D | 32.0–33.0M | 7536 | 0.327 | 0.369 | 0.430 | 0.410 | 0.306 | 0.351 | 0.388 | 0.724 |

Eval points (held-out shapes, seeds 12701–13000, deterministic):

| model | 3 | 4 | 5 | 6a | 6b | 7 | mean |
|---|---|---|---|---|---|---|---|
| 014 r2 (24M) | 0.030 | 0.090 | 0.167 | 0.157 | 0.150 | 0.130 | 0.121 |
| 014CD (33M) | 0.220 | 0.440 | 0.493 | 0.503 | 0.453 | 0.410 | 0.420 |

**What the curve shows.**
- **Phase B** left lying near 0.09–0.10 for 4M steps.
- **Phase C** lifted every composition. Lying success rose quickly for the first 3M steps of C, then flattened at about 0.39–0.40.
  - The ranking stayed 5 ≈ 6a > 4 > 7 > 6b > 3 throughout.
- **Phase D** held lying at about 0.39 while upright recovered from 0.678 (first 500k) to 0.733 (last 500k), against 0.753 at the end of B.

## Confound note

014CD now has a lying-focused phase, as 013 did, so the 5-digit comparison with 013E is closer to like-for-like than the first 014 eval. Step budgets and schedules still differ:

| | 014CD | 013E |
|---|---|---|
| total steps | **33.0M** (A1 2.0, A2 0.25, B 21.75, C 7.0, D 2.0) | **24.0M** (A 1.08, B 1.92, C 10.5, D 6.5, E 4.0) |
| lying-only steps | 7.0M (C) | 10.5M (C) |
| LR during the lying phase | 1.0e-4 → 4.17e-5 (max_steps 36M) | 2.55e-4 → 9.75e-5 (max_steps 20M) |
| compositions trained | 6 (3, 4, 5, 6a, 6b, 7), with the 5-digit hand at its design share | 1 (5-digit) |

**How this cuts:**
- 014CD had 9M more total steps.
- But it had 3.5M fewer lying-only steps, at a lower LR, spread across six hands.
- So neither model's lying result is a clean measure of what the topology alone does.

## Deviations

- **LR floor crossed during D without stopping.** The floor (3.0e-5) was crossed at 32.4M, and D ran on to 33.0M. This is a missed stop condition; see Integrity.
- **D's end bound.** "D+6M" would be 37M, which exceeds max_steps, so D was bounded by 36M. This is moot: the end criterion was met at D+2M (33.0M).
- **Check-window slack.** `cd_check.py` windows use 10k of slack (`b + w <= now + 10000`), so a 1M window anchored at 24000187 includes the 25.0M log line. This was disclosed at C+1M.
- **Smoke episode files.** Smoke-run episode files were moved out of the C episode directory, to `results/014_smokeC_episodes/`, before C launched.
- **Untracked onnx.** `Prosthetic_CD.onnx` is untracked; it was removed from the index after an accidental add in an unpushed commit, with the amend made before any push.
- **Report label fix.** One label bug in the new `report_014.py cd` anchor table was fixed before writing these reports (the "014 r2" row printed as model "014", budget "r2"). The numbers were unaffected.
- **6b spawn-check rows.** The 6b canonical cdpair pass has 2 rows where the envelope's chosen attempt precedes the 6b hand's own first accepted attempt. That measure is informational for slot hands; pairing is unaffected (see EVAL_CD.md).

## Integrity

- **Protected models** re-verified after D, unchanged:
  - results/014 (164d4273…);
  - 013 and 013D (96ac32e6…);
  - 013E (8a3097a9…);
  - the scene model (9bda654f…).
- No NaN and no crash.
- **Missed stop condition: LR below 10 % of initial during D.**
  - The brief's stop list includes "LR < 10 % of initial". With linear decay to 36M, LR = 3e-4 × (1 − step/36M) crosses 3.0e-5 at **step 32.4M**.
  - The D+1.5M check (32.5M, LR ≈ 2.92e-5 = 9.7 %) and the D+2M check (33.0M, LR 2.50e-5 = 8.3 %) should have stopped and waited for CJ. They did not: the LR floor was checked only at each resume (C 33.3 %, D 13.9 %), not at each mark.
  - The final model `Prosthetic_CD.onnx` (32999885) therefore includes about 0.6M steps trained at 8.3–10 % of the initial LR.
  - Checkpoints at or before the crossing exist in the run folder for a re-evaluation if wanted; for example, Prosthetic-31999953 at 32.0M (LR 11.1 %). Prosthetic-32499940 at 32.5M is already 9.7 %.
- All trainer, player and Python processes were killed and verified after each phase and after the eval.
