# legacyNoReseed discriminator (2026-09-24)

Question: did the per-episode reseed of commit 1716818 change the measured success of the deployed 012 model on seeds
6001-6100 at mu 1.0 (sampled head), or did the pre-fix readings of 0.83 and 0.90 on this block have another cause?

Setup: `results/012/Prosthetic.onnx` (9bda654f), Editor harness `ArticulatedGates` mode `eval` at commit 0d93fc8 (no code
change for this test), scene Dynamic_Scene, sampled head, one job worker, K = 50, perturbation 1.0, timeScale 20, seeds
6001-6100, mu 1.0. Every measured pass is its own Play session and follows a throwaway pass (seeds 6091-6100). Summaries
by `tools/eval/summarize_pass.py` (zero-step rows excluded, counted as `excluded_zero_step`). Files in this directory:
`legacy_smp_A/B/C.csv`, `newpath_smp_A.csv`, `throwaway_smp.csv`, `discriminator_runs.log`, `discriminator_summary.csv`.

`legacyNoReseed = true` reproduces the pre-1716818 seeding: `UnityEngine.Random.InitState(raw seed)` at the top of every
episode and no `Unity.InferenceEngine.Random.SetSeed` (the sampled head keeps drawing from the package's static stream,
which only a Play start resets). `legacyNoReseed = false` is the current path: `DerivedSeed(seed, passIndex)` for both.

## 2a — legacy seeding, three relaunched Play sessions

| session | n | excluded_zero_step | success [95 % Wilson] | lifted | palm | contacts | mean hold | ends |
|---|---|---|---|---|---|---|---|---|
| legacy_smp_A | 98 | 2 | 0.878 [0.798, 0.929] | 0.990 | 0.878 | 4.58 | 452 | 86 success, 12 drop |
| legacy_smp_B | 98 | 2 | 0.878 [0.798, 0.929] | 0.990 | 0.878 | 4.58 | 452 | 86 success, 12 drop |
| legacy_smp_C | 98 | 2 | 0.878 [0.798, 0.929] | 0.990 | 0.878 | 4.58 | 452 | 86 success, 12 drop |

Rows differing between sessions: 0 / 100 (A = B = C on all 49 columns). The legacy path is reproducible too once the job
system runs one worker: the inference stream restarts from its fixed seed at every Play and the raw-seed `InitState`
is deterministic, so the session-to-session spread of the pre-fix readings (0.83 vs 0.90) came from multithreaded PhysX
contact resolution, not from the seeding. Counting the two MaxStep timeouts as failures gives 0.86.

## 2b — current seeding, one session

| session | n | excluded_zero_step | success [95 % Wilson] | lifted | palm | contacts | mean hold | ends |
|---|---|---|---|---|---|---|---|---|
| newpath_smp_A | 100 | 0 | 0.960 [0.902, 0.984] | 1.000 | 0.890 | 4.81 | 481 | 96 success, 4 drop |

Identical to the post-fix reference `results/012/checks/determinism/repro2_sampled_A.csv` and `_B.csv` on all 45 common
columns (100 / 100 rows each; the reference files predate the `maxStepTimeout` and phase columns and ran with the default
worker count). No instrument regression.

## Verdict: LEGACY-LOW

Legacy 0.878 (86 / 98) against 0.960 (96 / 100) on the same nominal seed block: difference 0.082, two-proportion
z = 2.1 (p about 0.03). The legacy value sits in the pre-fix cluster (0.83, 0.90); the reseeded value reproduces the
post-fix reference.

Which column differs systematically: none of the outcome columns can be compared row by row, because the two seedings
produce different episodes by construction. Theta-identical rows between legacy_smp_A and newpath_smp_A: 0 / 100
(mass, stiffness, mask and length scales all differ on every row), so the legacy block is a different sample of the same
theta distribution, and the pulse schedule differs on every row as well. Distribution-level differences between the two
samples of 100: mean object mass 0.644 kg (legacy) vs 0.707 kg (current; the log-uniform 0.2-1.5 kg mean is 0.645),
mean first-pulse hold step 247 vs 279 (each shift is about two standard errors), mean transition step 128 vs 95,
mean hold steps 443 vs 481, phaseAtBegin equal on 10 / 100 rows (chance level). Nothing implicates agent code: the two
paths differ only in harness-side draws (which `UnityEngine.Random` stream seeds theta, mass, spawn and pulses, and
whether the sampled head's noise stream is reset per episode). Whether the 0.08 gap is the seed stream (consecutive raw
seeds 6001-6100 as `InitState` values give a harder theta / pulse sample) or the un-reset inference stream cannot be
separated with the existing switch, which changes both at once; the n = 300 sampled-head value on 7001-7300 (0.92) lies
between the two. Not fixed, per the brief; the reseeded path stays the instrument.
