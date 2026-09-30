# Part C: first BO campaign over morphology on the run-013 substrate (2026-09-29)

**Substrate: 013D** (Prosthetic-20000063, onnx 96ac32e6). 013E failed the substrate gate on the anchor (0.883 < 0.95) while passing
upright and lying (`results/013/013E/PART1.md`).

## Headline

| held-out shapes 65-80, seeds 11501-11800, n = 300 per presentation | canonical | random stable | both (n = 600) |
|---|---|---|---|
| reference (spec nominal hand) | 0.970 | 0.740 | 0.855 |
| BO best (bo #68, campaign J 0.930) | 0.970 | 0.773 | 0.872 |
| random best (random #4, campaign J 0.898) | 0.943 | 0.830 | 0.887 |
| **BO-best − reference [95 % paired CI]** | +0.000 [−0.024, +0.024] | +0.033 [−0.023, +0.090] | +0.017 [−0.014, +0.048] |
| **BO-best − random-best** | +0.027 [−0.001, +0.054] | −0.057 [−0.108, −0.005] | −0.015 [−0.044, +0.014] |
| BO-best with the thumb-base bit OFF − BO-best | −0.967 [−0.987, −0.946] | −0.530 [−0.592, −0.468] | −0.748 [−0.785, −0.711] |

- **Held-out result.** BO found no θ that beats the nominal hand, and did no better than random search. The best BO θ is within noise of
  the reference in both presentations. It is not better than the best random θ: level on canonical, worse on random stable.
- **Inside the campaign, BO did its job.**
  - The BO arm's sequential points averaged J 0.877 against 0.794 for its Sobol design and 0.782 for the random arm.
  - The BO arm reached the highest observed J: 0.930 against 0.898.
  - Those advantages were selection on a noisy 128-episode objective on train shapes. They did not survive re-measurement on held-out
    shapes. All six top θ land at 0.872-0.887 against the reference's 0.855.
- **The one large effect in the space is the thumb-base drive.** Switching it off on the BO-best hand costs 0.97 upright and 0.53 lying,
  the same collapse the Part 3 grids showed on the thumb-masked probe hands. With all mask bits on, the reduced space has little range.
- **The one secondary signal is random-stable wrist stiffness.** The only dimension the GP rates clearly relevant is wrist k, and the BO
  and random best both sit near the top of its range (9.4 and 8.2 N m/rad against 3.2). random1 − reference on random stable is
  +0.090 [+0.042, +0.138], the one significant held-out gain over the reference in the table. It came from the random arm and was not
  a pre-registered comparison, so treat it as a lead, not a result.

## Campaign (3a)

- **Objective.** J(θ) is described in `PART2.md`: 128 episodes on even train shapes 2-64 × both presentations × 2 episodes, seeds
  11001-11128. It costs 72-127 s per evaluation.
- **Arms.**
  - Random: 100 scrambled Sobol points, seed 4242.
  - BO: 30 Sobol points (seed 4243), then 70 GP-EI points. The GP is ARD Matérn-5/2 on standardized inputs, with noise floor 0.002 from
    2c. Acquisition is EI over 4096 uniform candidates plus 512 perturbations (SD 0.05) of the posterior-mean incumbent.
- **Order.** The arms were interleaved: random i, then BO i.
- **Logs.** `campaign.csv` (200 rows); `gp_hparams_030…100.json` every 10 BO points; `drift.csv`; `passes/`.

| arm | mean J | SD | min | max | canonical mean | random-stable mean |
|---|---|---|---|---|---|---|
| random (100) | 0.782 | 0.062 | 0.633 | 0.898 | 0.921 | 0.643 |
| BO (100) | 0.852 | 0.053 | 0.656 | 0.930 | 0.961 | 0.744 |
| BO sequential points 31-100 | 0.877 | | | | | |

Per family, the mean over the arm:

| family | random | BO |
|---|---|---|
| length < 130 mm, canonical | 0.826 | 0.920 |
| length < 130 mm, random stable | 0.722 | 0.867 |
| length 130-210 mm, canonical | 0.958 | 0.984 |
| length 130-210 mm, random stable | 0.624 | 0.688 |
| length 210-280 mm, canonical | 0.972 | 0.975 |
| length 210-280 mm, random stable | 0.588 | 0.686 |

Reference drift was checked every 20 evaluations: 11 checks, maximum |ΔJ| 0.031 (limit 0.10). The value was 0.859 at every check except
after evaluations 100 (0.875) and 120 (0.891). Both of those fell in an Editor session that had slowed from 71 to 127 s per J. After a
planned relaunch at the 140 check, the reference returned byte-identical to the first value (hashes f8816016 / 1329567a).

## Analysis (3b)

`ANALYSIS.md` holds the best-so-far table, `best_so_far.csv` the curves, and the natural-unit comparison of each arm's best θ.

**Best-so-far (observed J):**

| evaluations | random | BO |
|---|---|---|
| 10 | 0.898 | 0.891 |
| 50 | 0.898 | 0.906 |
| 70 | 0.898 | 0.930 |
| 100 | 0.898 | 0.930 |

**Top-10 dimensions by inverse length-scale (final GP, n = 100).** The final fit had signal variance 0.0024 and noise variance 0.0020
(at the floor).

| rank | dimension | length scale | inverse |
|---|---|---|---|
| 1 | k_wrist | 2.13 | 0.470 |
| 2 | zeta_thumb | 6.14 | 0.163 |
| 3 | k_MCP | 7.90 | 0.127 |
| 4 | inertia_DIP | 8.27 | 0.121 |
| 5 | zeta_PIP | 8.93 | 0.112 |
| 6 | zeta_DIP | 9.55 | 0.105 |
| 7 | len_index | 9.81 | 0.102 |
| 8 | k_PIP | 11.02 | 0.091 |
| 9 | len_middle | 12.22 | 0.082 |
| 10 | k_thumb | 13.12 | 0.076 |

Only wrist k is clearly relevant. Every other length scale is at least 6 standardized units, which is nearly flat over the box. Signal
and noise variances are about equal, so single-θ J differences under about 0.05 are not resolvable.

**Best θ against the reference** (natural units):

| dimension | reference | BO best (#68) | random best (#4) |
|---|---|---|---|
| k_wrist | 3.2 | 9.4 | 8.2 |
| k_thumb | 3.9 | 15.0 (upper bound) | 5.8 |
| zeta_thumb | 0.70 | 0.30 (lower bound) | 0.38 |

The other 17 dimensions scatter without a shared direction between the two arms.

## Confirmation (3c, `CONFIRM.md`, `confirm/`)

Eight θ were run, each on both presentations, on held-out shapes with seeds 11501-11800 and passIndex 0. That is 300 episodes per θ and
presentation, paired on (seed, shape) and every harness draw except θ.

- **The θ set:** top-3 BO and top-3 random θ by campaign J, the reference, and BO-best with the thumb-base bit off.
- **Top-3 means over both presentations:** BO 0.872 / 0.883 / 0.882; random 0.887 / 0.877 / 0.880; reference 0.855.

## Deviations

1. **The substrate is 013D, not 013E.** The gate failed on the anchor; this was not a stop.
2. **One J is two harness plays, one per presentation.** The harness sets the pose mode per play. It is still one realization of the 128
   specified episodes per θ. Canonical uses seeds 11001-11064 and random stable 11065-11128.
3. **An Editor-only harness key was added to reach the even train shapes 2-64.** ArticulatedGates cannot stratify with a stride. The key
   is objectIndexStride in `Assets/Scripts/Objects/ObjectBank.cs`, committed with Part 1; the player never reads it. Every J checked 4
   episodes per even shape.
4. **The GP fit was repaired after BO point 30.**
   - A single L-BFGS start converged at n = 30 to an all-noise local optimum: signal variance 9e-10, NLL −43.7.
   - That made EI uninformative for BO point 30 (EI 1e-5, GP SD 3e-5), effectively a pick near the incumbent. It stays in the log.
   - Other starts reach NLL −45.5 / −47.6 with signal variance about 1.3e-3.
   - From point 31 on, the fit uses 6 starts (log length-scale 0 / 0.7 / 1.4 × two signal/noise splits) and keeps the lowest NLL.
     The change is in tools/bo/campaign_013.py; gp.py is unchanged for Part B reproducibility.
   - The campaign was restarted between evaluations. It is resumable, so one in-flight random point was re-run.
5. **Two planned Editor relaunches.** One came before 3c. The other came at the drift-140 checkpoint, because per-J time had risen from
   71 to 127 s, which would have hit the 180 s limit. Both were followed by a throwaway pass. One relaunch showed the known
   Package-Manager dialog, and its Continue button was clicked.
6. **The 3c ranking used observed campaign J for both arms.** The random arm has no GP to rank by posterior mean.

## Open questions

1. **Is the objective too saturated and too narrow to steer morphology?** Canonical J at the reference is 0.97 on train and held-out
   shapes alike. The random-stable half carries almost all the range, and the GP's fitted noise (SD 0.045) is as large as its fitted
   signal (SD 0.049). A random-stable-only or harder objective, or more episodes per θ, would give BO something to find.
2. **Should wrist stiffness be pursued?** It is the only relevant dimension, and random1's +0.090 on random stable points the same way. A
   one-dimensional sweep of wrist k, or a confirmation of the high-k_wrist region with a fresh seed block, would settle it.
3. **Should the thumb-base bit enter the space as a decision variable?** It dominates everything else, but that says more about 013D's
   thumb-driven grasp than about the hand. Keeping it on (as here) is the sensible default for this substrate.
4. **Should the Editor be relaunched on a schedule?** It slows down after 2.5-3 h of passes, and the only reference drift away from
   0.859 came in that state. A relaunch every about 100 evaluations should be standard for long campaigns.
