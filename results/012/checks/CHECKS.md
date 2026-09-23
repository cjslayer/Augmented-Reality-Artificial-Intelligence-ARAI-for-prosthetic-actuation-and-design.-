# Run 012: read-only checks (2026-09-23)

Model `results/012/Prosthetic.onnx` (Prosthetic-12000014, sha256 9bda654f...), deployed as the scene model in commit
6590310 (tag `012-deployed`). Evaluations here ran in the Editor eval harness (`ArticulatedGates` mode `eval`, Editor
assembly unchanged since commit a834d81), sampled head, K = 50, perturbation 1.0, friction applied to the object's
material. The player build `Builds/Run012` is the training build; the eval harness is Editor-only, so "identical build"
below means the identical Editor assembly, scene, model file and harness config.

## 2a. Determinism — verdict: **DIFFERS** (harness is NOT deterministic per build/session)

Straight repeat of the stage-3 fresh-block evaluation (seeds 6001-6100, identical config files except the output path,
`eval_repeat_runs.log`), compared row for row with `results/012/eval/eval012_fresh_mu*.csv`:

| mu | success stage 3 (07:53-07:59) | success repeat (15:10-15:12) | fully identical rows | rows with identical `steps` | first differing seed and columns |
|---|---|---|---|---|---|
| 1.0 | 0.83 | 0.90 | 4 / 100 | 7 / 100 | 6005: maxPenMm, gripTorqueMean, retShaping |
| 0.6 | 0.89 | 0.89 | 1 / 100 | 5 / 100 | 6002: maxPenMm, gripTorqueMean, retShaping |
| 1.5 | 0.79 | 0.66 | 27 / 100 | 28 / 100 | 6028: contacts, distinctFingers, gripTorqueMean, retShaping |

Not byte-identical at any mu. Columns that differ: every outcome column (success, endReason, steps, transitionStep,
stepsToLift, holdSteps, pulses, maxPulseN, contacts, distinctFingers, thumb, palm, forearm, maxPenMm, gripTorqueMean,
retShaping, retPhase, retHold, retBonus, retDrop, pulseStep1-3, lastPulseStep, pulseActiveAtDrop). Theta columns
(mass, kFingerMean, len*, activeGroups, mask, zetaMean, inertiaScaleMean, handSpanRatio) agree on 99 of 100 seeds; the
last row (seed 6100) differs in theta because the harness logs the following unseeded reset's theta for the final
episode (logging bug, outcome columns of that row are correct). Structure of the differences: the two runs are
bit-identical for a contiguous prefix of seeds (1 / 4 / 27 rows), the first divergence is in contact metrics
(penetration, grip torque) while the outcome is still the same, and every later episode differs. The same structure
appears in the 011b files (2c). Reading: theta is re-seeded per episode (Random.InitState in the harness hook) and is
reproducible; PhysX contact resolution occasionally diverges between runs; the policy's sampling stream is not re-seeded
per episode, so once one episode diverges the stream is offset for all later ones.

**This falsifies the bo_eval premise "deterministic per build" for the articulated hand with a sampled head in the
Editor.** The bitwise Editor/player determinism recorded for bo_eval was established on the kinematic run-010 setup and
does not carry over. Per the brief, no fix was attempted and tools/bo_eval was not touched. Practical consequence: a
100-episode success rate moves by up to 0.13 between sessions of the identical configuration (mu 1.5: 0.79 -> 0.66),
so 012's fresh-block numbers should be quoted as 0.89 / 0.83-0.90 / 0.66-0.79 at mu 0.6 / 1.0 / 1.5, about +-0.1.

## 2b. Theta composition of 5001-5100 vs 6001-6100 — verdict: **composition does NOT explain the gap**

Theta is seed-determined: the three mu files of each block agree row for row on every theta column. Block A =
5001-5100 (mu 1.0 file), block B = 6001-6100. Welch t (normal p) and Mann-Whitney U (normal p, tie-corrected):

