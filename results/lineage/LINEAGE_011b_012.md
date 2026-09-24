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
- Rows flagged `maxStepTimeout` (ML-Agents MaxStep interruptions, record steps 0) stay in the CSV and are excluded from
  every aggregate; the count is reported as `excluded_zero_step`.
- Sampled head at mu 1.0 on the same block gives the training-time-head figure; it is reseeded per episode, so it is a
  pass-level reproducible number too.
- Task: K = 50 hold decisions, perturbation scale 1.0, friction override applied to the object's physics material.

## Part 3 — n = 300 per cell on seeds 7001-7300 (new hash, one job worker, throwaway pass first)

| model | head | mu | n | excluded_zero_step | success [95 % Wilson] | lifted | palm at end | contacts | mean hold steps | ends |
|---|---|---|---|---|---|---|---|---|---|---|
| 011b | deterministic | 0.60 | 260 | 40 | 0.777 [0.722, 0.823] | 1.000 | 0.919 | 5.62 | 411 | {'drop': 58, 'success': 202} |
| 011b | deterministic | 1.00 | 242 | 58 | 0.872 [0.824, 0.908] | 1.000 | 0.913 | 6.01 | 448 | {'drop': 31, 'success': 211} |
| 011b | deterministic | 1.50 | 223 | 77 | 0.803 [0.746, 0.850] | 0.987 | 0.830 | 5.83 | 410 | {'drop': 44, 'success': 179} |
| 011b | sampled | 1.00 | 285 | 15 | 0.582 [0.524, 0.638] | 0.989 | 0.688 | 3.98 | 337 | {'drop': 119, 'success': 166} |
| 012 | deterministic | 0.60 | 300 | 0 | 0.980 [0.957, 0.991] | 0.990 | 0.980 | 5.67 | 490 | {'drop': 6, 'success': 294} |
| 012 | deterministic | 1.00 | 300 | 0 | 1.000 [0.987, 1.000] | 1.000 | 0.967 | 5.57 | 500 | {'success': 300} |
| 012 | deterministic | 1.50 | 299 | 1 | 0.997 [0.981, 0.999] | 1.000 | 0.953 | 5.34 | 499 | {'drop': 1, 'success': 298} |
| 012 | sampled | 1.00 | 298 | 2 | 0.946 [0.915, 0.967] | 0.993 | 0.859 | 4.35 | 478 | {'drop': 16, 'success': 282} |

### Paired differences (same seeds = same theta, mass, spawn and pulse draws; zero-step rows dropped pairwise; normal 95 % CI on the mean paired difference)

| comparison | mean difference | 95 % CI | n pairs | wins / losses | theta-paired rows |
|---|---|---|---|---|---|
| 012 - 011b, deterministic, mu 0.6 | +0.200 | [+0.149, +0.251] | 260 | 54 / 2 | 300/300 |
| 012 - 011b, deterministic, mu 1.0 | +0.128 | [+0.086, +0.170] | 242 | 31 / 0 | 300/300 |
| 012 - 011b, deterministic, mu 1.5 | +0.193 | [+0.139, +0.246] | 223 | 44 / 1 | 300/300 |
| 012: deterministic - sampled, mu 1.0 | +0.054 | [+0.028, +0.079] | 298 | 16 / 0 | 300/300 |
| 011b: deterministic - sampled, mu 1.0 | +0.277 | [+0.203, +0.351] | 231 | 79 / 15 | 300/300 |
| 012 deterministic: mu 0.6 - mu 1.5 | -0.017 | [-0.034, +0.001] | 299 | 1 / 6 | 300/300 |
| 011b deterministic: mu 0.6 - mu 1.5 | -0.005 | [-0.077, +0.067] | 195 | 25 / 26 | 300/300 |

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


### 011b (deterministic, mu 1.0, n = 242)

| stiffness tercile (kFingerMean; cuts 1.321, 1.616) | n | success [95 % Wilson] |
|---|---|---|
| low | 79 | 0.873 [0.782, 0.930] |
| mid | 82 | 0.878 [0.790, 0.932] |
| high | 81 | 0.864 [0.773, 0.922] |

stiffness tercile (kFingerMean; cuts 1.321, 1.616): no bin difference exceeds its interval.

| active groups | n | success [95 % Wilson] |
|---|---|---|
| <= 9 | 20 | 0.850 [0.640, 0.948] |
| 10-11 | 90 | 0.833 [0.743, 0.896] |
| 12-14 | 132 | 0.902 [0.839, 0.942] |

active groups: no bin difference exceeds its interval.

