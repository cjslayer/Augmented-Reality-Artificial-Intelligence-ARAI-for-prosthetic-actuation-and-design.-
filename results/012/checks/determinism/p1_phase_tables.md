# Part 1 — decision-phase measurement (2026-09-24)

Harness columns `academyStepAtBegin`, `phaseAtBegin` (= Academy.StepCount at the reset mod DecisionPeriod 10; phase 0 = first decision at the next step, phase p = 10 - p zero-action steps first), `warmupSteps` (Academy steps before the first seeded episode). Deterministic head, one job worker, mu 1.0, model results/012/Prosthetic.onnx, scene Dynamic_Scene.

## Runs

| run | condition | seeds | warmupSteps | outcome rows vs family Y (reg1d_det_B) | vs family X (reg1d_det_A) |
|---|---|---|---|---|---|
| p1_recompile_A | launched 7 s after a forced recompile (1a code change) | 6001-6100 | 1 | 100/100 identical | 0/100 |
| p1_later_A | fresh Play, 48 s after the previous pass ended | 6001-6100 | 1 | 100/100 identical | 0/100 |
| p1_recompile_B | launched 6 s after a forced recompile (comment-only edit) | 6001-6100 | 1 | 100/100 identical | 0/100 |
| p1_recompile_C | launched 7 s after a forced recompile (comment-only edit) | 6001-6100 | 1 | 100/100 identical | 0/100 |
| p1_tail_A | seeds 6091-6100 alone | 6091-6100 | 1 | 0/10 identical to the in-pass rows | - |

Family X was not reproduced in three launches within 30 s of a real recompile; every 100-seed pass of the day is family Y. X has no phase table (it was never logged and did not recur).

## Family Y phase sequence (identical in all four 100-seed passes)

phaseAtBegin per episode 1..100:

```
1 6 4 6 7 1 5 8 4 5 4 5 2 4 7 0 5 4 6 5 4 7 5 5 6 5 3 9 6 2 6 5 5 1 3 6 3 3 4 2 4 8 3 6 3 9 4 5 6 2 5 3 4 6 5 7 6 4 6 5 4 4 5 5 4 6 9 5 9 6 6 4 5 3 0 4 7 6 0 1 6 5 4 7 6 3 3 6 5 1 4 5 4 2 1 2 3 3 7 0
```

histogram: 0: 4, 1: 6, 2: 6, 3: 12, 4: 19, 5: 21, 6: 19, 7: 7, 8: 2, 9: 4; Academy step at the first / last reset: 1 / 58660

## Tail run (6091-6100 alone) vs the same seeds inside the 100-seed pass

| seed | phase tail / in-pass | steps tail / in-pass | success tail / in-pass | outcome columns identical |
|---|---|---|---|---|
| 6091 | 1 / 4 | 577 / 571 | 1 / 1 | False |
| 6092 | 8 / 5 | 573 / 569 | 1 / 1 | False |
| 6093 | 1 / 4 | 592 / 588 | 1 / 1 | False |
| 6094 | 3 / 2 | 608 / 619 | 1 / 1 | False |
| 6095 | 1 / 1 | 584 / 591 | 1 / 1 | False |
| 6096 | 5 / 2 | 580 / 591 | 1 / 1 | False |
| 6097 | 5 / 3 | 609 / 600 | 1 / 1 | False |
| 6098 | 4 / 3 | 591 / 594 | 1 / 1 | False |
| 6099 | 5 / 7 | 578 / 573 | 1 / 1 | False |
| 6100 | 3 / 0 | 583 / 576 | 1 / 1 | False |

Seed 6095 begins at phase 1 in both runs and still differs (584 vs 591 steps): with the same seed, the same model, the same head, one job worker and the same decision phase, the outcome depends on the episodes run before it. The decision phase is therefore not the only history channel (PhysX scene persistence across the per-episode rig rebuild remains).

First differing column between the X and Y families (reg1d_det_A vs reg1d_det_B, header order): `steps` on the first episode (seed 6001); every one of the 100 rows differs.

Verdict: INCONCLUSIVE (X not reproduced after the retries); the equal-phase counterexample argues against phase pinning as a sufficient fix.
