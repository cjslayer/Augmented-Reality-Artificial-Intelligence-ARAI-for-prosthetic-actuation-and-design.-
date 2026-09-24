# Run 011b: stage-1 analyses (2026-09-22/23)

Model `results/011b/Prosthetic.onnx` (Prosthetic-6000087, sha256 0268b0e3...), Editor eval harness (`ArticulatedGates` mode
`eval`, sampled head), seeds 5001-5100, K = 50, perturbation scale 1.0, theta resampled per episode, dt 10 ms unless stated.
Files in this folder: `eval011b_final_mu{06,10,15}.csv` (the corrected evaluation, schema of the 011b eval plus
`dtMs,pulseStep1..3,lastPulseStep,pulseActiveAtDrop`), `eval011b_rerun_mu*.csv` and `eval011b_5ms_mu*.csv` (the raw
re-runs, see "Validity" below), `tables_*.md` and the per-table CSVs, `eval_runs*.log`.

## Validity of the evaluations (found while re-running with the extra logging)

1. **The friction override never reached the physics.** `ArmGraspAgent` builds the object's `PhysicsMaterial` once at
   `Initialize` (`ArmGraspAgent.cs:296-299`), before the harness's `EvalSetup` sets `objectFriction`; the CSV `mu` column
   printed the field, not the material. The re-run proved it: the mu 0.6, 1.0 and 1.5 passes of one session were
   episode-identical (100/100 identical step counts). So the 22:50 "pass-vs-mu 0.59 / 0.51 / 0.52" was three evaluations
   at mu 1.0, and their spread is the run-to-run noise of the sampled policy with identical theta (about +-0.04 at n = 100).
   Fixed in the harness (commit a834d81: the override is applied to the live material; the log now prints the material's
   mu and combine mode) and the mu 0.6 / 1.5 passes were re-run. **Corrected held-out numbers:**

   | mu (object material, combine Maximum) | success | lifted | palm at end | mean contacts | mean hold steps | ends |
   |---|---|---|---|---|---|---|
   | 0.6 | 0.48 | 0.95 | 0.69 | 4.32 | 297 | 48 success, 47 drop, 5 no contact |
   | 1.0 | 0.54 | 0.98 | 0.77 | 4.23 | 316 | 54 success, 44 drop, 2 no contact |
   | 1.5 | 0.38 | 0.93 | 0.63 | 3.67 | 244 | 38 success, 56 drop, 6 no contact |

2. **Reproduction check (same model, same seeds, sampled head).** mu 1.0: original 0.51, re-run 0.54 (+0.03, inside the
   +-0.03 criterion; per-seed agreement 57/100, theta identical in 99/100 rows). mu 0.6 and 1.5 have no valid original to
   reproduce (see 1); their "originals" 0.59 and 0.52 were mu 1.0 runs.
3. **Session determinism.** Within one Editor session the sampled policy is deterministic given the episode seed (the
   three passes of the first re-run were identical); across sessions (after a domain reload) it is not. Any comparison
   between runs therefore carries the +-0.04 noise unless the two runs are paired in one session.
4. Two rows per pass end as "maxStep" with `steps = 0`: aborted first episodes after the harness warm-up, counted as failures.

## 1a. Drop timing

Mass terciles from the mu 1.0 file: light < 0.386 kg, mid 0.386-0.762 kg, heavy >= 0.762 kg. Phase at the drop uses the
pulse schedule (hold-step starts, sorted) and `pulseActiveAtDrop`; "within 0.4 s" = the drop is <= 40 physics steps after
a pulse started (a pulse lasts 5 steps).

**Pooled over the three corrected mu passes (300 episodes, 145 hold-phase drops, 8 before-lift drops):**

| mass tercile | n | success | drop before lift | drop before pulse 1 | during a pulse | <= 0.4 s after a pulse | later after a pulse |
|---|---|---|---|---|---|---|---|
| light | 99 | 0.43 | 3 | 35 | 0 | 5 | 10 |
| mid | 99 | 0.43 | 4 | 30 | 1 | 8 | 10 |
| heavy | 102 | 0.53 | 1 | 25 | 1 | 6 | 14 |
| all | 300 | 0.47 | 8 | **90 (62 % of hold drops)** | 2 | 19 (13 %) | 34 (23 %) |

**By pulse index (hold-phase drops):**