| mass tercile (cuts 0.380, 0.805 kg) | n | success [95 % Wilson] |
|---|---|---|
| low | 79 | 0.861 [0.768, 0.920] |
| mid | 82 | 0.878 [0.790, 0.932] |
| high | 81 | 0.877 [0.787, 0.932] |

mass tercile (cuts 0.380, 0.805 kg): no bin difference exceeds its interval.

| lenIndex tercile (cuts 0.926, 1.069) | n | success [95 % Wilson] |
|---|---|---|
| low | 80 | 0.850 [0.756, 0.912] |
| mid | 80 | 0.875 [0.785, 0.931] |
| high | 82 | 0.890 [0.804, 0.941] |

lenIndex tercile (cuts 0.926, 1.069): no bin difference exceeds its interval.

| lenMiddle tercile (cuts 0.945, 1.060) | n | success [95 % Wilson] |
|---|---|---|
| low | 79 | 0.899 [0.813, 0.948] |
| mid | 82 | 0.854 [0.761, 0.914] |
| high | 81 | 0.864 [0.773, 0.922] |

lenMiddle tercile (cuts 0.945, 1.060): no bin difference exceeds its interval.

| lenRing tercile (cuts 0.945, 1.074) | n | success [95 % Wilson] |
|---|---|---|
| low | 80 | 0.875 [0.785, 0.931] |
| mid | 80 | 0.875 [0.785, 0.931] |
| high | 82 | 0.866 [0.776, 0.923] |

lenRing tercile (cuts 0.945, 1.074): no bin difference exceeds its interval.

| lenPinky tercile (cuts 0.923, 1.070) | n | success [95 % Wilson] |
|---|---|---|
| low | 80 | 0.863 [0.770, 0.921] |
| mid | 81 | 0.877 [0.787, 0.932] |
| high | 81 | 0.877 [0.787, 0.932] |

lenPinky tercile (cuts 0.923, 1.070): no bin difference exceeds its interval.

| lenThumb tercile (cuts 0.936, 1.051) | n | success [95 % Wilson] |
|---|---|---|
| low | 80 | 0.850 [0.756, 0.912] |
| mid | 81 | 0.827 [0.731, 0.894] |
| high | 81 | 0.938 [0.864, 0.973] |

lenThumb tercile (cuts 0.936, 1.051): no bin difference exceeds its interval.


### 012: success by phaseAtBegin (deterministic, mu 0.6 / 1.0 / 1.5 pooled, n = 899)

| phase | n | success [95 % Wilson] |
|---|---|---|
| 0 | 37 | 1.000 [0.906, 1.000] |
| 1 | 33 | 1.000 [0.896, 1.000] |
| 2 | 66 | 1.000 [0.945, 1.000] |
| 3 | 111 | 0.991 [0.951, 0.998] |
| 4 | 194 | 1.000 [0.981, 1.000] |
| 5 | 189 | 0.979 [0.947, 0.992] |
| 6 | 139 | 1.000 [0.973, 1.000] |
| 7 | 71 | 1.000 [0.949, 1.000] |
| 8 | 40 | 0.975 [0.871, 0.996] |
| 9 | 19 | 0.947 [0.754, 0.991] |

chi-square 15.12 on 9 df (critical value at 0.05: 16.92): no detectable phase effect at this n; pooled success 0.992.

### 011b: success by phaseAtBegin (deterministic, mu 0.6 / 1.0 / 1.5 pooled, n = 725)

| phase | n | success [95 % Wilson] |
|---|---|---|
| 0 | 51 | 0.765 [0.632, 0.860] |
| 1 | 86 | 0.767 [0.668, 0.844] |
| 2 | 88 | 0.852 [0.763, 0.912] |
| 3 | 60 | 0.800 [0.682, 0.882] |
| 4 | 84 | 0.857 [0.767, 0.916] |
| 5 | 68 | 0.912 [0.821, 0.959] |
| 6 | 68 | 0.794 [0.684, 0.873] |
| 7 | 65 | 0.785 [0.670, 0.867] |
| 8 | 77 | 0.844 [0.747, 0.909] |
| 9 | 78 | 0.769 [0.664, 0.849] |

chi-square 10.43 on 9 df (critical value at 0.05: 16.92): no detectable phase effect at this n; pooled success 0.817.

### 011b: where the failures end (deterministic, mu 1.0; 31 failures of 242)

| category | failures |
|---|---|
| hold, before the first pulse | 19 |
| between pulses | 7 |
| during pulse 1 | 2 |
| after the last pulse | 2 |
| during pulse 2 | 1 |

median hold step at drop (failures that reached hold): 41 (n = 31); the three pulses start at hold steps drawn per episode and last 5 steps each.

