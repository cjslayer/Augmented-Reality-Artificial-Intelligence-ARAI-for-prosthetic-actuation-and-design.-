# Lineage 011b -> 012 on one instrument (2026-09-24)

Both scene models re-measured with the same evaluation harness on the same fresh seed block, so that every number in
this file is comparable with every other: `results/011b/Prosthetic.onnx` (run 011b, 6M steps, sha256 0268b0e3, the
baseline tagged `011b-baseline`) and `results/012/Prosthetic.onnx` (run 012, 12M steps, sha256 9bda654f, the deployed
scene model, tag `012-deployed`). Raw passes: `results/011b/eval_n300/` and `results/012/eval_n300_v2/` (run log
`eval_runs.log` in the latter). Tables produced read-only from those CSVs with `tools/eval/summarize_pass.py` and the
session's analysis script.

## Instrument definition

- Editor harness `Assets/Scripts/Diagnostics/ArticulatedGates.cs` mode `eval` at commit 91fce34 (closeout cf40aae,
  phase columns 0d93fc8), scene `Dynamic_Scene`, Unity 6000.3.18f1, ML-Agents 4.1.0, Inference Engine 2.6.1, PhysX
  TGS at 10 ms, DecisionPeriod 10.
- Reproducible unit = one full pass on a fixed seed block: deterministic head (`BehaviorParameters.DeterministicInference`),
  one job-system worker (`jobWorkers: 1`), per-episode reseed of `UnityEngine.Random` and the inference package's
  static stream with `DerivedSeed(seed, passIndex)` (words `seed`, `1000`, `passIndex`), `passIndex` 0, and a throwaway
  pass after every recompile or Editor launch before the measured pass. Passes at different mu on the same block share
  theta, mass, spawn and pulse draws (common random numbers), so mu contrasts and model contrasts are paired.
- Per-episode reproducibility is not claimed: an episode depends on the episodes run before it in the same pass (PhysX
  scene persistence; the decision-phase hypothesis was tested and found insufficient, `results/012/checks/determinism/p1_phase_tables.md`).
- Rows flagged `maxStepTimeout` (ML-Agents MaxStep interruptions, record steps 0: the policy ran MaxStep steps without
  reaching a grasp) stay in the CSV. Convention of this revision (2026-09-24, second revision): only such a row on the
  pass's first episode (a warm-up abort) is excluded (`excluded_zero_step`); every other one is a reach failure, counted
  as a failure and reported as `reach_failures`. No pass in this file has a warm-up abort, so n = 300 everywhere. The
  first revision dropped every zero-step row, which inflated 011b (40 / 58 / 77 such rows per deterministic pass; 57 of
  the 58 at mu 1.0 never transitioned to a grasp); its numbers are kept in a secondary table for the record.
- Sampled head at mu 1.0 on the same block gives the training-time-head figure; it is reseeded per episode, so it is a
  pass-level reproducible number too.
- Task: K = 50 hold decisions, perturbation scale 1.0, friction override applied to the object's physics material.

## Part 3 — n = 300 per cell on seeds 7001-7300 (new hash, one job worker, throwaway pass first)

| model | head | mu | n | excluded_zero_step | success [95 % Wilson] | lifted | palm at end | contacts | mean hold steps | ends |
|---|---|---|---|---|---|---|---|---|---|---|
| 011b | deterministic | 0.60 | 300 | 0 | 0.673 [0.618, 0.724] | 0.867 | 0.873 | 5.15 | 356 | {'drop': 58, 'maxStep': 40, 'success': 202} |
| 011b | deterministic | 1.00 | 300 | 0 | 0.703 [0.649, 0.752] | 0.810 | 0.850 | 5.30 | 363 | {'drop': 31, 'maxStep': 58, 'success': 211} |
| 011b | deterministic | 1.50 | 300 | 0 | 0.597 [0.540, 0.651] | 0.737 | 0.797 | 4.87 | 306 | {'drop': 44, 'maxStep': 77, 'success': 179} |
| 011b | sampled | 1.00 | 300 | 0 | 0.553 [0.497, 0.609] | 0.940 | 0.683 | 3.87 | 321 | {'drop': 119, 'maxStep': 15, 'success': 166} |
| 012 | deterministic | 0.60 | 300 | 0 | 0.980 [0.957, 0.991] | 0.990 | 0.980 | 5.67 | 490 | {'drop': 6, 'success': 294} |
| 012 | deterministic | 1.00 | 300 | 0 | 1.000 [0.987, 1.000] | 1.000 | 0.967 | 5.57 | 500 | {'success': 300} |
| 012 | deterministic | 1.50 | 300 | 0 | 0.993 [0.976, 0.998] | 0.997 | 0.953 | 5.33 | 498 | {'drop': 1, 'maxStep': 1, 'success': 298} |
| 012 | sampled | 1.00 | 300 | 0 | 0.940 [0.907, 0.962] | 0.987 | 0.857 | 4.33 | 475 | {'drop': 16, 'maxStep': 2, 'success': 282} |