| pass | n | before pulse 1 (median hold step) | during 1 / 2 / 3 | <= 0.4 s after 1 / 2 / 3 | later after 1 / 2 / 3 |
|---|---|---|---|---|---|
| mu 0.6 | 47 | 26 (23.5) | 2 / 0 / 0 | 3 / 1 / 1 | 3 / 9 / 2 |
| mu 1.0 | 44 | 29 (24) | 0 / 0 / 0 | 4 / 0 / 1 | 4 / 3 / 3 |
| mu 1.5 | 54 | 35 (24) | 0 / 0 / 0 | 7 / 2 / 0 | 5 / 1 / 4 |
| pooled | 145 | 90 (24) | 2 / 0 / 0 | 14 / 3 / 2 | 12 / 13 / 9 |

Hold step at the drop (pooled): 83 of 145 in the first 50 steps (0.5 s), 16 in 50-99, then a thin tail to 499; median 35
(light 28, mid 36, heavy 65). The first pulse starts at a median hold step of 245, so a drop at step 24 is a grip that
yields right after the lift, with no disturbance yet.

**Which hypothesis the data supports for light objects:** neither. Light-object failures are not flicks during closure
or lift (3 of 53 failures end before the lift, the same rate as the other terciles), and they are not knocked out by
pulses (5 of 50 hold drops fall within 0.4 s of a pulse; none during one). 35 of 50 light hold drops happen before the
first pulse, median hold step 23, exactly like mid and heavy objects. Mass is not a driver (0.43 / 0.43 / 0.53). The
dominant failure at every mass is **early-hold instability: the grasp lets go in the first half second after the lift**.
Pulse-related drops (during or <= 0.4 s after) are 21 of 145 (14 %); the 34 "later" drops (median 120 steps after the
last pulse) are not attributable to a pulse either.

## 1b. Contacts at the drop vs at success (pooled over mu)

| mass tercile | group | n | contacts | distinct fingers | palm | thumb |
|---|---|---|---|---|---|---|
| light | success end | 43 | 5.33 | 2.65 | 0.91 | 1.00 |
| light | drop (hold) | 50 | 2.76 | 1.40 | 0.42 | 0.52 |
| mid | success end | 43 | 4.88 | 2.51 | 0.95 | 0.98 |
| mid | drop (hold) | 49 | 2.69 | 1.41 | 0.41 | 0.37 |
| heavy | success end | 54 | 5.46 | 2.72 | 0.98 | 1.00 |
| heavy | drop (hold) | 46 | 3.93 | 2.11 | 0.59 | 0.67 |
| all | success end | 140 | 5.24 | 2.64 | 0.95 | 0.99 |
| all | drop (hold) | 145 | 3.11 | 1.63 | 0.47 | 0.52 |
| all | drop (before lift) | 8 | 2.75 | 1.50 | 0.38 | 0.38 |

A successful hold ends with ~5 contacts on ~2.6 finger groups plus palm and thumb (95-99 %). At the drop step contacts are
3.1 on 1.6 groups and palm / thumb engagement is ~50 %. Contacts at the drop are measured after separation has begun, so
this is partly consequence; the robust reading is that failing grasps recruited fewer finger groups and lost the
thumb / palm opposition. Heavy objects keep more contacts at the drop (3.9) than light ones (2.8).

Other bins (corrected mu 1.0 pass): active groups <= 9: 0.54 (n 13), 10-11: 0.63 (n 41), 12-14: 0.46 (n 46), no
monotone trend; finger length scale terciles flat (see `success_by_theta_bins.csv`).

## 1c. Stiff-tercile solver check

**Time-constant audit** (`ArmGraspAgent`, pulse, harness; full list in `MLAGENTS_UPGRADE.md`). dt lives in code
(`ArticulatedHand.fixedTimestep = 0.01`, applied in Awake; `ProjectSettings/TimeManager.asset` 0.02 is dead) and
DecisionPeriod in the scene (10). Classification at dt 5 ms / DecisionPeriod 20:

| constant | value | class | conversion |
|---|---|---|---|
| decision cadence | DecisionPeriod 10 | steps | scene 10 -> 20 (agent caches it at Initialize) |
| hold requirement | K x DecisionPeriod | decisions | automatic |
| pulse start times | uniform over the hold, redrawn per hold entry | steps, hold-relative | automatic (distribution in wall time preserved) |
| pulse duration `perturbPulseSteps` | 5 | steps | 5 -> 10 |
| pulse force / torque | 1.5 x weight x U(0.5, 1), `ForceMode.Force` | N, continuous | automatic (impulse = F x len x dt) |
| `MaxStep` | 5000 | steps | 5000 -> 10000 |
| `liftBudgetSteps` | 200 | steps | 200 -> 400 |
| `taskBudgetMarginSteps` | 100 | steps | 100 -> 200 (task budget then derived) |
| hold-pay window / rate | 50 steps x 0.004 | steps, reward per step | 50 -> 100 and 0.004 -> 0.002 together (also an observation) |
| existential penalty | 1 / MaxStep per step | per step, self-normalising | automatic once MaxStep is doubled |
| effort / safety weights | 2e-5, 1e-4 per step | reward per step | halve (reward only, no effect on evaluation) |
| finger / wrist setpoint integration, arm servo | deg/s x Time.fixedDeltaTime, position windup | dt-aware | automatic |
| drop rule, contact gate | instantaneous predicates | none | automatic (checked twice as often) |
| drives, damping, MorphologyManager | physical units | seconds | automatic |
| solver iterations 16 / 4 | per step | per step | **not a time constant: twice the solver work per second at 5 ms; inherent to the test** |

