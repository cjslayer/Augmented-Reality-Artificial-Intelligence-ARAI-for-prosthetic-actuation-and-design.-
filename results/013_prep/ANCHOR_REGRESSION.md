# 2a. Anchor regression gate (2026-09-27, build 76d92c6)

Question: does the object-bank code path change the physics of the anchor (the scene's cylinder)? Test: the deployed run-012 policy
on seeds 7001-7300, mu 1.0, K = 50, perturbation 1.0, deterministic head, one job worker, passIndex 0, throwaway pass first, compared
row by row with the recorded reference `results/012/eval_n300_v2/eval012_v2_det_mu10.csv` (2026-09-23 16:25) and its two recorded
repeats `repeat_det_mu10_A.csv` (16:32) and `repeat_det_mu10_B.csv` (16:34), all from the same Editor session and build.

## Passes

| pass | object path | success [Wilson 95 %] | lifted | palm | contacts | hold | ends | data-row hash |
|---|---|---|---|---|---|---|---|---|
| reference `eval012_v2_det_mu10` (2026-09-23) | pre-bank build | 1.000 [0.987, 1.000] | 1.000 | 0.967 | 5.57 | 500.0 | 300 success | e2998d1ea5cdb276 |
| reference repeat A (2026-09-23) | pre-bank build | 0.993 [0.976, 0.998] | 0.997 | 0.967 | 5.56 | 496.7 | 298 success, 2 drop | (= B) |
| reference repeat B (2026-09-23) | pre-bank build | 0.993 [0.976, 0.998] | 0.997 | 0.967 | 5.56 | 496.7 | 298 success, 2 drop | (= A) |
| `anchor_default` (this build, 1st long pass after the throwaway) | `objectMode ""` → runtime default anchorOnly, no object columns | 0.980 [0.957, 0.991] | 0.987 | 0.957 | 5.51 | 490.1 | 294 success, 5 drop, 1 reach failure | 4347a765ba1334e4 |
| `anchor_fixed0` (this build) | `fixedIndex 0` through the swap path (`ObjectBank.Apply(0)`), 8 object columns | 0.993 [0.976, 0.998] | 0.997 | 0.967 | 5.56 | 496.7 | 298 success, 2 drop | 95d98f753b51b261 (49 reference columns identical to A / B) |
| `anchor_default_repeat` (this build, consecutive) | runtime default anchorOnly | 0.993 [0.976, 0.998] | 0.997 | 0.967 | 5.56 | 496.7 | 298 success, 2 drop | identical to A / B on all columns |

## Row-level comparison (the 49 reference columns)

- `anchor_fixed0` vs repeat A: **0 of 300 rows differ**; vs repeat B: 0. The anchor through the swap path reproduces the recorded
  repeat passes byte for byte (the two failures are the same episodes, 155 and 224, at the same masses 0.333 and 1.069 kg).
- `anchor_default_repeat` vs `anchor_fixed0` / A / B: 0 of 300 rows differ (whole file byte-identical to A and B).
- `anchor_default` (the first long pass of the session) vs A / B / fixed0: 298 rows differ, first at episode 3
  (`gripTorqueMean` 2.725 vs 2.660, `retShaping` 0.9186 vs 0.9172); 6 success flags differ.
- reference main pass vs A / B: 298 rows differ, first at episode 3 (`contacts` 5 vs 6, `maxPenMm` 2.97 vs 2.92, `gripTorqueMean`
  2.684 vs 2.660); 2 success flags differ. The recorded reference is itself not reproduced by its own recorded repeats.
- Episodes 1 and 2 are identical across all six passes (both builds, both paths).

## Reading

The literal criterion of the brief ("byte-identical to the μ 1.0 pass") cannot be met by any pass, including the record's own
repeats A and B taken seven minutes after the reference in the same session: the reference file is a one-off realisation that
diverges at episode 3 from every later pass. The instrument has a stable realisation (A = B = `anchor_fixed0` = `anchor_default_repeat`,
four passes across two builds and two days) and occasional one-off realisations on the first long pass after a launch (the reference
main pass; this session's first `anchor_default`). Both realisations share episodes 1-2, so the divergence is the episode-chaining
property already documented in `LINEAGE_011b_012.md` and `HARD_HANDS.md`, not a change of physics: a physics change (mesh, scale,
inertia, contact offset, rest height) would show in episode 1.

**Gate result: PASS.** The swap path reproduces the recorded instrument exactly (0 differing rows against the recorded repeats),
the anchorOnly default path reproduces it on its second pass, and the aggregates of every pass sit inside the reference interval
[0.976, 1.000] except the first-pass realisation (0.980, lower bound 0.957), which is the same class of first-pass excursion the
record shows for its own main pass in the other direction. For 2b-2e every measured pass is therefore preceded by the throwaway
and the first long pass of the session (`anchor_default`), so they run in the stable regime.

Files: `passes/throwaway.csv`, `passes/anchor_default_7001_7300.csv`, `passes/anchor_fixed0_7001_7300.csv`,
`passes/anchor_default_repeat_7001_7300.csv`, log `passes.json`.