| quantity | mean A | mean B | sd A | sd B | terciles A (33/67) | terciles B | t | p_t | U | p_U |
|---|---|---|---|---|---|---|---|---|---|---|
| kFingerMean | 1.51 | 1.48 | 0.42 | 0.39 | 1.27 / 1.67 | 1.31 / 1.68 | 0.43 | 0.66 | 5046 | 0.91 |
| activeGroups | 11.25 | 11.21 | 1.51 | 1.38 | 11 / 12 | 11 / 12 | 0.20 | 0.84 | 5105 | 0.79 |
| mass | 0.64 | 0.64 | 0.37 | 0.37 | 0.39 / 0.76 | 0.39 / 0.75 | 0.02 | 0.99 | 5001 | 1.00 |
| lenMean | 1.00 | 0.99 | 0.05 | 0.05 | 0.98 / 1.03 | 0.96 / 1.01 | 2.17 | 0.03 | 5879 | 0.03 |
| lenIndex | 1.02 | 0.98 | 0.10 | 0.12 | 0.98 / 1.07 | 0.92 / 1.04 | 2.19 | 0.03 | 5849 | 0.04 |
| lenMiddle | 1.01 | 0.99 | 0.12 | 0.11 | 0.93 / 1.09 | 0.91 / 1.05 | 1.08 | 0.28 | 5382 | 0.35 |
| lenRing | 0.99 | 0.99 | 0.12 | 0.12 | 0.91 / 1.07 | 0.92 / 1.08 | 0.53 | 0.59 | 5144 | 0.73 |
| lenPinky | 1.00 | 0.99 | 0.11 | 0.12 | 0.93 / 1.07 | 0.91 / 1.06 | 0.61 | 0.54 | 5274 | 0.50 |
| lenThumb | 1.00 | 1.00 | 0.12 | 0.11 | 0.93 / 1.08 | 0.94 / 1.05 | 0.43 | 0.67 | 5170 | 0.68 |

Stiffness, active groups and mass are indistinguishable (p >= 0.66); lenMean and lenIndex differ nominally at p = 0.03
(block A about 0.01-0.04 longer, d = 0.3), which is what nine comparisons produce by chance.

Success by theta bin at mu 1.5 (tercile edges from the pooled 200 episodes), stage-3 session:

| bin | n 5001 | success 5001 | n 6001 | success 6001 | diff |
|---|---|---|---|---|---|
| kFingerMean <= 1.28 | 35 | 0.94 | 32 | 0.78 | +0.16 |
| kFingerMean 1.28-1.67 | 31 | 1.00 | 35 | 0.80 | +0.20 |
| kFingerMean > 1.67 | 34 | 0.94 | 33 | 0.79 | +0.15 |
| activeGroups <= 9 | 13 | 0.92 | 9 | 0.89 | +0.03 |
| activeGroups 10-11 | 41 | 1.00 | 47 | 0.79 | +0.21 |
| activeGroups 12-14 | 46 | 0.93 | 44 | 0.77 | +0.16 |
| mass <= 0.39 | 34 | 0.94 | 33 | 0.91 | +0.03 |
| mass 0.39-0.75 | 32 | 0.97 | 34 | 0.82 | +0.15 |
| mass > 0.75 | 34 | 0.97 | 33 | 0.64 | +0.33 |
| lenMean <= 0.97 | 27 | 0.96 | 40 | 0.75 | +0.21 |
| lenMean 0.97-1.02 | 34 | 1.00 | 34 | 0.79 | +0.21 |
| lenMean > 1.02 | 39 | 0.92 | 26 | 0.85 | +0.08 |

Direct standardisation of the 6001 block to the 5001 bin proportions at mu 1.5 (raw 0.79 vs 0.96): by kFingerMean
0.79, activeGroups 0.79, mass 0.79, lenMean 0.80, mass x activeGroups (9 cells) 0.77. The reverse standardisation
leaves the 5001 block at 0.96. The gap persists in all 12 matched bins (0.03-0.33) and is mu-dependent: 0.03 at mu
0.6, 0.11 at mu 1.0, 0.17 at mu 1.5 (same session). Composition cannot account for it. With 2a in hand the likeliest
reading is session-level run-to-run variation of the same size (the fresh block alone moved 0.79 -> 0.66 at mu 1.5
between sessions), not a property of the seed sets; a third block in a third session would settle it (not run: outside
this brief).