### 012: drop-phase table skipped (0 failures, fewer than 10)


## Part 3, companion: the same cells with the zero-step rows counted as failures

The brief's aggregate rule excludes zero-step rows. For 012 they are rare (0-2 per pass). For 011b they are 40-77 per
deterministic pass and 57 of the 58 at mu 1.0 never transitioned to a grasp in 5000 steps: reach failures of the policy,
not harness artefacts. Both conventions are therefore shown; the 011b baseline label (0.48 / 0.54 / 0.38, seeds 5001-5100,
pre-1716818 harness) counted them as failures.

| model | head | mu | n_total | zero-step rows | success, zero-step excluded [Wilson] | success, all rows [Wilson] |
|---|---|---|---|---|---|---|
| 011b | deterministic | 0.60 | 300 | 40 | 0.777 [0.722, 0.823] | 0.673 [0.618, 0.724] |
| 011b | deterministic | 1.00 | 300 | 58 | 0.872 [0.824, 0.908] | 0.703 [0.649, 0.752] |
| 011b | deterministic | 1.50 | 300 | 77 | 0.803 [0.746, 0.850] | 0.597 [0.540, 0.651] |
| 011b | sampled | 1.00 | 300 | 15 | 0.582 [0.524, 0.638] | 0.553 [0.497, 0.609] |
| 012 | deterministic | 0.60 | 300 | 0 | 0.980 [0.957, 0.991] | 0.980 [0.957, 0.991] |
| 012 | deterministic | 1.00 | 300 | 0 | 1.000 [0.987, 1.000] | 1.000 [0.987, 1.000] |
| 012 | deterministic | 1.50 | 300 | 1 | 0.997 [0.981, 0.999] | 0.993 [0.976, 0.998] |
| 012 | sampled | 1.00 | 300 | 2 | 0.946 [0.915, 0.967] | 0.940 [0.907, 0.962] |

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
(`legacyNoReseed`: raw-seed `InitState`, no inference-stream reseed) gives 0.878 [0.798, 0.929] (86 / 98, two zero-step
rows), byte-identical across three relaunched Play sessions; the current seeding gives 0.960 [0.902, 0.984], byte-identical
to the post-fix reference of 2026-09-23. The two paths draw different theta and pulse samples on every row (0 / 100
theta-identical), so the 0.08 gap (two-proportion z = 2.1) is a difference between two samples of the theta / pulse
distribution and the un-reset inference stream, not a change in the agent; no outcome column can be paired. The
session-to-session spread of the old readings (0.83 vs 0.90) is explained by multithreaded PhysX, since the legacy path is
itself reproducible at one worker. Not fixed; the reseeded path is the instrument.

## Superseded and standing numbers

Superseded by this file: every pass-vs-mu figure and every per-theta table produced before commit 1716818 (the 011b
baseline label 0.48 / 0.54 / 0.38 on 5001-5100 and its corrected variants, the run 012 n = 100 figures 0.89 / 0.83 / 0.79
and 0.90 / 0.89 / 0.66 on 6001-6100, the paired +0.41 to +0.48 on 5001-5100, the stiffness-tercile, active-group,
finger-length and mass-bin tables in `results/011b/analysis/ANALYSIS.md` and `results/012/checks/CHECKS.md`): they were
read with the sampled head, an unseeded noise stream, multithreaded physics and theta columns shifted by one episode.
Standing: the training curves and monitor rows of runs 011, 011b and 012, the lesson transitions, the dt 5 ms aggregate
transfer conclusion (0.54 -> 0.51, an aggregate read), the LR-schedule conclusion, and everything keyed on the object mass
from the agent's episode record (mass is captured at episode end and was never shifted).

## Scene-model label decision

The README / WATCH.md label (run 012, deterministic head, seeds 7001-7300, 0.97 / 0.98 / 0.99 at mu 0.6 / 1.0 / 1.5,
sampled 0.92 at mu 1.0; zero-step rows counted as failures) is kept. The v2 cells on the same block read 0.98 / 1.00 /
0.99 and sampled 0.94 with all rows counted (0.98 / 1.00 / 1.00 and 0.95 with zero-step rows excluded); the only cell
outside the label's interval is deterministic mu 1.0 (1.000 [0.987, 1.000] against 0.98 [0.96, 0.99]), and its two repeat
passes read 0.993 [0.976, 0.998], inside the label's interval. Note that the mu 0.6 and 1.5 v2 passes use different
theta than the label's passes (new hash) and the label's counting convention differs from the exclusion rule adopted
here; changing the label is a decision to take together with that convention.