### Paired differences (same seeds = same theta, mass, spawn and pulse draws; zero-step rows dropped pairwise; normal 95 % CI on the mean paired difference)

| comparison | mean difference | 95 % CI | n pairs | wins / losses | theta-paired rows |
|---|---|---|---|---|---|
| 012 - 011b, deterministic, mu 0.6 | +0.307 | [+0.253, +0.361] | 300 | 94 / 2 | 300/300 |
| 012 - 011b, deterministic, mu 1.0 | +0.297 | [+0.245, +0.348] | 300 | 89 / 0 | 300/300 |
| 012 - 011b, deterministic, mu 1.5 | +0.397 | [+0.340, +0.453] | 300 | 120 / 1 | 300/300 |
| 012: deterministic - sampled, mu 1.0 | +0.060 | [+0.033, +0.087] | 300 | 18 / 0 | 300/300 |
| 011b: deterministic - sampled, mu 1.0 | +0.150 | [+0.078, +0.222] | 300 | 87 / 42 | 300/300 |
| 012 deterministic: mu 0.6 - mu 1.5 | -0.013 | [-0.032, +0.005] | 300 | 2 / 6 | 300/300 |
| 011b deterministic: mu 0.6 - mu 1.5 | +0.077 | [+0.006, +0.147] | 300 | 70 / 47 | 300/300 |

### Secondary table, for the record: the exclusion convention of the first LINEAGE revision (every zero-step row dropped) against the primary convention (reach failures counted)

| model | head | mu | n_total | zero-step rows | success, zero-step rows excluded [Wilson] | success, primary convention [Wilson] |
|---|---|---|---|---|---|---|
| 011b | deterministic | 0.60 | 300 | 40 | 0.777 [0.722, 0.823] | 0.673 [0.618, 0.724] |
| 011b | deterministic | 1.00 | 300 | 58 | 0.872 [0.824, 0.908] | 0.703 [0.649, 0.752] |
| 011b | deterministic | 1.50 | 300 | 77 | 0.803 [0.746, 0.850] | 0.597 [0.540, 0.651] |
| 011b | sampled | 1.00 | 300 | 15 | 0.582 [0.524, 0.638] | 0.553 [0.497, 0.609] |
| 012 | deterministic | 0.60 | 300 | 0 | 0.980 [0.957, 0.991] | 0.980 [0.957, 0.991] |
| 012 | deterministic | 1.00 | 300 | 0 | 1.000 [0.987, 1.000] | 1.000 [0.987, 1.000] |
| 012 | deterministic | 1.50 | 300 | 1 | 0.997 [0.981, 0.999] | 0.993 [0.976, 0.998] |
| 012 | sampled | 1.00 | 300 | 2 | 0.946 [0.915, 0.967] | 0.940 [0.907, 0.962] |


## Part 4 — per-theta, per-phase and drop-phase tables

### 012 (deterministic, mu 1.0, n = 300)

| stiffness tercile (kFingerMean; cuts 1.320, 1.648) | n | success [95 % Wilson] |
|---|---|---|
| low | 100 | 1.000 [0.963, 1.000] |
| mid | 100 | 1.000 [0.963, 1.000] |
| high | 100 | 1.000 [0.963, 1.000] |

stiffness tercile (kFingerMean; cuts 1.320, 1.648): no bin difference exceeds its interval.

