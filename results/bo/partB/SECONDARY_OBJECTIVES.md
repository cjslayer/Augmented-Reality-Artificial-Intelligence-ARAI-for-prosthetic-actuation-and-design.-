# Secondary objectives with theta-range on the Part B probe data (2026-09-27)

Question: which logged quantities vary across hands (theta) by more than episode noise on the existing probe data, and which of
those would mean something as a morphology objective. Data: the 120 probe passes of `results/bo/partB/probe_passes/` (20
training-distribution hands × 6 stressor levels × 100 episodes on seeds 4001-4100, deterministic head, one job worker, run-012
policy), read only; `PARTB.md` describes the instrument. Rows follow the `tools/eval/summarize_pass.py` convention (a first-episode
warm-up abort excluded, none occurred; other zero-step rows are reach failures and count as failures with steps to gate undefined).
Scripts: the session's analysis script (numpy only; Spearman by ranks), not committed.

## How to read the tables

- **between** = sample variance of the 20 per-hand means; **within / n** = the variance a per-hand mean would have from episode
  noise alone; **ratio** = between / (within / n). With n = 100 the denominator is small, so the brief's flag (ratio ≥ 3) is met by
  almost everything: 64 of 66 metric × level cells. The ratio ranks metrics but does not separate them.
- **ICC(1)** = share of a single episode's variance that is between hands (one-way random effects). This is the number that says
  whether an objective built on the metric would be dominated by theta or by episode noise.
- **range** = min-max of the 20 per-hand means: the effect size an optimizer would have to work with.
- One pass per (hand, level). A pass is reproducible within an Editor session; across sessions the hand-14 baseline of
  `HARD_HANDS.md` moved from 0.57 to 0.64 on identical seeds (31 of 100 episodes flipped), so per-hand means carry a pass-level
  component that within / n does not see. The check at the end of the tables bounds it: for reach-phase quantities (which the
  perturbation scale cannot change except through episode chaining) the spread across the four default-mass levels is *smaller*
  than within / n predicts for 16 hands (common random numbers: same seeds, same spawns) and 1.2-2.3 × larger for the three
  thumb-base-masked hands (5, 11, 14), whose reach failures come in pass-level clusters.

## Summary: metrics, range, meaning

| metric | ratio (min-max over levels) | ICC(1) | range of per-hand means | prosthetic meaning if optimized | gaming / confound risk | verdict |
|---|---|---|---|---|---|---|
| success | 4.1-32.4 | 0.03-0.24 | 0.85-1.00 (default mass), 0.57-1.00 (0.4-3 kg) | the task | – | the primary; range only at doubled mass and only from 3-4 hands (`HARD_HANDS.md`) |
| lifted | 3.3-18.0 | 0.02-0.15 | 0.77-1.00 | grasp acquisition | restates success (ρ = +0.85) | no |
| hold steps | 4.0-31.7 | 0.03-0.24 | 301-500 | – | identical ranking to success (ρ = +1.00) | no |
| thumb flag at end | 1.4-10.1 | 0.00-0.08 | 0.86-1.00 | – | saturated | no |
| contacts at end | 100-187 | 0.50-0.65 | 3.4-8.0 groups | enveloping grasp, load sharing | ceiling = active groups (mask); ρ = −0.51 with thumb length; counts 1 mm contacts, and the kinematic-era drop analysis found contact count did not predict survival (README, run 008 → 009: contacts fell while the pass rate rose) | a weak quality proxy; gameable through the mask and geometry |
| palm flag at end | 21.5-34.7 | 0.17-0.25 | 0.51-1.00 | power grasp; palm contact was the strongest survival predictor in the kinematic drop test (AUC 0.91 for the wedge posture) | ρ = +0.66 with grip torque, −0.48 with thumb / finger length; a policy-side quantity that a fixed policy cannot game, but BO can pick hands whose geometry lands the palm without a better grasp | plausible secondary (grasp quality) |
| steps to gate (reach time) | 2.6-22.7 | 0.02-0.18 | 74-155 steps (0.74-1.55 s) | speed of acquisition | 16 hands sit in 74-85 steps; the range is the hard hands (ρ = −0.74 with success), and their reach times are pass-chaotic (see the noise check) | meaningful but mostly a restatement of "hard" |
| steps gate → lift | 7.7-13.8 | 0.07-0.11 | 7.6-22.9 steps | how fast the grasp takes load | small absolute range (0.15 s), driven by hand 14 | weak |
| gripTorqueMean | 487-662 | 0.83-0.87 | 0.34-5.0 N m | holding torque = actuator effort in an impedance-actuated hand | it is a readout of the stiffness draw (ρ = +0.76 with thumb-base k, +0.74 with the mean k of active groups) and of the mask: the three lowest-torque hands (14: 0.36, 11: 0.36, 5: 0.53) are the three thumb-base-masked hands, so "low effort" here means "no thumb base". Free minimization drives k to the lower bound and removes the thumb | only as a constraint (min torque subject to success ≥ target), never free |
| maxPenMm | 29-69 | 0.22-0.41 | 2.1-4.9 mm | none (solver interpenetration; a contact-force proxy at best) | ρ = −0.56 with distal-joint k | diagnostic, not an objective |
| retShaping | 9.5-19.3 | 0.08-0.16 | 0.77-0.90 | none (training reward term) | ρ = +0.79 with contacts | no |

