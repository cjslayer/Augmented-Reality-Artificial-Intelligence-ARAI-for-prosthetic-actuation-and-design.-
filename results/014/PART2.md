# Run 014 Part 2: variable digit slots (checkpoint 2, 2026-09-30; stopped before the build)

Commits: 6744248 (baselines on unmodified HEAD), 9fc9b44 "topology: variable digit slots", 9c7a941 (morph/I_* order fix, slot layout). Design and
decisions: `docs/design/014_topology.md`. Line numbers below refer to 9c7a941.

## Results

| check | result | file |
|---|---|---|
| 5-digit slot hand vs the legacy (013) hand: length, radius, capsule height, mass, pivot of every link and the palm | **identical (0 difference; criterion ≤ 1 %)** | `layout/LAYOUT.md` |
| sensors ML-Agents creates, legacy layout | JointTokens[16, 13], 10 ray sensors (9 × 3, RayRingend × 2), VectorSensor[24] | console, `layout/` runs |
| sensors ML-Agents creates, slot layout | **JointTokens[21, 20], VectorSensor[28], no ray sensors**; 24 actions: the OnEnable configuration takes effect without relying on script execution order | console |
| shape guard | the slot layout refuses the 013E model (12 inputs, [16, 13] tokens, [24] vector) and writes REFUSED; a legacy scene model is detached in the slot layout | `layout/guard_slots_013E.csv` |
| slot-layout scripted lift, 7-digit hand, 0.6 kg | success, 500-step hold, 11 contacts, palm contact; max penetration 11.0 mm (5-digit G1: about 1.6 mm), for Part 3 | `layout/smoke_lift_7.csv` |
| no-op proof (default layout) | every recorded 013E pass reproduced byte-identically at least once; the non-identical runs differ by 0.000 / −0.007 / +0.020 (every CI includes 0) and change between runs of the same code (timing channel) | `NOOP.md` |
| scripted gates, default layout vs HEAD | 12 of 13 files byte-identical; G1 not reproducible even on the same code | `NOOP.md` |
| I-order fix | every inertia scale at 2.0: link masses × 2.0000 on all 15 links, slot layout (fix) and legacy (harness path) | `layout/*_inertia2.csv` |

Hands per composition (reference θ):

| hand | slots (thumb, index..pinky, extra, thumb 2) | links | hand mass | palm width |
|---|---|---|---|---|
| 3 digits | 1110000 | 9 | 377.8 g | 85 mm |
| 4 | 1111000 | 12 | 398.5 g | 85 mm |
| 5 (013 hand) | 1111100 | 15 | 411.3 g | 85 mm |
| 6a (+ extra finger) | 1111110 | 18 | 498.1 g | 106 mm |
| 6b (+ second thumb) | 1111101 | 18 | 445.9 g | 85 mm |
| 7 | 1111111 | 21 | 532.7 g | 106 mm |

## What changed (by file, 9c7a941)

- `Assets/Scripts/ArticulatedHand.cs`:
  - `HandLayout` :121 (layout selection `Resolve` :172, `Composition` :152, sizes, group / digit / morphology index maps);
  - `HandSpec.extraFinger` :88;
  - palm widening :381; `BuildFinger` :397 and `BuildThumb` :422 (the mirrored second thumb); build order :467 (the legacy order first);
  - force-limit class :574; `BoneOfGroup` :556.
- `Assets/Scripts/MorphologyManager.cs`:
  - `AllGroupNames` :41, `digitDistribution` :47, `FingerLengthRatio` over present groups :120;
  - existence overrides `morph/digits` / `morph/digits6b` / `morph/exist_*` :197; `DrawComposition` :280;
  - class-based `StiffnessRange` :239; I-order fix :218;
  - legacy draw order unchanged (loops run over the 16 / 14 legacy entries in the legacy layout).
- `Assets/Scripts/ArmGraspAgent.cs`:
  - `OnEnable` :299, `ConfigureLayout` :309, `AssertLayout` :334, `BindTipRays` :358, `BuildTipFrames` :373, `TipRayReading` :397, `VerifySensors` :501;
  - `NeutralDeg` :455; slot observations :630 / `CollectObservationsSlots` :682;
  - arm-action offset :755, effort over present joints :758;
  - contacts / gate, same semantics :810; limits of the new groups :1131; stats by digit count :1020; `LayoutGuard` :1143.
  - **No reward weight changed**: gate thresholds, phase / hold / success / drop rewards, shaping, existential and safety terms are untouched.
    The effort term keeps its weight and sums over present joints (decision 8).
- `Assets/Scripts/Diagnostics/ArticulatedGates.cs`:
  - config `topology` / `digits` / `digits6b` / `liftBudgetSteps` / `thetaExist` :120; digits :194; lift budget :202;
  - model guard :300; pinch set by digit :521; slot θ format :790; late mass scaling off in slots :806;
  - layout dump :818; controller, targets, stability walk and CSV columns over present groups.
  - Audit mode stays legacy-only.
- `MorphologyWatchOverlay.cs`: all slots; tip lookup via the hand's links, replacing the tag lookup that never found the unparented skeleton.
- `CheckpointTheater.cs`: `LayoutGuard` added to its model check.
- Python:
  - `tools/bo/theta_space_014.py`: the slot θ space, bounds cited, 5-digit prefix = the 67-dim space.
  - `tools/eval/summarize_pass.py`: success by digit count and composition.
  - `tools/eval/{gates_014,gates_compare,noop_014,layout_014}.py`.

## Deviations and notes

1. **Token width is 20, not 15.** Your instruction to match the scene RaySensor semantics means 3 floats per ray (tag hit, hit nothing, fraction).
   Two rays per digit plus the thumb flag give 13 + 6 + 1 = 20.
2. **The folded rays read the current link pose.** The legacy sensors read the armature bones, which follow the links only in LateUpdate, so their
   pose is up to one rendered frame old. Every other ray property is ML-Agents' own computation.
3. **The eval lift budget default stays 200.** The harness key `liftBudgetSteps` sets `task/liftBudgetSteps` (300 for the headline evaluation).
4. **The 5-digit gate baseline under TGS** (`gates_baseline/GATES.md`) differs from the spike-6 record:
   - G1 hold-window penetration is 2.1–2.2 mm against spike 6's ≤ 1.9 mm.
   - G4 maximum penetration is 25.0 mm against 16.7 mm.
   - **G3 pinch now passes 3/3 at 1.5 kg and at perturbation scale 4.0.** Spike 5 (PGS) had 1/3 and 2/3, so under TGS pinch is no longer strictly
     weaker than the power grasp on these axes.
   - Part 3 compares the new generator against this baseline.
5. **Protected-file writes.** The deployed scene is untouched on disk. Existing code still rewrites `results/011_artic2/handspec_reference.txt` at the
   first rebuild of each Editor session. Its content is unchanged (git shows no diff).

## Next (Part 3, not started)

1. Build the headless player with `--topology slots` support and record the Assembly-CSharp hash.
2. Run the gates on the 3-, 5- and 7-digit hands (2-digit hands are out), comparing the 5-digit results with `gates_baseline`.
3. Run the 100k-step throughput probe and the 500k-step smoke.