All step-denominated task constants convert by field values, so the test was run. Deviations from the brief: no
player was built, because the eval harness is Editor-only; the 5 ms evaluation ran in the Editor with the harness's
`fixedTimestep` override (which sets Time.fixedDeltaTime and the DecisionRequester) and the harness now scales MaxStep,
lift budget, margin, pulse duration, hold-pay window / rate and the per-step weights by 0.01 / dt (commit a834d81); the
scene's DecisionPeriod was set to 20 in memory only (needed because the agent caches it at Initialize) and set back to 10
afterwards; `git status` on Assets/Scenes and ProjectSettings is clean; DynamicsManager.asset was not touched. The Editor
log of each 5 ms pass confirms `DecisionPeriod(agent cache)=20`, MaxStep 10000, lift 400, margin 200, pulse 10 steps,
hold-pay 100 steps.

**Success by stiffness tercile at mu 1.0** (edges 1.276 / 1.669 N m/rad from the mu 1.0 file, n = 33 / 33 / 34,
binomial SE about 0.085 per cell):

| pass | dt | overall | low-k | mid-k | high-k |
|---|---|---|---|---|---|
| original session (22:50) | 10 ms | 0.51 | 0.48 | 0.67 | **0.38** |
| re-run, same seeds and theta | 10 ms | 0.54 | 0.33 | 0.70 | **0.59** |
| re-run | 5 ms, DecisionPeriod 20 | 0.51 | 0.42 | 0.52 | 0.59 |

**Decision.** The stiffest-tercile deficit did not survive a re-run at the training dt with identical theta and seeds
(0.38 -> 0.59, +0.21, while low-k went 0.48 -> 0.33): the per-tercile numbers move by two standard errors from session
to session of the sampled policy, so there was no stable deficit to test. At 5 ms the high-k tercile is unchanged
(0.59), mid-k drops 0.18 and low-k rises 0.09; the terciles neither shift together nor does a stiff-tercile deficit
shrink. Conclusion: the stiff-tercile weakness reported for 011b is sampling noise at n = 33, not a solver artifact and
not a real deficit. Secondary result: the policy transfers to dt 5 ms with an overall change of -0.03 (0.54 -> 0.51),
so the hold task is not sensitive to the 10 ms solver step.

## Consequences for the next stages

- Run 012 (same recipe, longer) is unaffected by these findings; nothing here argues for a physics or reward change.
- Any pass-vs-mu number quoted for 011b must be the corrected one (0.48 / 0.54 / 0.38 at mu 0.6 / 1.0 / 1.5).
- Per-tercile theta screening at n = 100 per pass cannot resolve differences below about 0.2; treat it as a smoke check.
- The 012 vs 011b paired comparison must be run in one Editor session per mu to remove the session noise.

## ADDENDUM 09-24 (harness closeout)

Every eval CSV produced before commit 1716818 logged the theta columns of the NEXT episode on each row (the harness read
`MorphologyManager` live after the following reset; `RNG_INVENTORY.md` section D). Object mass is not affected: it is
captured in the agent's episode record at episode end (`ArmGraspAgent.cs:770`, `mass = m_Mass`) and read from the
record (`r.mass`). Therefore in this document:
- VOID: the stiffness-tercile rows and the 1c stiffness-per-dt table, the active-group bins, the finger-length bins and
  `success_by_theta_bins.csv` / `success_by_stiffness_tercile_per_dt.csv` (theta assigned to the wrong episode). The 1c
  decision is unaffected at the aggregate level (0.54 -> 0.51 at 5 ms), but the "stiff-tercile deficit was noise"
  finding is unsupported either way, because the tercile membership was wrong in both sessions.
- STAND: everything keyed on mass (1a mass-tercile x phase, hold-drop step buckets by mass, 1b contacts by mass), the
  pulse-index tables (pulse columns come from the record), the drop-timing conclusions, the friction-override correction
  and the reproduction check.
Re-derived per-theta tables on the reproducible harness: `results/lineage/LINEAGE_011b_012.md` (closeout Part 3).