Net: on this task and policy the quantities with real theta-range are the ones that read the hand's parameters back
(grip torque, contacts, penetration) and the palm flag. Palm contact is the one secondary objective with both range (0.51-1.00)
and an established mechanical meaning; time to gate has meaning but its range is the hard-hand deficit again; grip torque is
usable only as a constraint because of the thumb-mask confound. None of them gives the success objective the range it lacks.


## Variance decomposition per level (20 theta x 100 episodes)

For each metric and level: per-theta means m_i over the 100 episodes of the pass; between = sample variance of the 20 m_i; within = mean over theta of (within-theta variance / n_i), i.e. the expected variance of a per-theta mean from episode noise alone; ratio = between / within. ICC(1) = (MSB - MSW) / (MSB + (n - 1) MSW) with MSB = n x between and MSW the pooled within-theta variance (one-way random effects; negative values clipped at 0 are shown as such). Metrics with a filter use only the rows where the quantity exists (steps to gate: rows that met the gate; gate -> lift: rows that lifted). Flag: ratio >= 3.

### perturb 1.0, mass 0.2-1.5 kg

| metric | between var of per-theta means | within var / n | ratio | ICC(1) | range of per-theta means (min-max) | worst 5 hands | best 5 hands | flag |
|---|---|---|---|---|---|---|---|---|
| success | 0.0004134 | 6.126e-05 | 6.7 | 0.054 | 0.91-1 | 14 5 11 18 0 | 19 10 1 2 3 | **yes** |
| lifted | 0.0001274 | 2.899e-05 | 4.4 | 0.033 | 0.95-1 | 14 5 0 17 16 | 19 18 1 2 3 | **yes** |
| contacts at end | 1.352 | 0.007792 | 173.5 | 0.633 | 3.63-8 | 12 11 5 7 10 | 4 0 16 1 2 | **yes** |
| palm flag at end | 0.006666 | 0.0003099 | 21.5 | 0.170 | 0.77-1 | 5 11 14 16 17 | 19 18 1 2 3 | **yes** |
| thumb flag at end | 8e-05 | 1.939e-05 | 4.1 | 0.030 | 0.96-1 | 5 0 17 16 15 | 19 18 1 2 3 | **yes** |
| hold steps | 65.99 | 11.84 | 5.6 | 0.044 | 464-500 | 14 5 11 18 0 | 19 10 1 2 3 | **yes** |
| steps to gate (reach time) | 57.07 | 2.518 | 22.7 | 0.178 | 73.6-107 | 10 11 14 9 18 | 16 6 3 0 2 | **yes** |
| steps gate -> lift | 5.226 | 0.4789 | 10.9 | 0.094 | 7.82-19.1 | 14 11 7 0 5 | 6 12 9 15 13 | **yes** |
| gripTorqueMean (effort proxy) | 2.038 | 0.003193 | 638.1 | 0.864 | 0.338-5 | 17 10 0 18 19 | 11 14 5 3 7 | **yes** |
| maxPenMm | 0.4311 | 0.006223 | 69.3 | 0.406 | 2.1-4.79 | 6 17 2 15 18 | 16 12 10 9 7 | **yes** |
| retShaping | 0.0003113 | 3.173e-05 | 9.8 | 0.081 | 0.828-0.899 | 11 5 14 12 17 | 16 15 4 3 1 | **yes** |

### perturb 1.5, mass 0.2-1.5 kg