## 2c. 011b baseline reconciliation — verdict: **the corrected numbers are right (0.47-0.48 / 0.53-0.54 / 0.38-0.48); the original 0.59 / 0.51 / 0.52 is invalid as a mu sweep**

Files (all seeds 5001-5100, perturbScale 1.00 everywhere, the `mu` column always equal to the requested value):
ORIGINAL = `results/011_artic2/eval011b_mu{06,10,15}.csv` (22:50 session, before commit a834d81); RE-RUN A =
`results/011b/analysis/eval011b_rerun_mu*.csv` (23:13, pulse logging added, friction still not reaching the material);
RE-RUN B = `..._fixedmu.csv` (23:25, after the friction fix, mu 0.6 and 1.5); PAIRED = `results/012/eval/eval011b_paired_mu*.csv`
(07:51-07:57, stage 3).

**Mechanism.** `ArmGraspAgent.Initialize` builds the object's `PhysicsMaterial("ObjectMu")` once from
`objectFriction` (scene default 1.0) and assigns it to the collider (`ArmGraspAgent.cs:296-299`). The harness's
`EvalSetup` runs later (in its own `Start`) and set only the field `agent.objectFriction`; the CSV's `mu` column and
the `[Gates] eval ... mu=` log line print that field. So before commit a834d81 every eval pass ran at the scene's
mu = 1.0 regardless of the config. Commit a834d81 (2026-09-23, tooling) applies the override to the live material
(`col.sharedMaterial.staticFriction/dynamicFriction`) and logs the material's value; the two `_fixedmu` and all PAIRED
passes carry that log line ("object material mu=0.6 combine=Maximum").

**Reproduction from the files.** Rows identical in all outcome columns, by pair (same seeds):

| pair | identical rows | identical seed range |
|---|---|---|
| RE-RUN A mu 0.6 / 1.0 / 1.5 (all three pairs) | 100 | 5001-5100 (the three files differ only in the `mu` column) |
| ORIGINAL mu 0.6 vs ORIGINAL mu 1.5 | 21 | 5001-5021, diverge at 5022 |
| ORIGINAL mu 0.6 or 1.5 vs PAIRED mu 1.0 | 2 | 5001-5002 (the unoverridden physics equals a real mu 1.0 pass) |
| RE-RUN B mu 1.5 vs PAIRED mu 1.5 | 13 | 5001-5013 |
| RE-RUN B mu 0.6 vs PAIRED mu 0.6 | 10 | 5001-5010 |
| ORIGINAL mu 1.0 vs anything, ORIGINAL vs RE-RUN A per mu, RE-RUN A vs RE-RUN B, RE-RUN B mu 0.6 vs mu 1.5 | 0 | - |

Physics at seed 5001 (steps / maxPenMm / gripTorqueMean) sorts the files into clusters: ORIGINAL mu 0.6, ORIGINAL mu
1.5 and PAIRED mu 1.0 = 622 / 2.40 / 0.332 (one physics: mu 1.0); RE-RUN A x3 = 624 / 2.38 / 0.342; ORIGINAL mu 1.0 =
624 / 2.40 / 0.341; RE-RUN B mu 0.6 and PAIRED mu 0.6 = 287 / 2.59 / 0.198 (no grasp at all at mu 0.6 for this seed);
RE-RUN B mu 1.5 and PAIRED mu 1.5 = 837 / 4.43 / 0.400. So the three ORIGINAL passes were three sessions of one
mu = 1.0 physics, and their 0.59 / 0.51 / 0.52 spread is the session-to-session noise of the sampled policy; after the
fix mu 0.6 and 1.5 are different physics from mu 1.0 and from each other.

Success per file: ORIGINAL 0.59 / 0.51 / 0.52; RE-RUN A 0.54 / 0.54 / 0.54; RE-RUN B 0.48 (mu 0.6), 0.38 (mu 1.5);
PAIRED 0.47 / 0.53 / 0.48. Correct 011b numbers: mu 0.6 = 0.47-0.48, mu 1.0 = 0.51-0.54, mu 1.5 = 0.38-0.48 (the mu 1.5
spread between the two post-fix sessions is the 2a effect).

