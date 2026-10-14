# Discrete outer loop over hand morphology on 014CD (2026-10-13/14)

## Scope

**What was searched.** A candidate is a composition `c` (3, 4, 5, 6a, 6b or 7) plus an actuation mask `m` over the joint groups present in `c`. The policy is fixed: `results/014/CD/Prosthetic_CD.onnx` (sha256 68d86b81…a8a9cf). Nothing was trained.

**Search block.**
- J is the success rate over 16 held-out shapes × 4 seeds per pose:
  - upright (canonical): seeds 13001–13064;
  - lying (randomStable): seeds 13065–13128.
- That is 128 episodes per candidate. The brief's "32 shapes" was wrong.
- Every candidate reports upright, lying and pooled J. Elimination ranks on pooled J.

**Confirmation.** Seeds 13201–13500 per pose (n = 300 each, 600 pooled), with the deterministic and sampled heads. Each point is paired with the composition's all-on hand on identical rows.

**Results.**
- **5-digit hand.** With all groups on it has the highest confirmed J (0.837 deterministic). It also tolerates the most masking: two groups off cost nothing (+0.013, ns).
- **Thumb base.** It is the one group the policy cannot lose. In every composition with 4 or more digits, its single ablation costs 0.36–0.56 J.
- **Second thumb.** It adds nothing; see [the second-thumb question](#the-second-thumb-question).

## Method

### Instrument

- Editor harness (`tools/bo/gates_runner.run_pass` → ArticulatedGates mode `eval`), configured as in `tools/eval/eval_014.py`:
  - perturbation 1.0, K 50, μ 1.0;
  - lift budget 300;
  - held-out shapes 65–80, stratified (`objectStratify heldout`: shape = 65 + ordinal % 16);
  - one job worker, per-episode reseed;
  - `spawnStream` placements;
  - slot topology.
- Driver: `tools/bo/discrete_search.py`.
- One throwaway pass (seeds 6091–6100, already spent) runs after every Editor launch.
- The Editor was relaunched every 100 passes.

### θ is pinned at nominal, with the mask pinned too

Every candidate runs through the fixed-θ slot path: `theta_space_014.reference_theta(c)`, i.e. length scale 1, class-reference k = √(lo·hi), ζ 0.7, inertia scale 1, with the candidate's mask. A masked group is held stiffly at its neutral pose (acceleration drive, k 4000, b 200), exactly as in training.

**The two numbers are not comparable**:
- The 014CD held-out eval (0.853 upright / 0.493 lying) drew θ at random per episode, mask bits included. Its 300 rows hold 202 distinct masks and 9 `activeGroups` values. That eval therefore averages over partially masked, off-nominal hands.
- The fully powered nominal hand scores:
  - 0.984 / 0.812 on the search block;
  - 0.987 / 0.687 on the confirmation seeds.

**Sanity check, re-based.** The first run stopped on the brief's 0.60–0.75 band, which came from the randomized eval: J was 0.898, `STOP.txt`. The check was then re-based to |J − 0.898| ≤ 0.05 for the 5-digit all-on configuration. The two completed 5-digit passes were reused: same seeds, kept in commit 34b1391.

### Checks on every pass

A failure stops the run. Checks:
- the row count;
- `activeGroups` == n(m) on every row (a mismatch invalidates the row and stops the run);
- `slots` and `digitCount` match the composition;
- `lenMean` and `inertiaScaleMean` = 1;
- the head mode;
- no NaN or inf;
- the placement columns (`objIndex`, `poseMode`, `poseIndex`, `spawnYaw`) are identical to the first pass of the same seed block;
- the protected hashes at start and end.

**Outcome.** 328 passes were kept (plus 4 throwaways), all valid:
- 0 `activeGroups` mismatches;
- 0 placement mismatches, also across compositions;
- 0 NaN;
- protected hashes unchanged: `Prosthetic_CD.onnx` 68d86b81…, `results/014/Prosthetic.onnx` 164d4273….

### Stages

- **A.** All-on per composition, deterministic and sampled head.
- **B.** Single ablation, deterministic only: all-on with one present group off, 85 candidates. Δ_g = J(all-on) − J(g off).
- **C.** Ordered elimination, deterministic only:
  - remove groups in ascending Δ_g (ties broken by group index), re-evaluating after each removal;
  - stop at the first J < J(all-on) − 0.10.
  - The frontier points are the deepest step with J ≥ J0 − 0.05 and the deepest with J ≥ J0 − 0.10.
- **D.** Confirmation of all-on and the frontier points on fresh seeds, both heads, with:
  - a paired difference against all-on and its 95% CI;
  - McNemar's test;
  - Wilson CIs.

## Group map per composition

Group indices (`HandLayout.GroupNames`): 0–2 index Base/Middle/End, 3–5 middle, 6–8 ring, 9–11 pinky, 12 thumbBase (CMC), 13 thumbEnd, 14–16 extra (6th finger, ulnar) Base/Middle/End, 17 thumb2Base, 18 thumb2End.

| comp | slots | present groups | n | n present − masked = `activeGroups` |
|---|---|---|---|---|
| 3 | 1110000 | 0–5, 12, 13 | 8 | yes |
| 4 | 1111000 | 0–8, 12, 13 | 11 | yes |
| 5 | 1111100 | 0–13 | 14 | yes |
| 6a | 1111110 | 0–16 | 17 | yes |
| 6b | 1111101 | 0–13, 17, 18 | 16 | yes |
| 7 | 1111111 | 0–18 | 19 | yes |

## Stage A: all-on (search block, n = 128 pooled / 64 per pose)

File: `allon.csv`.

| comp | J det | 95% CI | upright det | lying det | J sam | upright sam | lying sam |
|---|---|---|---|---|---|---|---|
| 3 | 0.641 | 0.555–0.719 | 0.891 | 0.391 | 0.609 | 0.750 | 0.469 |
| 4 | 0.820 | 0.745–0.877 | 0.969 | 0.672 | 0.750 | 0.906 | 0.594 |
| 5 | **0.898** | 0.834–0.940 | 0.984 | 0.812 | 0.742 | 0.875 | 0.609 |
| 6a | 0.852 | 0.780–0.903 | 0.938 | 0.766 | 0.766 | 0.875 | 0.656 |
| 6b | 0.844 | 0.771–0.897 | 0.922 | 0.766 | 0.719 | 0.828 | 0.609 |
| 7 | 0.781 | 0.702–0.844 | 0.891 | 0.672 | 0.641 | 0.750 | 0.531 |

## Stage B: single ablation (deterministic head, search block)

File: `ablation.csv`.

**How to read the Δ values.**
- Δ is the pooled-J drop when that one group is masked.
- One 128-episode J has a standard error of about 0.03–0.04, so a Δ of about ±0.05 is within noise. Beyond that comes the pass-level drift described under [Caveats](#caveats).
- Flags: **[TB]** thumbBase, **[T]** thumbEnd, **[T2]** second-thumb groups.

**Ranks, by descending Δ.** Each entry is group, Δ, then J (upright / lying) with that group off.

**3** (J0 0.641)
1. middleBase +0.336, J 0.305 (0.359 / 0.250)
2. **thumbBase [TB] +0.242**, J 0.398 (0.688 / 0.109)
3. indexBase +0.180
4. middleMiddle +0.102
5. middleEnd +0.078
6. thumbEnd [T] +0.031
7. indexEnd +0.008
8. indexMiddle −0.008

**4** (J0 0.820)
1. **thumbBase [TB] +0.398**, J 0.422 (0.734 / 0.109)
2. ringBase +0.070
3. middleBase +0.055
4. middleEnd +0.047
5. ringEnd +0.039
6. indexBase +0.023
7. middleMiddle +0.008
8. ringMiddle +0.008
9. thumbEnd [T] +0.008
10. indexEnd −0.016
11. indexMiddle −0.023

**5** (J0 0.898)
1. **thumbBase [TB] +0.461**, J 0.438 (0.672 / 0.203)
2. indexBase +0.070
3. middleBase +0.070
4. ringBase +0.047
5. ringEnd +0.047
6. pinkyBase +0.039
7. indexMiddle +0.031
8. middleMiddle +0.031
9. indexEnd +0.023
10. pinkyEnd +0.016
11. thumbEnd [T] +0.016
12. middleEnd +0.008
13. ringMiddle +0.008
14. pinkyMiddle +0.000

**6a** (J0 0.852)
1. **thumbBase [TB] +0.562**, J 0.289 (0.422 / 0.156)
2. middleBase +0.070
3. ringMiddle +0.055
4. ringEnd +0.055
5. indexBase +0.047
6. pinkyBase +0.039
7. pinkyEnd +0.039
8. extraEnd +0.039
9. indexMiddle +0.016
10. indexEnd +0.016
11. middleEnd +0.016
12. ringBase +0.008
13. pinkyMiddle +0.000
14. thumbEnd [T] +0.000
15. extraBase +0.000
16. extraMiddle −0.016
17. middleMiddle −0.023

**6b** (J0 0.844)
1. **thumbBase [TB] +0.359**, J 0.484 (0.812 / 0.156)
2. middleBase +0.055
3. ringMiddle +0.055
4. **thumb2Base [T2] +0.039**, J 0.805 (0.922 / 0.688)
5. middleEnd +0.031
6. ringBase +0.031
7. thumbEnd [T] +0.031
8. pinkyBase +0.016
9. pinkyMiddle +0.016
10. indexMiddle +0.008
11. ringEnd +0.008
12. indexEnd +0.000
13. middleMiddle +0.000
14. indexBase −0.023
15. **thumb2End [T2] −0.023**, J 0.867 (0.969 / 0.766)
16. pinkyEnd −0.031

**7** (J0 0.781)
1. **thumbBase [TB] +0.383**, J 0.398 (0.562 / 0.234)
2. middleBase +0.086
3. ringBase +0.078
4. ringEnd +0.078
5. indexBase +0.070
6. extraEnd +0.062
7. ringMiddle +0.055
8. middleMiddle +0.039
9. extraMiddle +0.039
10. **thumb2End [T2] +0.031**, J 0.750 (0.891 / 0.609)
11. pinkyMiddle +0.023
12. indexMiddle +0.016
13. thumbEnd [T] +0.016
14. **thumb2Base [T2] +0.016**, J 0.766 (0.844 / 0.688)
15. indexEnd −0.008
16. middleEnd −0.008
17. pinkyBase −0.008
18. pinkyEnd −0.008
19. extraBase −0.008

**Reading.**
- **thumbBase** ranks first in every composition with 4 or more digits, and its loss is concentrated on lying objects (lying J 0.11–0.23).
- In the 3-digit hand, middleBase ranks above thumbBase; see the tripod-gate caveat.
- **thumbEnd** costs 0.000–0.031 everywhere.
- **Next tier:** the finger base joints (index, middle, ring) at +0.05–0.09.
- The four second-thumb ablations range from −0.023 to +0.039, all within noise.

## Stage C: elimination paths (deterministic head, search block)

File: `elimination.csv`. Columns: J (upright / lying), drop = J0 − J. **f05** / **f10** mark the deepest step with drop ≤ 0.05 / ≤ 0.10. The last step listed in each composition is the one that crossed −0.10.

**3**
| step | removed | n_on | J (upright / lying) | drop | frontier |
|---|---|---|---|---|---|
| 0 | — | 8 | 0.641 (0.891 / 0.391) | 0 | |
| 1 | indexMiddle | 7 | 0.648 (0.781 / 0.516) | −0.008 | |
| 2 | indexEnd | 6 | 0.586 (0.734 / 0.438) | 0.055 | |
| 3 | thumbEnd | 5 | 0.594 (0.766 / 0.422) | 0.047 | **f05 = f10** |
| 4 | middleEnd | 4 | 0.531 (0.719 / 0.344) | 0.109 | stop |

**4**
| step | removed | n_on | J (upright / lying) | drop | frontier |
|---|---|---|---|---|---|
| 0 | — | 11 | 0.820 (0.969 / 0.672) | 0 | |
| 1 | indexMiddle | 10 | 0.844 (0.984 / 0.703) | −0.023 | |
| 2 | indexEnd | 9 | 0.820 (0.953 / 0.688) | 0 | |
| 3 | middleMiddle | 8 | 0.797 (0.969 / 0.625) | 0.023 | **f05** |
| 4 | ringMiddle | 7 | 0.742 (0.938 / 0.547) | 0.078 | |
| 5 | thumbEnd | 6 | 0.734 (0.906 / 0.563) | 0.086 | **f10** |
| 6 | indexBase | 5 | 0.648 (0.828 / 0.469) | 0.172 | stop |

**5**
| step | removed | n_on | J (upright / lying) | drop | frontier |
|---|---|---|---|---|---|
| 0 | — | 14 | 0.898 (0.984 / 0.812) | 0 | |
| 1 | pinkyMiddle | 13 | 0.898 (1.000 / 0.797) | 0 | |
| 2 | middleEnd | 12 | 0.883 (0.984 / 0.781) | 0.016 | **f05 = f10** |
| 3 | ringMiddle | 11 | 0.797 (0.953 / 0.641) | 0.102 | stop |

**6a**
| step | removed | n_on | J (upright / lying) | drop | frontier |
|---|---|---|---|---|---|
| 0 | — | 17 | 0.852 (0.938 / 0.766) | 0 | |
| 1 | middleMiddle | 16 | 0.867 | −0.016 | |
| 2 | extraMiddle | 15 | 0.852 | 0 | |
| 3 | pinkyMiddle | 14 | 0.883 | −0.031 | |
| 4 | thumbEnd | 13 | 0.867 | −0.016 | |
| 5 | extraBase | 12 | 0.867 (1.000 / 0.734) | −0.016 | **f05 = f10** |
| 6 | ringBase | 11 | 0.750 (0.859 / 0.641) | 0.102 | stop |

**6b**
| step | removed | n_on | J (upright / lying) | drop | frontier |
|---|---|---|---|---|---|
| 0 | — | 16 | 0.844 (0.922 / 0.766) | 0 | |
| 1 | pinkyEnd | 15 | 0.875 | −0.031 | |
| 2 | indexBase | 14 | 0.844 | 0 | |
| 3 | **thumb2End** | 13 | 0.867 (0.938 / 0.797) | −0.023 | |
| 4 | indexEnd | 12 | 0.797 (0.906 / 0.688) | 0.047 | **f05** |
| 5 | middleMiddle | 11 | 0.750 | 0.094 | |
| 6 | indexMiddle | 10 | 0.758 (0.875 / 0.641) | 0.086 | **f10** |
| 7 | ringEnd | 9 | 0.703 | 0.141 | stop |

**7**
| step | removed | n_on | J | drop | frontier |
|---|---|---|---|---|---|
| 0 | — | 19 | 0.781 (0.891 / 0.672) | 0 | |
| 1 | indexEnd | 18 | 0.805 | −0.023 | |
| 2 | middleEnd | 17 | 0.773 | 0.008 | |
| 3 | pinkyBase | 16 | 0.727 | 0.055 | |
| 4 | pinkyEnd | 15 | 0.773 | 0.008 | |
| 5 | extraBase | 14 | 0.758 | 0.023 | |
| 6 | indexMiddle | 13 | 0.758 | 0.023 | |
| 7 | thumbEnd | 12 | 0.781 | 0 | |
| 8 | **thumb2Base** | 11 | 0.727 | 0.055 | |
| 9 | pinkyMiddle | 10 | 0.727 | 0.055 | |
| 10 | **thumb2End** | 9 | 0.781 | 0 | |
| 11 | middleMiddle | 8 | 0.734 | 0.047 | |
| 12 | extraMiddle | 7 | 0.750 (0.922 / 0.578) | 0.031 | **f05 = f10** |
| 13 | ringMiddle | 6 | 0.633 (0.859 / 0.406) | 0.148 | stop |

**Notes.**
- Where **f05 = f10**, one point is both frontiers. It was confirmed once and appears in `pareto.csv` as `f010=f005`.
- **7-digit frontier.** It keeps 7 of 19 groups on: indexBase, middleBase, ringBase, ringMiddle, ringEnd, extraEnd and thumbBase. The pinky, thumbEnd and both second-thumb groups are fully masked.
- **Determinism check.** Step 1 of each path is a Stage B candidate and was re-run. 9 of 12 passes reproduced bit for bit; 3 did not. See [Caveats](#caveats) and `determinism_C.txt`.

## Stage D: confirmation (seeds 13201–13500 per pose, n = 300 per pose, 600 pooled)

Files: `pareto.csv` (deterministic head) and `confirm.csv` (both heads, per pose and pooled).

### Pooled J and its difference from all-on

d is the paired difference against the composition's all-on hand on the same 600 rows, with its 95% CI and McNemar's p.

| comp | point | groups on | pooled J det [Wilson 95%] | d det [95%], p | pooled J sam | d sam [95%], p |
|---|---|---|---|---|---|---|
| 3 | all-on | 8/8 | 0.623 [0.584, 0.661] | | 0.565 | |
| 3 | f05 = f10 | 5/8 | 0.548 [0.508, 0.588] | **−0.075** [−0.114, −0.036], p 2e-4 | 0.500 | −0.065 [−0.107, −0.023], p 0.003 |
| 4 | all-on | 11/11 | 0.825 [0.793, 0.853] | | 0.737 | |
| 4 | f05 | 8/11 | 0.778 [0.743, 0.810] | −0.047 [−0.080, −0.013], p 0.008 | 0.653 | −0.083 [−0.121, −0.046], p 3e-5 |
| 4 | f10 | 6/11 | 0.685 [0.647, 0.721] | **−0.140** [−0.177, −0.103], p 4e-13 | 0.610 | −0.127 [−0.165, −0.089], p 2e-10 |
| 5 | all-on | 14/14 | **0.837** [0.805, 0.864] | | 0.745 | |
| 5 | f05 = f10 | 12/14 | **0.850** [0.819, 0.876] | +0.013 [−0.011, +0.037], p 0.34 | 0.730 | −0.015 [−0.047, +0.017], p 0.42 |
| 6a | all-on | 17/17 | 0.840 [0.809, 0.867] | | 0.703 | |
| 6a | f05 = f10 | 12/17 | 0.832 [0.800, 0.860] | −0.008 [−0.039, +0.022], p 0.67 | 0.713 | +0.010 [−0.026, +0.046], p 0.65 |
| 6b | all-on | 16/16 | 0.825 [0.793, 0.853] | | 0.677 | |
| 6b | f05 | 12/16 | 0.792 [0.757, 0.822] | −0.033 [−0.066, −0.001], p 0.054 | 0.700 | +0.023 [−0.013, +0.059], p 0.24 |
| 6b | f10 | 10/16 | 0.752 [0.716, 0.785] | −0.073 [−0.107, −0.040], p 3e-5 | 0.590 | −0.087 [−0.128, −0.046], p 5e-5 |
| 7 | all-on | 19/19 | 0.765 [0.730, 0.797] | | 0.623 | |
| 7 | f05 = f10 | 7/19 | 0.725 [0.688, 0.759] | −0.040 [−0.079, −0.001], p 0.053 | 0.583 | −0.040 [−0.082, +0.002], p 0.074 |

### Per pose, deterministic head [Wilson 95%]

| comp | point | upright | lying |
|---|---|---|---|
| 3 | all-on | 0.870 [0.827, 0.903] | 0.377 [0.324, 0.433] |
| 3 | f05 = f10 | 0.750 [0.698, 0.796] | 0.347 [0.295, 0.402] |
| 4 | all-on | 0.977 [0.953, 0.989] | 0.673 [0.618, 0.724] |
| 4 | f05 | 0.973 [0.948, 0.986] | 0.583 [0.527, 0.638] |
| 4 | f10 | 0.897 [0.857, 0.926] | 0.473 [0.418, 0.530] |
| 5 | all-on | 0.987 [0.966, 0.995] | 0.687 [0.632, 0.737] |
| 5 | f05 = f10 | 0.987 [0.966, 0.995] | 0.713 [0.660, 0.762] |
| 6a | all-on | 0.953 [0.923, 0.972] | 0.727 [0.674, 0.774] |
| 6a | f05 = f10 | 0.960 [0.931, 0.977] | 0.703 [0.649, 0.752] |
| 6b | all-on | 0.963 [0.936, 0.979] | 0.687 [0.632, 0.737] |
| 6b | f05 | 0.933 [0.899, 0.956] | 0.650 [0.594, 0.702] |
| 6b | f10 | 0.907 [0.868, 0.935] | 0.597 [0.540, 0.651] |
| 7 | all-on | 0.863 [0.820, 0.898] | 0.667 [0.612, 0.718] |
| 7 | f05 = f10 | 0.860 [0.816, 0.895] | 0.590 [0.534, 0.644] |

### Pareto reading (pooled deterministic J against powered groups)

**On the frontier:**
- **5-digit with 12/14 groups on** (pinkyMiddle and middleEnd masked): **0.850**, the highest confirmed J at any group count.
- **5-digit all-on (14):** 0.837.
- **6a with 12/17 on:** 0.832.
- **4-digit with 8/11 on:** 0.778.
- **7-digit with 7/19 on:** 0.725.
- **4-digit with 6/11 on:** 0.685.
- **3-digit with 5/8 on:** 0.548.

**Dominated.** 6b (0.825 with 16 groups, 0.792 with 12) and 7 (0.765 with 19) are dominated by the 5-digit points.

**Hand mass and palm width.** These are not part of J. The 5-digit hand is the lightest of the 5–7 digit hands (411 g, 85 mm palm; see `results/014/VIEWER.md`).

### The search block overstated two frontiers

The frontier steps were chosen on the 128-episode search block, so the search block is optimistic about them. Fresh-seed confirmation moved two points past their thresholds:
- **3-digit f05:** search drop 0.047, confirmed 0.075. It fails the −0.05 criterion.
- **4-digit f10:** search 0.086, confirmed 0.140. It fails the −0.10 criterion.

The other points stayed within their thresholds: 4 f05 0.047; 5 −0.013; 6a 0.008; 6b f05 0.033 and f10 0.073; 7 0.040.

The search block was also easier than the confirmation seeds for lying objects: the 5-digit all-on lying score is 0.812 on the search block against 0.687 on confirmation.

## The second-thumb question

**Answer: no.** At nominal θ, 014CD gets nothing from a second thumb, whether added to the 5-digit hand (6b) or to the 6-digit hand (7 against 6a). All comparisons below are on the identical 600 Stage D rows (0 placement mismatches across compositions).

**Adding it to the 5-digit hand (6b − 5):**
- deterministic: −0.012 [−0.041, +0.017], p 0.50;
- sampled: −0.068 [−0.106, −0.031], p 5e-4.

It is no better with the deterministic head and worse with the sampled head.

**Adding it to the 6-finger hand (7 − 6a):**
- deterministic: **−0.075** [−0.111, −0.040], p 5e-5;
- sampled: −0.080 [−0.120, −0.040], p 1e-4.

It is significantly worse with both heads, in both poses: upright −0.090, lying −0.060 (deterministic).

**Inside the hands that have one:**
- Masking it alone costs nothing measurable:
  - 6b: thumb2Base Δ +0.039, thumb2End −0.023;
  - 7: thumb2End +0.031, thumb2Base +0.016.
- Both elimination paths shed it before the −0.05 frontier:
  - 6b removes thumb2End at step 3, and J goes up 0.023.
  - 7 removes thumb2Base and thumb2End at steps 8 and 10. The confirmed 7-digit frontier has both second-thumb groups masked and loses only 0.040.
- The primary **thumbBase**, by contrast, is the most critical group in 6b and 7 (Δ +0.359 and +0.383).

**For comparison, the sixth finger.** 6a − 5 deterministic: +0.003 [−0.024, +0.031], p 0.91. 6a is no worse than 5 pooled, but upright is −0.033 (p 0.006) and lying +0.040 (p 0.16). Sampled 6a − 5: −0.042 (p 0.03).

**One more cross-composition result.** 4 − 5 deterministic: −0.012 [−0.039, +0.016], p 0.48. On confirmation the 4-digit hand is not distinguishable from the 5-digit hand. The search-block gap (0.820 against 0.898) did not hold up.

## Caveats

- **Common-random-number search block.** Every Stage A–C candidate sees the same 128 placements (16 shapes × 4 seeds per pose). This makes Δ comparisons sharper, but it also means the frontier points were selected on this specific block. The block's lying placements are easier than the confirmation seeds' (5-digit lying 0.812 against 0.687). The selection optimism is visible in two of the confirmed frontiers.
- **Greedy path, not a combinatorial search.**
  - Groups are removed in order of their single-ablation Δ, and pairwise interactions are not searched.
  - Single-ablation Δ near zero sits within noise (SE ≈ 0.03–0.04 per J), so the order among the first several removals is largely noise.
  - The paths show non-monotone J (for example, 7-digit steps 3 → 4 and 9 → 10).
- **Training distribution.** The policy was trained with per-episode mask randomization, so masked hands are in-distribution as a class. It was not trained on these particular elimination orders or on fixed masks held for a whole pass. All numbers are for nominal θ, not the randomized θ the policy usually sees.
- **3-digit hand: tripod gate with zero slack.**
  - The hold gate (`ArmGraspAgent`, unchanged) requires ≥ 6 touching groups, ≥ 2 distinct non-thumb digits, and thumb contact.
  - The 3-digit hand has exactly two non-thumb digits (index and middle) and 8 groups. Every digit must therefore make contact, and at most two groups may fail to touch.
  - This is why middleBase (+0.336) and indexBase (+0.180) rank so high there. It also explains why the 3-digit frontier is shallow, and why its confirmed f05 costs 0.075, mostly upright (−0.120).
  - Masked groups still count as contacts when they touch, since they are held at neutral, not removed.
- **Pass-level reproducibility drifted in 3 of 12 checks.**
  - Re-running the Stage B step-1 candidates in Stage C reproduced 9 of 12 passes bit for bit.
  - Three differed: 3-digit lying (from seed 13125: 4 rows, same k 33/64); 6a upright (from 13002: 63 rows, 61 → 60); 7-digit upright (from 13015: 50 rows, 57 → 59).
  - In each, the rows are identical up to one episode, and then the carried-over state diverges (`academyStepAtBegin`, `phaseAtBegin`, `preResetTiltDeg`).
  - Placements stay identical, so pairing remains valid. The drift adds about 0–2/64 per pass to the search-block noise.
  - The cause is not diagnosed. It does not correlate with the runner race below.
  - It weakens the earlier finding that the pass is the reproducible unit (commit 1716818) for this setup.
- **Runner race.**
  - `gates_runner.run_pass` checks for DONE every 5 s but for Play mode every 30 s. A pass of about 30 s can write DONE and leave Play in between, and the runner then reports rc = 4.
  - `discrete_search.py` accepts rc = 4 only when the CSV ends in DONE, and the pass still goes through full validation. 30 passes were accepted this way (`runner_race: true` in `passes.json`).
  - One pass, `A_c3_allon_canonical_sam`, stopped the first A+B run on this race before the handling existed. It was re-run.
  - `gates_runner.py` was not edited.
- **Sampled head.** It was run in Stages A and D only; B and C used the deterministic head.

## Seeds spent

- **13001–13128:** search block. Upright 13001–13064 and lying 13065–13128, used by every candidate in Stages A–C.
- **13201–13500:** confirmation, both poses.
- **6091–6100:** throwaway passes after each Editor launch (already spent).

The next fresh held-out block starts at **13501**. 13129–13200 are unused but sit between spent blocks; skip them for cleanliness.

## Files

**Driver:** `tools/bo/discrete_search.py`.

**Under `results/bo/discrete/`:**
- **Stage results:** `allon.csv`, `ablation.csv`, `elimination.csv`, `determinism_C.txt`, `confirm.csv`, `pareto.csv`.
- **Passes:** `passes/` (328 pass CSVs plus 4 throwaways) and `passes.json` (tag, hash, k, n, block, runner_race).
- **Logs:** `run_AB.log` (the first run, which stopped on sanity), `run_AB2.log`, `run_CD.log`, `STOP.txt`.