| metric | between var of per-theta means | within var / n | ratio | ICC(1) | range of per-theta means (min-max) | worst 5 hands | best 5 hands | flag |
|---|---|---|---|---|---|---|---|---|
| success | 0.0002934 | 7.237e-05 | 4.1 | 0.030 | 0.93-1 | 11 5 14 18 1 | 19 10 2 3 4 | **yes** |
| lifted | 0.0001305 | 3.899e-05 | 3.3 | 0.023 | 0.95-1 | 11 1 5 14 0 | 19 10 2 3 4 | **yes** |
| contacts at end | 1.384 | 0.007402 | 187.0 | 0.650 | 3.46-7.87 | 11 12 5 7 10 | 4 0 16 1 2 | **yes** |
| palm flag at end | 0.00794 | 0.0003256 | 24.4 | 0.190 | 0.69-1 | 11 14 5 16 7 | 19 10 1 2 4 | **yes** |
| thumb flag at end | 2.737e-05 | 1.99e-05 | 1.4 | 0.004 | 0.98-1 | 5 11 18 0 17 | 19 10 1 2 3 |  |
| hold steps | 65.67 | 16.24 | 4.0 | 0.030 | 467-500 | 11 5 14 18 1 | 19 10 2 3 4 | **yes** |
| steps to gate (reach time) | 97.52 | 10.09 | 9.7 | 0.082 | 73.6-109 | 10 11 5 14 9 | 16 6 0 3 2 | **yes** |
| steps gate -> lift | 2.807 | 0.2039 | 13.8 | 0.114 | 7.63-15.7 | 14 0 2 5 7 | 6 9 15 13 12 | **yes** |
| gripTorqueMean (effort proxy) | 2.002 | 0.003025 | 661.9 | 0.869 | 0.332-4.99 | 17 10 0 19 18 | 11 14 5 3 7 | **yes** |
| maxPenMm | 0.4451 | 0.007065 | 63.0 | 0.383 | 2.12-4.83 | 6 17 2 15 18 | 16 12 10 9 14 | **yes** |
| retShaping | 0.0004267 | 4.479e-05 | 9.5 | 0.079 | 0.804-0.897 | 11 5 12 14 17 | 16 15 4 3 13 | **yes** |

### perturb 2.0, mass 0.2-1.5 kg

| metric | between var of per-theta means | within var / n | ratio | ICC(1) | range of per-theta means (min-max) | worst 5 hands | best 5 hands | flag |
|---|---|---|---|---|---|---|---|---|
| success | 0.001634 | 0.0001188 | 13.8 | 0.113 | 0.85-1 | 14 5 18 0 17 | 19 10 1 2 3 | **yes** |
| lifted | 0.0006063 | 7.434e-05 | 8.2 | 0.067 | 0.92-1 | 14 5 0 17 16 | 19 18 1 2 3 | **yes** |
| contacts at end | 1.322 | 0.008953 | 147.6 | 0.594 | 3.81-7.73 | 11 12 5 7 14 | 4 0 16 1 2 | **yes** |
| palm flag at end | 0.007967 | 0.0003346 | 23.8 | 0.186 | 0.72-1 | 11 14 5 16 15 | 19 10 1 2 3 | **yes** |
| thumb flag at end | 0.0002892 | 5.247e-05 | 5.5 | 0.043 | 0.94-1 | 14 5 0 17 16 | 19 18 1 2 3 | **yes** |
| hold steps | 364.5 | 27.63 | 13.2 | 0.109 | 431-500 | 14 5 18 0 17 | 19 10 1 2 3 | **yes** |
| steps to gate (reach time) | 106.6 | 18.05 | 5.9 | 0.049 | 73.4-110 | 10 14 11 5 18 | 16 0 6 3 2 | **yes** |
| steps gate -> lift | 3.573 | 0.4623 | 7.7 | 0.067 | 7.62-16.8 | 14 2 0 7 18 | 6 9 15 12 13 | **yes** |
| gripTorqueMean (effort proxy) | 1.953 | 0.00305 | 640.4 | 0.865 | 0.342-4.95 | 17 0 10 19 18 | 11 14 5 7 3 | **yes** |
| maxPenMm | 0.3887 | 0.009463 | 41.1 | 0.286 | 2.12-4.63 | 6 2 17 15 18 | 16 12 10 9 7 | **yes** |
| retShaping | 0.0004099 | 3.918e-05 | 10.5 | 0.086 | 0.827-0.896 | 5 11 14 12 17 | 16 15 3 4 1 | **yes** |

### perturb 3.0, mass 0.2-1.5 kg