Per-seed flips at mu 0.6, ORIGINAL (physically mu 1.0) -> RE-RUN B (mu 0.6): 37 flipped, 13 gained (5002, 5004, 5005,
5008, 5010, 5011, 5024, 5027, 5029, 5044, 5056, 5058, 5069: drop -> success), 24 lost (5006, 5009, 5014, 5017, 5021,
5025, 5030, 5032, 5036, 5038, 5040, 5042, 5048, 5053, 5062, 5066, 5068, 5073, 5076, 5080, 5083, 5084, 5085, 5094:
success -> drop, hold steps 0-499). ORIGINAL -> PAIRED at mu 0.6: 40 flipped, 14 gained (5002, 5004, 5005, 5008, 5010,
5011, 5029, 5033, 5051, 5052, 5058, 5060, 5063, 5075), 26 lost (5006, 5009, 5015, 5016, 5019, 5021, 5022, 5025, 5026,
5028, 5030, 5032, 5034, 5036, 5040, 5042, 5048, 5049, 5062, 5073, 5084, 5085, 5089, 5091, 5092, 5097). At mu 1.5,
ORIGINAL -> RE-RUN B: 44 flipped (15 gained: 5004, 5005, 5008, 5034, 5042, 5049, 5058, 5061, 5071, 5073, 5075, 5077,
5084, 5089, 5100; 29 lost: 5012, 5013, 5014, 5017, 5020, 5021, 5023, 5024, 5027, 5028, 5030, 5036, 5037, 5039, 5044,
5048, 5054, 5055, 5063, 5066, 5070, 5074, 5078, 5080, 5081, 5088, 5090, 5094, 5097). For scale, two sessions of the
same mu 1.0 physics also flip 43-45 seeds (ORIGINAL -> RE-RUN A: 23 gained / 20 lost; RE-RUN A -> PAIRED: 22 / 23)
while the rate moves 0.51 -> 0.54 -> 0.53: per-seed flips are dominated by the session effect, not by mu.

Side findings: theta agrees on seeds 5001-5099 across all eleven files; seed 5100 (the last row) carries a different
theta in most files (last-row logging bug, see 2a). 1-6 rows per file end as `maxStep` with `steps = 0` (aborted
episodes after the warm-up reset) and count as failures.

## 2d. Curriculum log of run 012

From the trainer logs (`Temp/run012_phaseA.out`, `Temp/run012.out`; "Parameter ... is in lesson" lines) with the
10k-step summaries around each transition:

| phase | parameter | lesson entered | last summary before (step, mean reward) | first summary after (step, mean reward) |
|---|---|---|---|---|
| A | hold/decisions | Hold10 (K 10) | 1,410,000, 0.73 | 1,420,000, 0.95 |
| A | hold/decisions | Hold20 (K 20) | 1,730,000, 0.73 | 1,740,000, 0.91 |
| A | hold/decisions | Hold50 (K 50) | 1,950,000, 0.67 | 1,960,000, 0.75 |
| B (resumed at 1,999,948) | perturb/scale | Perturb25 (0.25) | 2,670,000, 0.81 | 2,680,000, 0.62 |
| B | perturb/scale | Perturb50 (0.5) | 2,730,000, 0.67 | 2,740,000, 0.91 |
| B | perturb/scale | Perturb75 (0.75) | 2,760,000, 0.84 | 2,770,000, 0.89 |
| B | perturb/scale | Perturb100 (1.0) | 2,760,000, 0.84 | 2,770,000, 0.89 |

The 0.75 lesson ran: it was entered and left between the 2,760,000 and 2,770,000 summaries (consecutive log lines), so
it lasted fewer than 10,000 steps. The whole ladder 0.25 -> 1.0 passed in 90k steps (2.68M -> 2.77M) while the
10k-window mean reward sat at 0.62-0.91 around the 0.9 threshold; the TensorBoard lesson series never sampled lesson 3.
The gate rule (smoothed mean of the 100-episode reward buffer > 0.9, buffer cleared on each change) nominally needs
100 new episodes per lesson, which cannot fit in 10k steps at ~600-800 steps per episode; the mechanism that let
Perturb75 pass within one summary window was not traced (outside this brief). Net effect: the perturbation ladder did
not gate anything; 012 trained at full perturbation from 2.77M, exactly as 011b did from 2.91M.