| active groups | n | success [95 % Wilson] |
|---|---|---|
| <= 9 | 30 | 1.000 [0.886, 1.000] |
| 10-11 | 116 | 1.000 [0.968, 1.000] |
| 12-14 | 154 | 1.000 [0.976, 1.000] |

active groups: no bin difference exceeds its interval.

| mass tercile (cuts 0.374, 0.802 kg) | n | success [95 % Wilson] |
|---|---|---|
| low | 100 | 1.000 [0.963, 1.000] |
| mid | 100 | 1.000 [0.963, 1.000] |
| high | 100 | 1.000 [0.963, 1.000] |

mass tercile (cuts 0.374, 0.802 kg): no bin difference exceeds its interval.

| lenIndex tercile (cuts 0.927, 1.075) | n | success [95 % Wilson] |
|---|---|---|
| low | 100 | 1.000 [0.963, 1.000] |
| mid | 100 | 1.000 [0.963, 1.000] |
| high | 100 | 1.000 [0.963, 1.000] |

lenIndex tercile (cuts 0.927, 1.075): no bin difference exceeds its interval.

| lenMiddle tercile (cuts 0.937, 1.056) | n | success [95 % Wilson] |
|---|---|---|
| low | 97 | 1.000 [0.962, 1.000] |
| mid | 103 | 1.000 [0.964, 1.000] |
| high | 100 | 1.000 [0.963, 1.000] |

lenMiddle tercile (cuts 0.937, 1.056): no bin difference exceeds its interval.

| lenRing tercile (cuts 0.935, 1.069) | n | success [95 % Wilson] |
|---|---|---|
| low | 100 | 1.000 [0.963, 1.000] |
| mid | 100 | 1.000 [0.963, 1.000] |
| high | 100 | 1.000 [0.963, 1.000] |

lenRing tercile (cuts 0.935, 1.069): no bin difference exceeds its interval.

| lenPinky tercile (cuts 0.926, 1.069) | n | success [95 % Wilson] |
|---|---|---|
| low | 100 | 1.000 [0.963, 1.000] |
| mid | 100 | 1.000 [0.963, 1.000] |
| high | 100 | 1.000 [0.963, 1.000] |

lenPinky tercile (cuts 0.926, 1.069): no bin difference exceeds its interval.

| lenThumb tercile (cuts 0.943, 1.063) | n | success [95 % Wilson] |
|---|---|---|
| low | 99 | 1.000 [0.963, 1.000] |
| mid | 101 | 1.000 [0.963, 1.000] |
| high | 100 | 1.000 [0.963, 1.000] |

lenThumb tercile (cuts 0.943, 1.063): no bin difference exceeds its interval.


### 011b (deterministic, mu 1.0, n = 300)

| stiffness tercile (kFingerMean; cuts 1.320, 1.648) | n | success [95 % Wilson] |
|---|---|---|
| low | 100 | 0.680 [0.583, 0.763] |
| mid | 100 | 0.780 [0.689, 0.850] |
| high | 100 | 0.650 [0.553, 0.736] |

stiffness tercile (kFingerMean; cuts 1.320, 1.648): no bin difference exceeds its interval.

| active groups | n | success [95 % Wilson] |
|---|---|---|
| <= 9 | 30 | 0.567 [0.392, 0.726] |
| 10-11 | 116 | 0.647 [0.556, 0.728] |
| 12-14 | 154 | 0.773 [0.700, 0.832] |

active groups: no bin difference exceeds its interval.

| mass tercile (cuts 0.374, 0.802 kg) | n | success [95 % Wilson] |
|---|---|---|
| low | 100 | 0.660 [0.563, 0.745] |
| mid | 100 | 0.720 [0.625, 0.799] |
| high | 100 | 0.730 [0.636, 0.807] |

mass tercile (cuts 0.374, 0.802 kg): no bin difference exceeds its interval.

| lenIndex tercile (cuts 0.927, 1.075) | n | success [95 % Wilson] |
|---|---|---|
| low | 100 | 0.700 [0.604, 0.781] |
| mid | 100 | 0.710 [0.615, 0.790] |
| high | 100 | 0.700 [0.604, 0.781] |

lenIndex tercile (cuts 0.927, 1.075): no bin difference exceeds its interval.