| metric | between var of per-theta means | within var / n | ratio | ICC(1) | range of per-theta means (min-max) | worst 5 hands | best 5 hands | flag |
|---|---|---|---|---|---|---|---|---|
| success | 0.002562 | 0.000212 | 12.1 | 0.100 | 0.85-1 | 5 11 14 16 19 | 9 10 1 2 3 | **yes** |
| lifted | 0.0004345 | 8.096e-05 | 5.4 | 0.042 | 0.92-1 | 5 14 11 18 0 | 19 10 1 2 3 | **yes** |
| contacts at end | 1.341 | 0.01055 | 127.2 | 0.558 | 3.44-7.7 | 11 5 12 7 14 | 4 0 16 1 2 | **yes** |
| palm flag at end | 0.01467 | 0.0004643 | 31.6 | 0.234 | 0.6-1 | 11 14 5 16 7 | 19 12 1 2 4 | **yes** |
| thumb flag at end | 0.0008779 | 0.000131 | 6.7 | 0.054 | 0.89-1 | 5 11 14 16 8 | 19 10 1 2 3 | **yes** |
| hold steps | 480 | 39.76 | 12.1 | 0.100 | 434-500 | 5 11 14 8 18 | 9 10 1 2 3 | **yes** |
| steps to gate (reach time) | 184.9 | 26.25 | 7.0 | 0.059 | 73.7-127 | 11 10 5 14 12 | 0 2 16 3 6 | **yes** |
| steps gate -> lift | 4.529 | 0.4184 | 10.8 | 0.092 | 7.93-18.2 | 14 0 2 11 7 | 6 9 12 15 13 | **yes** |
| gripTorqueMean (effort proxy) | 1.994 | 0.003039 | 656.2 | 0.868 | 0.353-4.99 | 17 10 0 19 18 | 14 11 5 3 7 | **yes** |
| maxPenMm | 0.4441 | 0.006929 | 64.1 | 0.387 | 2.14-4.86 | 6 2 17 15 18 | 16 10 12 9 14 | **yes** |
| retShaping | 0.0008489 | 8.114e-05 | 10.5 | 0.086 | 0.79-0.89 | 5 11 14 12 7 | 15 4 3 16 1 | **yes** |

### perturb 1.0, mass 0.40-3.00 kg

| metric | between var of per-theta means | within var / n | ratio | ICC(1) | range of per-theta means (min-max) | worst 5 hands | best 5 hands | flag |
|---|---|---|---|---|---|---|---|---|
| success | 0.008775 | 0.0003774 | 23.3 | 0.182 | 0.66-1 | 14 5 10 11 0 | 19 7 1 2 3 | **yes** |
| lifted | 0.002887 | 0.0002041 | 14.1 | 0.116 | 0.79-1 | 14 5 10 11 17 | 19 18 1 2 3 | **yes** |
| contacts at end | 1.458 | 0.01184 | 123.1 | 0.550 | 3.5-8 | 5 11 12 10 14 | 4 0 1 13 19 | **yes** |
| palm flag at end | 0.0131 | 0.0005014 | 26.1 | 0.201 | 0.63-1 | 14 5 11 16 7 | 19 18 1 3 4 | **yes** |
| thumb flag at end | 0.0009379 | 0.0001598 | 5.9 | 0.046 | 0.9-1 | 5 10 14 11 0 | 19 18 1 2 3 | **yes** |
| hold steps | 1842 | 83.34 | 22.1 | 0.174 | 343-500 | 14 5 10 11 0 | 19 7 1 2 3 | **yes** |
| steps to gate (reach time) | 334.9 | 108 | 3.1 | 0.022 | 74-138 | 5 10 11 14 0 | 2 3 6 7 4 | **yes** |
| steps gate -> lift | 9.066 | 0.7928 | 11.4 | 0.110 | 8.6-22.9 | 14 11 7 2 17 | 6 9 13 15 5 | **yes** |
| gripTorqueMean (effort proxy) | 1.922 | 0.003943 | 487.4 | 0.829 | 0.365-4.99 | 17 0 19 18 10 | 14 11 5 7 16 | **yes** |
| maxPenMm | 0.3544 | 0.01065 | 33.3 | 0.244 | 2.37-4.77 | 6 15 17 2 18 | 12 9 16 7 11 | **yes** |
| retShaping | 0.00123 | 9.7e-05 | 12.7 | 0.105 | 0.767-0.894 | 14 11 5 10 17 | 15 4 3 6 13 | **yes** |

### perturb 2.0, mass 0.40-3.00 kg

| metric | between var of per-theta means | within var / n | ratio | ICC(1) | range of per-theta means (min-max) | worst 5 hands | best 5 hands | flag |
|---|---|---|---|---|---|---|---|---|
| success | 0.01175 | 0.0003625 | 32.4 | 0.239 | 0.57-1 | 14 5 18 16 11 | 19 10 2 3 4 | **yes** |
| lifted | 0.003042 | 0.0001688 | 18.0 | 0.145 | 0.77-1 | 14 5 18 11 0 | 19 10 1 2 3 | **yes** |
| contacts at end | 1.354 | 0.01354 | 100.0 | 0.498 | 3.41-7.58 | 5 11 12 14 7 | 4 0 1 19 6 | **yes** |
| palm flag at end | 0.02228 | 0.0006424 | 34.7 | 0.252 | 0.51-1 | 14 5 11 16 12 | 0 10 17 1 13 | **yes** |
| thumb flag at end | 0.001638 | 0.0001628 | 10.1 | 0.083 | 0.86-1 | 5 14 16 11 12 | 19 10 1 2 3 | **yes** |
| hold steps | 2468 | 77.81 | 31.7 | 0.235 | 301-500 | 14 5 18 11 16 | 19 10 2 3 4 | **yes** |
| steps to gate (reach time) | 478.8 | 186.8 | 2.6 | 0.017 | 73.3-155 | 14 18 10 5 16 | 6 3 2 0 4 |  |
| steps gate -> lift | 8.436 | 0.7814 | 10.8 | 0.107 | 8.53-22.5 | 14 11 2 0 7 | 6 9 4 18 15 | **yes** |
| gripTorqueMean (effort proxy) | 1.972 | 0.003112 | 633.8 | 0.864 | 0.375-5.03 | 17 10 0 19 18 | 14 11 5 16 12 | **yes** |
| maxPenMm | 0.3632 | 0.01243 | 29.2 | 0.220 | 2.59-4.81 | 6 15 17 18 2 | 12 9 16 10 7 | **yes** |
| retShaping | 0.001341 | 6.932e-05 | 19.3 | 0.155 | 0.771-0.891 | 14 5 11 12 18 | 3 15 4 2 1 | **yes** |

### Flag summary (levels at which ratio >= 3)

- success: perturb 1.0, mass 0.2-1.5 kg, perturb 1.5, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg, perturb 2.0, mass 0.40-3.00 kg (6/6 levels)
- lifted: perturb 1.0, mass 0.2-1.5 kg, perturb 1.5, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg, perturb 2.0, mass 0.40-3.00 kg (6/6 levels)
- contacts at end: perturb 1.0, mass 0.2-1.5 kg, perturb 1.5, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg, perturb 2.0, mass 0.40-3.00 kg (6/6 levels)
- palm flag at end: perturb 1.0, mass 0.2-1.5 kg, perturb 1.5, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg, perturb 2.0, mass 0.40-3.00 kg (6/6 levels)
- thumb flag at end: perturb 1.0, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg, perturb 2.0, mass 0.40-3.00 kg (5/6 levels)
- hold steps: perturb 1.0, mass 0.2-1.5 kg, perturb 1.5, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg, perturb 2.0, mass 0.40-3.00 kg (6/6 levels)
- steps to gate (reach time): perturb 1.0, mass 0.2-1.5 kg, perturb 1.5, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg (5/6 levels)
- steps gate -> lift: perturb 1.0, mass 0.2-1.5 kg, perturb 1.5, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg, perturb 2.0, mass 0.40-3.00 kg (6/6 levels)
- gripTorqueMean (effort proxy): perturb 1.0, mass 0.2-1.5 kg, perturb 1.5, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg, perturb 2.0, mass 0.40-3.00 kg (6/6 levels)
- maxPenMm: perturb 1.0, mass 0.2-1.5 kg, perturb 1.5, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg, perturb 2.0, mass 0.40-3.00 kg (6/6 levels)
- retShaping: perturb 1.0, mass 0.2-1.5 kg, perturb 1.5, mass 0.2-1.5 kg, perturb 2.0, mass 0.2-1.5 kg, perturb 3.0, mass 0.2-1.5 kg, perturb 1.0, mass 0.40-3.00 kg, perturb 2.0, mass 0.40-3.00 kg (6/6 levels)

### Per-theta means averaged over the six levels, hands ranked (flagged metrics)