| lenMiddle tercile (cuts 0.937, 1.056) | n | success [95 % Wilson] |
|---|---|---|
| low | 97 | 0.670 [0.572, 0.756] |
| mid | 103 | 0.709 [0.615, 0.788] |
| high | 100 | 0.730 [0.636, 0.807] |

lenMiddle tercile (cuts 0.937, 1.056): no bin difference exceeds its interval.

| lenRing tercile (cuts 0.935, 1.069) | n | success [95 % Wilson] |
|---|---|---|
| low | 100 | 0.660 [0.563, 0.745] |
| mid | 100 | 0.700 [0.604, 0.781] |
| high | 100 | 0.750 [0.657, 0.825] |

lenRing tercile (cuts 0.935, 1.069): no bin difference exceeds its interval.

| lenPinky tercile (cuts 0.926, 1.069) | n | success [95 % Wilson] |
|---|---|---|
| low | 100 | 0.730 [0.636, 0.807] |
| mid | 100 | 0.660 [0.563, 0.745] |
| high | 100 | 0.720 [0.625, 0.799] |

lenPinky tercile (cuts 0.926, 1.069): no bin difference exceeds its interval.

| lenThumb tercile (cuts 0.943, 1.063) | n | success [95 % Wilson] |
|---|---|---|
| low | 99 | 0.747 [0.654, 0.823] |
| mid | 101 | 0.653 [0.557, 0.739] |
| high | 100 | 0.710 [0.615, 0.790] |

lenThumb tercile (cuts 0.943, 1.063): no bin difference exceeds its interval.


### 011b: reach-failure rate by theta bin (deterministic, mu 0.6 / 1.0 / 1.5 pooled, n = 900, 175 reach failures)

| stiffness tercile (kFingerMean; cuts 1.320, 1.648) | n | reach-failure rate [95 % Wilson] |
|---|---|---|
| low | 300 | 0.207 [0.165, 0.256] |
| mid | 300 | 0.177 [0.138, 0.224] |
| high | 300 | 0.200 [0.159, 0.249] |

stiffness tercile (kFingerMean; cuts 1.320, 1.648): no bin difference exceeds its interval.

| active groups | n | reach-failure rate [95 % Wilson] |
|---|---|---|
| <= 9 | 90 | 0.278 [0.196, 0.378] |
| 10-11 | 348 | 0.213 [0.173, 0.259] |
| 12-14 | 462 | 0.165 [0.133, 0.201] |

active groups: no bin difference exceeds its interval.

| mass tercile (cuts 0.374, 0.802 kg) | n | reach-failure rate [95 % Wilson] |
|---|---|---|
| low | 300 | 0.207 [0.165, 0.256] |
| mid | 300 | 0.193 [0.153, 0.242] |
| high | 300 | 0.183 [0.144, 0.231] |

mass tercile (cuts 0.374, 0.802 kg): no bin difference exceeds its interval.

| lenIndex tercile (cuts 0.927, 1.075) | n | reach-failure rate [95 % Wilson] |
|---|---|---|
| low | 300 | 0.173 [0.135, 0.220] |
| mid | 300 | 0.187 [0.147, 0.235] |
| high | 300 | 0.223 [0.180, 0.274] |

lenIndex tercile (cuts 0.927, 1.075): no bin difference exceeds its interval.

| lenMiddle tercile (cuts 0.937, 1.056) | n | reach-failure rate [95 % Wilson] |
|---|---|---|
| low | 291 | 0.206 [0.164, 0.256] |
| mid | 309 | 0.191 [0.151, 0.238] |
| high | 300 | 0.187 [0.147, 0.235] |

lenMiddle tercile (cuts 0.937, 1.056): no bin difference exceeds its interval.

| lenRing tercile (cuts 0.935, 1.069) | n | reach-failure rate [95 % Wilson] |
|---|---|---|
| low | 300 | 0.240 [0.195, 0.291] |
| mid | 300 | 0.177 [0.138, 0.224] |
| high | 300 | 0.167 [0.129, 0.213] |

lenRing tercile (cuts 0.935, 1.069): no bin difference exceeds its interval.

| lenPinky tercile (cuts 0.926, 1.069) | n | reach-failure rate [95 % Wilson] |
|---|---|---|
| low | 300 | 0.187 [0.147, 0.235] |
| mid | 300 | 0.193 [0.153, 0.242] |
| high | 300 | 0.203 [0.162, 0.252] |