| hand | success | lifted | contacts at end | palm flag at end | thumb flag at end | hold steps | steps to gate (reach time) | steps gate -> lift | gripTorqueMean (effort proxy) | maxPenMm | retShaping |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 9 | 1 | 1 | 5.75 | 0.995 | 1 | 500 | 82.5 | 8.65 | 3.19 | 2.69 | 0.873 |
| 15 | 1 | 1 | 5.99 | 0.983 | 1 | 500 | 77.4 | 9.12 | 2.59 | 4.14 | 0.891 |
| 2 | 1 | 1 | 6.31 | 0.985 | 1 | 500 | 74.3 | 11.8 | 2.82 | 4.11 | 0.882 |
| 3 | 1 | 1 | 5.97 | 0.983 | 1 | 500 | 74.1 | 10.5 | 1.35 | 3.52 | 0.889 |
| 4 | 1 | 1 | 7.81 | 0.998 | 1 | 500 | 76.2 | 9.65 | 3.47 | 3.11 | 0.889 |
| 13 | 1 | 1 | 6.16 | 1 | 1 | 500 | 80.2 | 9.16 | 2.54 | 3.59 | 0.882 |
| 6 | 1 | 1 | 5.99 | 0.997 | 1 | 500 | 73.8 | 8.02 | 2.63 | 4.78 | 0.881 |
| 12 | 0.998 | 1 | 3.77 | 0.965 | 0.997 | 499 | 83.8 | 9.24 | 1.37 | 2.46 | 0.855 |
| 19 | 0.998 | 1 | 6.14 | 0.998 | 1 | 500 | 80.8 | 10.4 | 4.03 | 3.28 | 0.875 |
| 7 | 0.998 | 1 | 4.5 | 0.965 | 1 | 499 | 77.1 | 11.7 | 1.34 | 2.85 | 0.865 |
| 1 | 0.997 | 0.998 | 6.64 | 1 | 1 | 498 | 85.3 | 10.6 | 3.1 | 3.04 | 0.884 |
| 17 | 0.997 | 0.998 | 5.51 | 0.985 | 0.998 | 498 | 80.1 | 11 | 4.99 | 4.14 | 0.859 |
| 8 | 0.995 | 1 | 4.79 | 0.997 | 0.998 | 498 | 77.4 | 10.5 | 1.48 | 3.39 | 0.861 |
| 0 | 0.99 | 1 | 7.01 | 0.997 | 0.997 | 496 | 76.8 | 11.9 | 4.19 | 3.41 | 0.869 |
| 16 | 0.977 | 0.998 | 6.64 | 0.898 | 0.985 | 492 | 81.3 | 10.9 | 1.38 | 2.32 | 0.888 |
| 10 (hard) | 0.972 | 0.983 | 4.56 | 0.99 | 0.987 | 487 | 110 | 10.3 | 4.2 | 2.77 | 0.857 |
| 18 (hard) | 0.967 | 0.99 | 5.73 | 0.993 | 0.995 | 487 | 89.9 | 10.8 | 3.92 | 3.81 | 0.872 |
| 11 | 0.94 | 0.975 | 3.61 | 0.697 | 0.975 | 472 | 104 | 12.2 | 0.36 | 3.11 | 0.805 |
| 5 (hard) | 0.868 | 0.937 | 3.77 | 0.738 | 0.923 | 440 | 102 | 10.6 | 0.527 | 3.38 | 0.815 |
| 14 (hard) | 0.807 | 0.897 | 4.52 | 0.68 | 0.95 | 413 | 105 | 19.2 | 0.363 | 3.08 | 0.82 |

### Spearman correlations across the 20 hands (level-averaged per-theta means)

| | success | lifted | contacts | palm | thumb | holdSteps | transitionStep | liftLatency | gripTorqueMean | maxPenMm | retShaping |
|---|---|---|---|---|---|---|---|---|---|---|---|
| success | 1 | +0.85 | +0.46 | +0.46 | +0.91 | +1.00 | -0.74 | -0.62 | +0.19 | +0.28 | +0.71 |
| lifted | +0.85 | 1 | +0.43 | +0.49 | +0.80 | +0.85 | -0.80 | -0.49 | +0.19 | +0.20 | +0.55 |
| contacts | +0.46 | +0.43 | 1 | +0.66 | +0.52 | +0.48 | -0.52 | -0.15 | +0.50 | +0.19 | +0.79 |
| palm | +0.46 | +0.49 | +0.66 | 1 | +0.61 | +0.47 | -0.32 | -0.47 | +0.66 | +0.23 | +0.46 |
| thumb | +0.91 | +0.80 | +0.52 | +0.61 | 1 | +0.91 | -0.69 | -0.49 | +0.28 | +0.28 | +0.71 |
| holdSteps | +1.00 | +0.85 | +0.48 | +0.47 | +0.91 | 1 | -0.75 | -0.62 | +0.22 | +0.32 | +0.71 |
| transitionStep | -0.74 | -0.80 | -0.52 | -0.32 | -0.69 | -0.75 | 1 | +0.20 | -0.14 | -0.51 | -0.61 |
| liftLatency | -0.62 | -0.49 | -0.15 | -0.47 | -0.49 | -0.62 | +0.20 | 1 | -0.22 | -0.06 | -0.39 |
| gripTorqueMean | +0.19 | +0.19 | +0.50 | +0.66 | +0.28 | +0.22 | -0.14 | -0.22 | 1 | +0.23 | +0.23 |
| maxPenMm | +0.28 | +0.20 | +0.19 | +0.23 | +0.28 | +0.32 | -0.51 | -0.06 | +0.23 | 1 | +0.22 |
| retShaping | +0.71 | +0.55 | +0.79 | +0.46 | +0.71 | +0.71 | -0.61 | -0.39 | +0.23 | +0.22 | 1 |

Against the derived theta summaries of 1a (Spearman across the 20 hands; |rho| >= 0.45 in bold, which is roughly the 5 % two-sided threshold at n = 20):