lenPinky tercile (cuts 0.926, 1.069): no bin difference exceeds its interval.

| lenThumb tercile (cuts 0.943, 1.063) | n | reach-failure rate [95 % Wilson] |
|---|---|---|
| low | 297 | 0.152 [0.115, 0.197] |
| mid | 303 | 0.208 [0.166, 0.257] |
| high | 300 | 0.223 [0.180, 0.274] |

lenThumb tercile (cuts 0.943, 1.063): no bin difference exceeds its interval.

Logistic fit of reach failure on the standardized theta summary features (n = 900; intercept -1.51):

| feature | coefficient (per SD) | z |
|---|---|---|
| kFingerMean | +0.029 | +0.3 |
| zetaMean | +0.103 | +1.2 |
| inertiaScaleMean | -0.036 | -0.4 |
| activeGroups | -0.230 | -2.7 |
| mass | -0.052 | -0.6 |
| lenIndex | +12.799 | +1.0 |
| lenMiddle | -0.027 | -0.3 |
| lenRing | -0.189 | -2.2 |
| lenPinky | +0.026 | +0.3 |
| lenThumb | +0.192 | +2.2 |
| handSpanRatio | -12.684 | -1.0 |
| mu | +0.332 | +3.8 |

### 012: success by phaseAtBegin (deterministic, mu 0.6 / 1.0 / 1.5 pooled, n = 900)

| phase | n | success [95 % Wilson] |
|---|---|---|
| 0 | 37 | 1.000 [0.906, 1.000] |
| 1 | 34 | 0.971 [0.851, 0.995] |
| 2 | 66 | 1.000 [0.945, 1.000] |
| 3 | 111 | 0.991 [0.951, 0.998] |
| 4 | 194 | 1.000 [0.981, 1.000] |
| 5 | 189 | 0.979 [0.947, 0.992] |
| 6 | 139 | 1.000 [0.973, 1.000] |
| 7 | 71 | 1.000 [0.949, 1.000] |
| 8 | 40 | 0.975 [0.871, 0.996] |
| 9 | 19 | 0.947 [0.754, 0.991] |

chi-square 14.71 on 9 df (critical value at 0.05: 16.92): no detectable phase effect at this n; pooled success 0.991.

### 011b: success by phaseAtBegin (deterministic, mu 0.6 / 1.0 / 1.5 pooled, n = 900)

| phase | n | success [95 % Wilson] |
|---|---|---|
| 0 | 62 | 0.629 [0.505, 0.738] |
| 1 | 107 | 0.617 [0.522, 0.703] |
| 2 | 101 | 0.743 [0.650, 0.818] |
| 3 | 72 | 0.667 [0.552, 0.765] |
| 4 | 111 | 0.649 [0.556, 0.731] |
| 5 | 94 | 0.660 [0.559, 0.747] |
| 6 | 82 | 0.659 [0.551, 0.752] |
| 7 | 79 | 0.646 [0.536, 0.742] |
| 8 | 97 | 0.670 [0.572, 0.756] |
| 9 | 95 | 0.632 [0.531, 0.722] |

chi-square 4.73 on 9 df (critical value at 0.05: 16.92): no detectable phase effect at this n; pooled success 0.658.

### 011b: where the failures end (deterministic, mu 1.0; 89 failures of 300)

| category | failures |
|---|---|
| reach (no transition) | 57 |
| hold, before the first pulse | 19 |
| between pulses | 8 |
| during pulse 1 | 2 |
| after the last pulse | 2 |
| during pulse 2 | 1 |

median hold step at drop (failures that reached hold): 45.5 (n = 32); the three pulses start at hold steps drawn per episode and last 5 steps each.

### 012: drop-phase table skipped (0 failures, fewer than 10)



## Part 3, instrument check: the 012 deterministic mu 1.0 cell on 7001-7300 across sessions