| theta summary | success | lifted | contacts | palm | thumb | holdSteps | transitionStep | liftLatency | gripTorqueMean | maxPenMm | retShaping |
|---|---|---|---|---|---|---|---|---|---|---|---|
| active groups | +0.01 | -0.01 | +0.17 | -0.16 | -0.12 | +0.01 | -0.33 | +0.15 | -0.39 | +0.39 | +0.14 |
| active finger groups (non-thumb) | -0.16 | -0.11 | -0.02 | -0.22 | -0.24 | -0.15 | -0.18 | +0.22 | **-0.50** | +0.35 | -0.07 |
| total finger length (4 fingers, mm) | +0.05 | -0.11 | -0.06 | +0.26 | +0.17 | +0.06 | +0.17 | +0.10 | +0.21 | +0.15 | -0.04 |
| thumb chain length (mm) | -0.11 | -0.02 | **-0.51** | -0.40 | -0.22 | -0.10 | +0.03 | +0.07 | -0.06 | +0.09 | **-0.49** |
| thumb / mean finger length | -0.17 | -0.04 | -0.39 | **-0.48** | -0.32 | -0.17 | -0.01 | +0.10 | -0.23 | +0.01 | -0.41 |
| hand span (palm + index + tip, mm) | +0.00 | +0.03 | -0.18 | +0.11 | +0.15 | -0.02 | -0.22 | +0.14 | -0.06 | +0.37 | -0.08 |
| mean finger length scale | +0.00 | -0.15 | -0.09 | +0.23 | +0.13 | +0.01 | +0.21 | +0.12 | +0.15 | +0.12 | -0.05 |
| mean k MCP (base) (N m/rad) | +0.03 | +0.03 | -0.30 | -0.29 | -0.08 | +0.01 | +0.07 | -0.18 | -0.28 | -0.45 | -0.26 |
| geo-mean k MCP (base) (N m/rad) | +0.00 | +0.10 | -0.32 | -0.31 | -0.08 | -0.01 | -0.00 | -0.04 | -0.33 | -0.33 | -0.25 |
| mean k PIP (middle) (N m/rad) | +0.22 | +0.26 | -0.22 | +0.08 | +0.23 | +0.21 | -0.05 | -0.27 | -0.08 | -0.13 | +0.06 |
| geo-mean k PIP (middle) (N m/rad) | +0.16 | +0.20 | -0.33 | -0.07 | +0.16 | +0.13 | +0.07 | -0.29 | -0.22 | -0.24 | +0.03 |
| mean k DIP (end) (N m/rad) | -0.22 | -0.07 | -0.18 | -0.22 | -0.37 | -0.25 | +0.01 | -0.04 | -0.19 | **-0.45** | -0.19 |
| geo-mean k DIP (end) (N m/rad) | -0.17 | -0.12 | -0.26 | -0.20 | -0.34 | -0.20 | +0.16 | -0.15 | -0.17 | **-0.56** | -0.22 |
| mean k thumb base (N m/rad) | -0.17 | -0.27 | +0.37 | +0.40 | -0.03 | -0.15 | +0.13 | +0.15 | **+0.76** | +0.34 | +0.03 |
| geo-mean k thumb base (N m/rad) | -0.17 | -0.27 | +0.37 | +0.40 | -0.03 | -0.15 | +0.13 | +0.15 | **+0.76** | +0.34 | +0.03 |
| mean k thumb end (N m/rad) | +0.01 | -0.02 | +0.00 | -0.33 | -0.07 | +0.01 | -0.09 | +0.16 | -0.16 | +0.13 | +0.23 |
| geo-mean k thumb end (N m/rad) | +0.01 | -0.02 | +0.00 | -0.33 | -0.07 | +0.01 | -0.09 | +0.16 | -0.16 | +0.13 | +0.23 |
| mean k wrist (N m/rad) | +0.33 | +0.28 | +0.04 | +0.08 | +0.38 | +0.33 | -0.30 | -0.04 | +0.09 | +0.10 | +0.13 |
| geo-mean k wrist (N m/rad) | +0.36 | +0.25 | +0.11 | +0.14 | +0.37 | +0.35 | -0.38 | -0.04 | +0.17 | +0.19 | +0.14 |
| mean k active finger groups (N m/rad) | +0.02 | -0.02 | +0.18 | +0.39 | +0.06 | +0.04 | +0.23 | -0.25 | **+0.74** | -0.14 | +0.04 |
| mean zeta fingers | +0.20 | +0.33 | +0.06 | -0.02 | +0.12 | +0.22 | -0.20 | -0.11 | -0.05 | +0.04 | +0.13 |
| mean zeta wrist | -0.03 | +0.08 | +0.42 | +0.42 | +0.08 | -0.02 | +0.17 | -0.30 | **+0.53** | -0.35 | +0.31 |
| mean inertia scale fingers | -0.23 | -0.17 | **-0.47** | -0.40 | -0.23 | -0.23 | +0.15 | +0.04 | **-0.48** | -0.02 | -0.14 |
| mean inertia scale wrist | +0.13 | +0.08 | -0.45 | -0.26 | +0.17 | +0.13 | +0.09 | -0.02 | -0.17 | -0.09 | -0.28 |

Individual theta dimensions most correlated with level-averaged success (|rho| top 10):

k_wristPron +0.69, mask_thumbBase +0.63, k_middleEnd -0.57, inertia_indexBase -0.46, mask_ringBase +0.41, mask_indexMiddle -0.38, mask_indexEnd -0.38, mask_middleMiddle -0.36, inertia_pinkyMiddle -0.33, k_ringEnd +0.32

### Pass-level noise check: reach-phase metrics across the four default-mass levels (same theta, same seeds; only the hold-phase pulses differ)

| theta | reach failures at perturb 1.0 / 1.5 / 2.0 / 3.0 | steps to gate, per-pass mean at 1.0 / 1.5 / 2.0 / 3.0 | SD of the four means | expected SD from within-pass noise (sqrt(within/n)) |
|---|---|---|---|---|
| 0 | 0 / 0 / 0 / 0 | 74.2 / 74.4 / 73.6 / 73.7 | 0.38 | 0.89 |
| 1 | 0 / 1 / 0 / 0 | 82.1 / 81.4 / 80.6 / 80.5 | 0.74 | 1.10 |
| 2 | 0 / 0 / 0 / 0 | 74.5 / 74.5 / 74.6 / 73.9 | 0.33 | 0.86 |
| 3 | 0 / 0 / 0 / 0 | 74.0 / 74.4 / 74.3 / 74.2 | 0.17 | 0.77 |
| 4 | 0 / 0 / 0 / 0 | 76.1 / 76.4 / 76.3 / 75.9 | 0.24 | 0.90 |
| 5 (hard) | 0 / 0 / 5 / 2 | 82.6 / 88.7 / 89.7 / 103.2 | 8.69 | 7.14 |
| 6 | 0 / 0 / 0 / 0 | 73.7 / 73.7 / 73.7 / 74.2 | 0.25 | 0.88 |
| 7 | 0 / 0 / 0 / 0 | 80.4 / 75.4 / 75.7 / 78.4 | 2.38 | 2.12 |
| 8 | 0 / 0 / 0 / 0 | 76.2 / 75.1 / 75.9 / 75.8 | 0.45 | 0.89 |
| 9 | 0 / 0 / 0 / 0 | 83.4 / 83.0 / 82.2 / 81.8 | 0.73 | 0.92 |
| 10 (hard) | 0 / 0 / 0 / 0 | 106.6 / 108.5 / 110.0 / 105.5 | 1.99 | 3.46 |
| 11 | 0 / 5 / 0 / 4 | 89.9 / 106.9 / 90.2 / 127.0 | 17.56 | 7.51 |
| 12 | 0 / 0 / 0 / 0 | 82.6 / 82.8 / 82.7 / 82.4 | 0.15 | 0.95 |
| 13 | 0 / 0 / 0 / 0 | 80.2 / 79.4 / 80.1 / 79.9 | 0.34 | 1.04 |
| 14 (hard) | 1 / 0 / 5 / 2 | 86.2 / 85.3 / 107.4 / 89.9 | 10.33 | 5.76 |
| 15 | 0 / 0 / 0 / 0 | 77.7 / 77.1 / 77.2 / 77.5 | 0.24 | 0.98 |
| 16 | 0 / 0 / 0 / 0 | 73.6 / 73.6 / 73.4 / 74.0 | 0.24 | 0.73 |
| 17 | 0 / 0 / 0 / 0 | 80.6 / 80.0 / 80.5 / 79.2 | 0.66 | 0.93 |
| 18 (hard) | 0 / 0 / 0 / 1 | 83.1 / 80.1 / 83.0 / 78.6 | 2.22 | 1.87 |
| 19 | 0 / 0 / 0 / 0 | 81.0 / 80.8 / 80.6 / 80.6 | 0.21 | 0.84 |

Median over theta of (SD across levels) / (within-pass SD of the mean): 0.47; a value near 1 means the pass-to-pass spread of a reach-phase mean is what episode noise alone predicts, larger values mean the pass (episode chaining) adds variance that the within/n denominator does not see.

Reach failures per theta at every level (perturb 1.0 / 1.5 / 2.0 / 3.0 at 0.2-1.5 kg; 1.0 / 2.0 at 0.4-3.0 kg): theta 1: 0 1 0 0 0 0; theta 5: 0 0 5 2 7 4; theta 10: 0 0 0 0 10 0; theta 11: 0 5 0 4 1 1; theta 14: 1 0 5 2 9 10; theta 18: 0 0 0 1 0 5