The mu 1.0 hash is unchanged since 1716818, so this cell should reproduce `results/012/eval_n300/eval012_n300_det_mu10.csv`
(2026-09-23, same settings). Theta is identical on 300 / 300 rows, seeds 7001 and 7002 are byte-identical, and seed 7003
resolves differently (equal steps; contacts, maxPenMm, gripTorqueMean and retShaping differ) with every later row
different, as expected once one episode diverges. Two repeat passes run back to back today (`repeat_det_mu10_A/B.csv`) are
byte-identical to each other on all 49 columns, differ from the sequence pass of 16:25 at the same seed 7003, and differ
from the 2026-09-23 pass at 7003 as well. Aggregates: 0.983 (2026-09-23), 1.000 (sequence pass) and 0.993 (both repeats),
all inside each other's intervals. Reading: seed 7003 is a knife-edge contact episode that the one-worker setting does not
pin across Editor sessions or across differently ordered pass sequences, while consecutive passes in the same context
reproduce byte for byte (as the 6001-6100 block did across two days, 1 500 episodes without an event). Pass-level
reproducibility therefore means: identical within a session context; across contexts, identical up to rare knife-edge
episodes whose effect on the aggregate stays inside the Wilson interval. The sampled-head mu 1.0 pass shows the same
pattern (19 identical rows, divergence at seed 7020 in contact columns).

## Part 2 — legacyNoReseed discriminator (results/012/checks/discriminator/DISCRIMINATOR.md)

Verdict LEGACY-LOW. Sampled head, one job worker, seeds 6001-6100, mu 1.0, model 012: the pre-1716818 seeding
(`legacyNoReseed`: raw-seed `InitState`, no inference-stream reseed) gives 0.86 [0.78, 0.91] (86 / 100 under the primary
convention of this revision, the two zero-step rows at episodes 50 and 51 being reach failures; DISCRIMINATOR.md, written
under the first convention, prints 0.878 on 98), byte-identical across three relaunched Play sessions; the current seeding
gives 0.960 [0.902, 0.984], byte-identical to the post-fix reference of 2026-09-23. The two paths draw different theta and
pulse samples on every row (0 / 100 theta-identical), so the 0.10 gap (two-proportion z = 2.5) is a difference between two
samples of the theta / pulse distribution and the un-reset inference stream, not a change in the agent; no outcome column
can be paired. The session-to-session spread of the old readings (0.83 vs 0.90) is explained by multithreaded PhysX, since
the legacy path is itself reproducible at one worker. Not fixed; the reseeded path is the instrument.

## Superseded and standing numbers

Superseded by this file: every pass-vs-mu figure and every per-theta table produced before commit 1716818 (the 011b
baseline label 0.48 / 0.54 / 0.38 on 5001-5100 and its corrected variants, the run 012 n = 100 figures 0.89 / 0.83 / 0.79
and 0.90 / 0.89 / 0.66 on 6001-6100, the paired +0.41 to +0.48 on 5001-5100, the stiffness-tercile, active-group,
finger-length and mass-bin tables in `results/011b/analysis/ANALYSIS.md` and `results/012/checks/CHECKS.md`): they were
read with the sampled head, an unseeded noise stream, multithreaded physics and theta columns shifted by one episode.
Standing: the training curves and monitor rows of runs 011, 011b and 012, the lesson transitions, the dt 5 ms aggregate
transfer conclusion (0.54 -> 0.51, an aggregate read), the LR-schedule conclusion, and everything keyed on the object mass
from the agent's episode record (mass is captured at episode end and was never shifted).

## Labels

011b relabeled (README, WATCH.md, MLAGENTS_UPGRADE.md, this file): "011b (6M) baseline, deterministic head, seeds
7001-7300: 0.67 / 0.70 / 0.60 at mu 0.6 / 1.0 / 1.5 (reach failures counted)". The 012 label (run 012, deterministic head,
seeds 7001-7300, 0.97 / 0.98 / 0.99 at mu 0.6 / 1.0 / 1.5, sampled 0.92 at mu 1.0, reach failures counted) is unchanged:
the v2 cells on the same block read 0.98 / 1.00 / 0.99 and sampled 0.94 under the same counting; the only cell outside
the label's interval is deterministic mu 1.0 (1.000 [0.987, 1.000] against 0.98 [0.96, 0.99]), and its two repeat passes
read 0.993 [0.976, 0.998], inside the label's interval. The mu 0.6 and 1.5 v2 passes use different theta than the label's
passes (new hash).
