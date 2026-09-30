# Run 014 design recon: digit count as a morphology parameter (2026-09-30, read-only)

Scope: every place the code assumes five digits / 14 finger joints; a slot design for 2-7 digits; the observation and action plan
with the resulting ONNX inputs; the θ layout and digit-count distribution; what breaks and what it costs. Nothing under `Assets/`,
`tools/` or `Config/` was changed for this document. Line numbers refer to main at 1bd8610. Abbreviations: **AH** =
`Assets/Scripts/ArticulatedHand.cs`, **MM** = `Assets/Scripts/MorphologyManager.cs`, **AGA** = `Assets/Scripts/ArmGraspAgent.cs`,
**GATES** = `Assets/Scripts/Diagnostics/ArticulatedGates.cs`, **BoE** = `Assets/Scripts/BoEval/BoEvalHarness.cs`, **DS** /
**DST** = `Assets/Scenes/Dynamic_Scene.unity` / `Dynamic_Scene_Train.unity`.

Motivation (carried forward): BO over continuous θ with every group powered is flat on held-out shapes (`results/bo/partC/PARTC.md`,
BO-best − reference +0.017 [−0.014, +0.048]); the only large effect in any probe is actuation topology (thumb-base off: −0.75).
The bar is 013E on held-out shapes: upright 0.755, lying 0.567 (lift budget 200 at eval), cylinder 0.883
(`results/013/013E/PART1.md`).

## Decisions needed at this review (summary)

| # | question | brief says | recommendation | why |
|---|---|---|---|---|
| D1 | joint count | "max 20 finger joints, 22 tokens, 25 actions" | **19 finger joints, 21 tokens, 24 actions** | the brief's own slot list (thumb 2 + 5 fingers × 3 + second thumb 2) sums to 19; 20 would be thumb + 6 fingers, which the slot list does not have |
| D2 | finger placement | N fingers spread over a fixed 85 mm palm, spacing min(21, (85 − margins)/(N − 1)), lengths interpolated by position | **anatomical slots**: fingers 1-4 at today's MCP positions and table values, absent fingers dropped from the ulnar side; finger 5 added ulnar of the pinky with the palm widened by 21 mm | the brief's formula gives 15.75 mm spacing for 5 fingers against adjacent proximal radii sums of 16-18.5 mm (up to 2.75 mm interpenetration at rest with self-collision on); it also makes the 3-digit hand thumb + index-like + pinky-like, not "thumb + index + middle" as Part 3 names it. Both variants reproduce the 013 hand exactly at 5 digits |
| D3 | ray inputs | fold 2 tip rays into the distal token (22 × 15) or a separate 14-ray sensor | **fold into tokens, computed in code** | the 10 ray sensors are scene components on the imported armature's bones, which exist for 5 digits only; runtime-generated links are rebuilt every episode; sensors cannot be added after the agent initialises |
| D4 | vector obs | 24 + digit count + thumb count = 26 | **28** (per-slot length scales for 7 slots instead of 5, + active / 19, + the two counts) | 5 length-scale floats cannot describe 7 slots; the 26 variant is listed below |
| D5 | agent gate for small hands | reward / gate semantics unchanged | **needs your call**: with the gate as is (≥ 6 touching groups, ≥ 2 distinct non-thumb fingers, thumb), a 2-digit hand (5 groups, 1 finger) can never pass the gate, so it can never succeed | options: (a) P(2) = 0 in training (evaluate 2-digit zero-shot only); (b) scale the thresholds with the hand (min(6, groups − 1), min(2, fingers)), which is a task-semantics change |
| D6 | where the layout lives | (not stated) | **a code-side layout switch, default = legacy** | the sizes live in the scene files (VectorObservationSize, action count, buffer sizes, ray components), which are outside the allowed paths; the deployed `Dynamic_Scene` runs the 012 model InferenceOnly, and a mismatched model hangs the Editor (`MLAGENTS_UPGRADE.md:272-285`); a legacy default keeps every existing scene and model working; the run-014 player selects the slot layout from a command-line flag passed by `env_settings.env_args` |
| D7 | tags for new links | (not stated) | **no new tags**: slot-5 and second-thumb links stay untagged | group tags live in `ProjectSettings/TagManager.asset` (forbidden); contact detection does not need link tags (AH:553-560); only the watch overlay and BoEval look colliders up by tag |

## 1a. Inventory: everything that assumes five digits or 14 finger joints

### Hand generator (AH)

| item | where | current behaviour |
|---|---|---|
| finger table | AH:79-84 | 4-element `Finger[]` (index / middle / ring / pinky): phalanges 39.2/21.3/15.3, 43.6/25.8/16.2, 41.0/24.5/16.6, 31.9/17.1/14.9 mm (Santoso 2026, AH:34); radii 9/8/7.5, 9.5/8.5/7.5, 9/8/7.5, 8/7/6.5 mm |
| MCP arch | AH:79-84 (`mcp`, `restAbductionDeg`, `flexionPlaneTiltDeg`) | across +31.5 / +10.5 / −10.5 / −31.5 mm (21 mm spacing, AH:36), along 99 / 104 / 100 / 91 mm, rest abduction +8 / 0 / −6 / −14°, flexion-plane tilt 2 / 3 / 8 / 13° |
| finger loop | AH:296, AH:308 | `for (int f = 0; f < 4; f++)`, group `g = f*3 + seg` (not `spec.fingers.Length`) |
| thumb | AH:47-64, AH:321-352 | CMC at (25, 5, 32) mm (trapezium, AH:50-51); MC1 / PP / DP 46 / 30 / 21 mm (AH:53); rest posture palmar 60°, radial 35° (AH:55); MCP a FixedJoint at 20° (AH:57, :329, :339); opposition target (80, 62, 40) mm, i.e. "62" is the palmar coordinate, the measured opposition angle is 31.1° (AH:59, :351); groups 12 (CMC, revolute) and 13 (IP, revolute) literal (AH:335, :349); PP is an `ExtraLinks` entry mapped to group 12 (AH:341) |
| palm | AH:66-78, AH:282-287 | width 85, length 104, thickness 28 mm, mass 0.30 kg; box (thickness, length, width) |
| group tables | AH:107-112 | `GroupCount = 14`, `GroupTags` (14), `k_GroupParent`, `k_GroupFinger` |
| per-finger arrays | AH:153, AH:182, AH:248, AH:252 | `MeshFollowDeviation[5]`, `m_LengthScale[5]`, `m_InertiaScale[GroupCount + 2]`, rebuild loops `f < 5` |
| force limits | AH:142-143, AH:454 | `forceLimits[8]` by class; index `g < 12 ? g % 3 : (g == 12 ? 3 : 4)` |
| drives | AH:449-467 | Force drive k / b per group; masked group = Acceleration drive 4000 / 200 at neutral (AH:455); wrist on the palm's twist / swing (AH:280-281, :461-467) |
| self-collision | AH:354-366 | pair set Bicep, Forearm, Palm, 14 groups, ExtraLinks; ignored: parent-child (:360), palm-base via `IsBase` = groups 0, 3, 6, 9, 12 (:361, :401), forearm-palm (:362), bicep-all (:363). No thumb-index rule; thumb PP vs palm is not ignored |
| contact reporter | AH:416, AH:541-563 | `LinkContact` on every link, filter tag `Cylinder` (:553), group via `GroupOf` (:435), palm / forearm flags |
| anthropometrics | AH:351, AH:389-396 | opposition vs `Groups[3]`; hand length from groups 3-5, palm width from groups 0 and 9 |
| link tags | AH:315, :335, :349 | `link.tag = GroupTags[g]` (tags defined in `ProjectSettings/TagManager.asset:14-27`) |
| visual armature | AH:222-225, AH:535-538 | the imported 5-digit armature follows the links in LateUpdate (visual only) |

### Morphology (MM)

| item | where | current behaviour |
|---|---|---|
| constants | MM:28-36 | `FingerCount 5`, `FingerGroupCount 14`, `WristAxisCount 2`, `GroupCount 16`, `GroupNames` (16), `FingerNames` (5), `k_GroupFinger` (thumb = 4) |
| θ fields | MM:80-86 | `lengthScale[5]`, `stiffness / damping / inertia[16]`, `mask[14]`, `inertiaScale[16]` |
| ranges | MM:41-69 | length 0.8-1.2; k MCP 0.5-6, PIP 0.3-3, DIP 0.1-1, thumb base 1-15, thumb end 0.3-3, wrist 1-10 (log-uniform); ζ 0.3-1.0; inertia 0.5-2.0; mask p 0.8, min active 6, thumb required |
| class map | MM:179-184 | `StiffnessRange(g)`: g ≥ 14 wrist, 12 thumb base, 13 thumb end, else `g % 3` |
| sampler | MM:195-212 | 5 lengths, then per group (k, ζ, inertia), then the mask (up to 100 draws; thumb test `k_GroupFinger[g] == 4`, :208) |
| episode apply | MM:149-176 | `morph/randomize`, `morph/len_<5 names>` (:161), rebuild (:162), `morph/k_ / b_ / I_ <16 names>` (:167-169), `morph/mask_<14>` (:174, not re-validated). Known issue: `morph/I_*` is applied after the rebuild (:172 vs :162), so it never reaches the link masses in that episode |
| derived | MM:94-103, :287-288, :292-334 | `NominalInertia[16]` per finger subtree, `LinkLength[14]`, `HandSpan` from the index chain (groups 0-2), `FingerLengthRatio`, `ActiveCount` of 14 |

### Agent (AGA)

| item | where | current behaviour |
|---|---|---|
| layout constants | AGA:151-163 | `GroupCount 14`, `ArmAxisCount 5`, `ActionCount 19`, `k_GroupParent`, `k_GroupSegment`, `k_ThumbFinger = 4`, `TokenSize 13`, `MaxTokens 16` |
| joint limits | AGA:27-40, AGA:898-907 | 14 named limit fields and a `GetLimits` switch |
| neutral pose | AGA:123 (DS:1638-1652) | 14 entries |
| vector obs | AGA:466-490 (DS:1729 `VectorObservationSize: 24`) | [0-2] object in the palm frame, [3] hold progress, [4] contacts / 14, [5] hold pay, [6] phase, [7] mass, [8-17] sin / cos of 5 arm axes, [18-22] 5 length scales, [23] active / 14 |
| tokens | AGA:492-530, AGA:323 (DS:1777-1780 `ObservableSize 13`, `MaxNumObservables 16`) | one token per unmasked finger group (masked = `continue`, AGA:496: omitted, no flag), then 2 wrist tokens; features: parent (g + 1)/16, length / 0.06, finger / 4 (wrist 5/4), segment / 2, limits / 90, log10 k / 3, inertia, sin, cos, velocity, setpoint error, distance to object / 0.15 |
| rays | DS:425, :632, :1176, :1947, :2008, :2891, :3174, :3430, :3491, :3616 (DST ≈ 4 lines earlier) | 10 `RayPerceptionSensorComponent3D` on the armature bones (middle + end per digit), 1 ray, length 20, sphere radius 0.5, tag `Cylinder`; `RayRingend` has no tag (2 floats instead of 3, DS:3431); `RayPointermiddle` length 1, 0° (DS:3495-3497). ONNX: `obs_1-obs_10` = 29 floats |
| actions | AGA:540-575 (DS:1732 `m_NumContinuousActions: 19`) | a[0-13] finger Δ-setpoint (180°/s), a[14-16] shoulder flexion / abduction / elbow velocity, a[17-18] wrist Δ-setpoint; masked group: action ignored (AGA:570) but counted in the effort term (AGA:546) |
| contacts / gate | AGA:580-636, AGA:79-81 | contacts = touching groups among 14 (masked included, palm excluded); thumb = groups 12-13; `fingerMask` bits 0-3 (non-thumb); gate = contacts ≥ 6 ∧ distinct non-thumb fingers ≥ 2 ∧ thumb |
| force-limit map | AGA:596 | `g < 12 ? g % 3 : (g == 12 ? 3 : 4)` |
| shaping | AGA:335-340, AGA:604-614 | 14 segment colliders + grasp point (x scaled by `FingerLengthRatio`) |
| thumb diagnostics | AGA:740-771 | antipodality thumb vs fingers |
| stats | AGA:790-860 | Success/, Gate/, Lift/ by pose, Morph/ActiveGroups etc.; no per-digit keys |
| drives plumbing | AGA:346-364 | `ConfigureDrives` loops 14 groups + 2 wrist |

### Diagnostics and Editor tools

| item | where | current behaviour |
|---|---|---|
| gate controller | GATES:497-536 | arrays of 14; thumb = `g ≥ 12`, flexion sign; `CapDeg(g)` 60 / 70 / 30 by `g % 3`, thumb 45 / 45; pinch set `g ≤ 2 ∨ g ≥ 12` (:501) |
| fixed targets | GATES:88-89, :145-146 | `envelopeTargets` / `pinchTargets` / `k_Envelope` / `k_Pinch`, 14 entries |
| θ injection | GATES:80-85, :763-776 | `thetaLength[5]`, `thetaK / Zeta / Inertia[16]`, `thetaMask` string of 14, sent as `morph/*` env params |
| snapshot / CSV | GATES:201, :223, :251-257, :288, :296-297 | `snapLen[5]`, `lenIndex..lenThumb`, `kFingerMean` over 14, `activeGroups`, `mask` |
| audit / misc | GATES:141-142, :328, :371, :399-402, :419-473, :576-586, :621, :781-782 | groups 0-2, 3, 12-13 literal; `bases = {0,3,6,9,12}`; arm actions at `GroupCount + a` |
| model load | GATES:276-283 | imports and `SetModel` with **no shape guard** |
| forced transition | GATES:562-571, AGA:698 | the harness blocks the agent's gate during the close and forces the transition, so G1-G6 do not depend on the agent's gate |
| watch overlay | `MorphologyWatchOverlay.cs:15-20, :42, :58, :93-115` | 5 tip tags, `i < 5` loops, "/14" labels; tips found via `IsChildOf(agent)`, but the skeleton root is created unparented (AH:253-254), so the tip flags probably never light (inferred, not tested) |
| checkpoint theater | `CheckpointTheater.cs:91-109` | the only shape guard: vector size and buffer size read from the scene; rays and actions not checked |
| physics overlay, step rate | `PhysicsViewOverlay.cs:50`, `StepRate.cs:29` | generic (no counts) |
| BoEval (player) | BoE:28-32, :141, :150, :154, :172-214, :253, :264, :316 | 5 / 16 / 14 validation, oracle action indices 0-13, 12 model inputs; `nObs = 22` is stale (the vector is 24) |
| legacy scripts | `ArmGraspAgentCopy.cs`, `CSVArmGraspAgent.cs` | unreferenced by any scene (GUID checked); compile only |

### Python

| item | where | assumption |
|---|---|---|
| `tools/bo_eval/theta.py` | :15-22, :39, :51-53, :77 | `FINGER_NAMES` 5, `GROUP_NAMES` 16, thumb groups (12, 13) |
| `tools/bo/theta_space.py` | :27-30, :40-45, :52-56, :59-127 | 67 dims = 5 + 16 + 16 + 16 + 14; `harness_fields` emits 5 / 16 / 16 / 16 / 14 |
| `tools/bo/theta_reduced.py` | :39-47, :56, :76-79 | 5 classes, 67-dim offsets |
| `tools/bo_optim/space.py`, `report.py` | :18-24, :38, :81-84; :74-78, :121 | 53 + 14 layout, `"1" * 14` |
| BO drivers | `evaluate_theta.py:22,36-38`, `objective_013.py:35,66-67`, `confirm_013.py:29-30`, `feasibility.py:40-50`, `campaign*.py`, `analysis*.py` | cross-check `kFingerMean` / `activeGroups`; `mask[12]` = thumb base |
| eval tables | `run_passes.py:60-62,103-104`, `grid_tables.py:13,51,72,79-84`, `part3_tables.py:28,66-74`, `pulse_vs_grasp.py:22-23,64`, `gate_013E.py:5,78-83`, `run_zero_shot.py:55-59` | `mask[12]`, `k[15]` = wrist pronation, `thumb` / `palm` columns |
| `tools/bo_eval/evaluate.py`, `palm_by_theta.py` | :121-141, :211; :15, :51-52 | `lenIndex..lenThumb`, `activeGroups` bins up to 14 |
| `tools/eval/summarize_pass.py` | :45-51 | reads only success / lift / palm / contacts / hold / endReason: **layout-free** |

### Recorded 5-digit gate values (the Part 3 reference)

The newest scripted gate run is spike 6 post-TGS, 2026-09-22 (`MLAGENTS_UPGRADE.md:773-828`, CSVs in `results/011_artic2/`). There is
no 013-era rerun; the hand has not changed since (013 changed only the object).

| gate | recorded (spike 6, TGS) | file |
|---|---|---|
| G1 lift | 9/9 at 0.2 / 0.6 / 1.5 kg, 500-step holds, hold-window penetration ≤ 1.93 mm; 3/3 at 2.0 and 2.5 kg (≤ 1.54 mm) | `g1_s6.csv`, `g1_s6_ext.csv` |
| G2 perturbation | 0.6 kg, 30/30 at scales 0.25-4.0, window ≤ 3.08 mm, peak pulse 33.95 N at 4.0 | `g2_s6.csv`, `g2_s6_ext.csv` |
| G3 pinch | **not rerun under TGS**; spike 5 (PGS): 3/3 at 0.2 / 0.6 / 1.0 kg, 1/3 at 1.5 kg; scales 3/3 to 3.0, 2/3 at 4.0 | `g3_s5_*.csv` |
| G4 stability | 20/20 finite, joint speed 5.7-36.6 kdeg/s, episode max penetration ≤ 16.7 mm | `g4_s6.csv` |
| G5 throughput | 544 decisions/s (1.13 × 481) | `results/throughput_011s6/` |
| G6 push | held at 10 / 20 / 40 N (window 3.53 / 3.72 / 5.54 mm, episode 11.0 mm); pushed out at 80 N (window 7.28 mm) | `g6_s6_*N.csv` |
| penetration at preload | 2.1 mm (TGS row) | `MLAGENTS_UPGRADE.md:800-809` |

Consequence for Part 3: the 5-digit comparison must be run twice, on the unmodified code (a fresh baseline, since spike 6 predates
the object bank and G3 was never run under TGS) and on the new generator, and compared at matching configs; the spike-6 numbers are
the plausibility check.

## 1b. Slot design

### Slots (D1)

| slot | digit | actuated joints | groups (new index) | present when |
|---|---|---|---|---|
| 0 | primary thumb (radial) | CMC, IP (MCP fixed 20°) | 12, 13 | always |
| 1 | index | MCP, PIP, DIP | 0, 1, 2 | always |
| 2 | middle | 3 | 3, 4, 5 | ≥ 3 digits |
| 3 | ring | 3 | 6, 7, 8 | ≥ 4 digits |
| 4 | pinky | 3 | 9, 10, 11 | ≥ 5 digits |
| 5 | extra ulnar finger | 3 | 14, 15, 16 | ≥ 6 digits |
| 6 | second thumb (ulnar, mirrored) | CMC, IP | 17, 18 | 7 digits |

- Maximum: 2 + 5 × 3 + 2 = **19 finger groups** + 2 wrist = **21 tokens**; actions 19 + 2 + 3 = **24**.
- **Group numbering keeps the legacy 0-13 as a prefix**, so every 013 array, θ and the legacy layout are a prefix of the new
  arrays. Wrist groups move to 19-20 in the slot layout (they stay 14-15 in the legacy layout); one mapping function owns this.
- Digit count → configuration (existence is nested, dropped from the ulnar side): 2 = thumb + index; 3 = + middle; 4 = + ring;
  5 = + pinky (**the 013 hand**); 6 = + extra finger; 7 = + second thumb. The existence vector is then a function of the count.
  An alternative 6-digit hand (thumb + 4 fingers + second thumb) is possible with the same slots; it is left out of the proposal
  (open question 3).

### Placement (D2)

**Recommended: anatomical slots.** Slots 1-4 use today's table and MCP positions verbatim, so any hand with ≤ 5 digits is the 013
hand minus its ulnar fingers, and the 5-digit hand is the 013 hand exactly (lengths, radii, masses, MCP positions, limits). Slot 5 is
extrapolated from the radial-to-ulnar trend of the table: MCP (82, 0, −52.5) mm (along 99 / 104 / 100 / 91 → 82), rest abduction
−22° (+8 / 0 / −6 / −14), tilt 18° (2 / 3 / 8 / 13), phalanges and radii copied from the pinky (31.9 / 17.1 / 14.9 mm, 8 / 7 / 6.5 mm).
The palm widens by 21 mm on the ulnar side when slot 5 exists (106 mm; the box centre shifts −10.5 mm across; mass scales with the
volume, 0.30 → 0.374 kg). The palm is unchanged for ≤ 5 digits.

**The brief's variant (spread + interpolate), for the record.** N fingers at spacing min(21, (85 − 22)/(N − 1)) centred on the palm;
lengths, radii, MCP along-offset, abduction and tilt interpolated at t = i/(N − 1) across the index-to-pinky table. N = 4 reproduces
the table (spacing 21, t = 0, 1/3, 2/3, 1). N = 5 gives 15.75 mm spacing against adjacent proximal radii sums of 16-18.5 mm (9 + 9.5, 9.5 + 9, 9 + 8, 8 + 8): adjacent proximal
phalanges would interpenetrate by 0.25-2.75 mm at rest with self-collision on (AH:354-366), which the gate record ties to explosive
depenetration (G4 max 16.7 mm is already the worst case). Fixing it needs either finger radii × 0.75 for N = 5 (a different finger)
or the palm-scaled alternative (spacing 21 at every N, palm width = 21(N − 1) + 22 mm), which for N ≤ 4 differs from the recommended
design only by centring the fingers on the palm instead of keeping the index at its anatomical place. N = 2 and 3 would give the
3-digit hand the index and pinky length profiles (t = 0, 1), not index + middle.

**Second thumb (slot 6).** Mirror of slot 0 about the palm's across-centre: CMC (25, 5, −32) mm (−53 mm with slot 5 present, i.e.
mirrored about the widened palm's centre −10.5 mm), radial abduction mirrored (−35°), opposition target (80, 62, −40) mm (again shifted
with the widened palm), same MC1 / PP / DP lengths and radii, MCP fixed at 20°, same joint limits with the flexion sign unchanged (the
CMC axis is `Cross(dir, target − cmc)`, AH:326, so mirroring dir and target mirrors the axis). It opposes the index-middle side
across the palm like the primary thumb, from the ulnar side.

**Self-collision.** The existing rules extend per slot: parent-child and palm-base (`IsBase` for groups 14 and 17), forearm-palm,
bicep-all. Today there is **no special thumb-index rule** (the brief's "handled like thumb-index today" therefore means: fully
colliding, as now). The second thumb's PP-vs-palm pair stays colliding, like the primary thumb's (a pre-existing asymmetry,
AH:360-361).

**Joint limits, neutral pose, force limits, k / ζ ranges** by class: slot-5 groups take the pinky's limits (AGA:36-38) and the finger
classes' k ranges; slot-6 groups take the thumb's limits (AGA:39-40), k ranges (thumb base 1-15, thumb end 0.3-3) and force-limit
classes. No new numbers are invented except the slot-5 placement above.

## 1c. Observations, actions, θ, distribution

### Tokens and rays (D3)

**Option A (recommended): tokens 21 × 15, rays computed in code.** Features 0-12 as today with the codes re-normalised for 7 slots
(parent (g + 1)/21, slot / 6 for fingers with wrist 7/6 replacing finger / 4 and 5/4, segment / 2); features 13-14 = two tip readings
on the distal token of each digit (0 on the others): `Physics.SphereCast` from the distal link's tip along the pad normal and along the
link axis, 5 mm radius, 0.15 m range, value = hit distance / 0.15 (1 = miss), tag `Cylinder` only. Absent slots and masked groups are
omitted from the buffer, as masked groups are today (AGA:496). ONNX inputs: **2** (`obs_0` [21, 15] tokens, `obs_1` [28] vector);
output `continuous_actions` [24].

- The present rays cannot be kept as they are: they are scene components on the imported 5-digit armature (no bones exist for
  slots 5-6), and a 0.5 m sphere over 20 m hits the object almost always (`docs/design/013_object_randomization.md` risk R3), so their
  29 floats are close to constant today. The replacement is a design change (short, narrow casts that mean "something in front of the
  pad"); it is stated as such.

**Option B: tokens 21 × 13 plus a separate 14-reading ray input.** ML-Agents collects sensor components once, when the agent
initialises (`Agent.InitializeSensors`, ml-agents `Runtime/Agent.cs:984-1015`, `GetComponentsInChildren<SensorComponent>`), and the
links are destroyed and rebuilt every episode (MM:162, AH:251-253), so per-link `RayPerceptionSensorComponent3D`s are not possible;
it would need a custom `ISensor` of 14 × 3 floats computed in code (absent digits report "miss, max range"). ONNX inputs: **3**
(`obs_0` [21, 13], `obs_1` custom [42], `obs_2` [28]). It keeps the rays outside the attention block, where the network cannot tie a
reading to its digit except by position.

Option A is recommended because the reading then sits in the token of the digit it belongs to (the attention block sees "which
finger sees the object"), absent digits cost nothing, and there is one fewer input to keep in sync.

### Vector observation (D4)

| index | content |
|---|---|
| 0-17 | unchanged (object in the palm frame, hold, contacts / 19, hold pay, phase, mass, sin / cos of 5 arm axes) |
| 18-24 | length scale per slot 0-6, 0 when absent |
| 25 | active groups / 19 |
| 26 | digit count / 7 |
| 27 | thumb count / 2 |

Total **28**. The brief's 26 variant keeps 18-22 as five length scales (slots 0-4) and drops the per-slot lengths of slots 5-6 from
the vector (they remain in the tokens' length feature); it is 2 floats smaller and loses nothing the tokens do not carry. Either way
`normalize: true` stays in the trainer config.

### Actions

24 = a[0-18] finger groups in the new group order (Δ-setpoint, 180°/s, as today), a[19-21] shoulder flexion / abduction / elbow
velocity, a[22-23] wrist Δ-setpoint. Absent and masked groups: action ignored. **Effort term**: today masked actions still count
(AGA:546); for absent slots the same rule would charge the policy for outputs that do nothing, so absent actions are excluded from
Σa² while masked ones keep today's rule. This is the only place the reward sees the layout, and it keeps the reward semantics
("effort of the joints you drive"); flagged in case you read it as a reward change.

### Layout switch (D6)

A `TopologyLayout` enum on the agent: **Legacy** (default; today's sizes: 24 vector, 16 × 13 tokens, 10 scene rays, 19 actions; the
generator builds the 5-digit hand) and **Slots** (28, 21 × 15, rays folded, 24 actions, digit count randomised). Slots is selected by
a command-line argument (`--topology slots`, passed by the run-014 YAML's `env_settings.env_args`) in the player and by a
`Temp/gates.json` key in the Editor harness. A `[DefaultExecutionOrder(-1000)]` bootstrap sets `BrainParameters`
(`VectorObservationSize`, `ActionSpec`), the buffer sensor's `ObservableSize` / `MaxNumObservables` (public setters,
`BufferSensorComponent.cs:27-46`) and removes the ten ray components before the agent's `OnEnable` initialises its sensors. The
scene files are not edited. Risk: script-order behaviour must be verified in 2c (print the sensor shapes; build a 1k-step smoke and
read the ONNX inputs).

### θ layout (slot layout)

| block | dims | notes |
|---|---|---|
| existence | 7 (slot 0 and 1 fixed at 1) | determined by the digit count under the nested rule |
| length scale per slot | 7 | 0.8-1.2 (MM:41); thumbs scale PP and DP only, as today (AH:321, :340-348) |
| k per group | 21 (19 finger + 2 wrist) | class ranges MM:43-53 |
| ζ per group | 21 | MM:57-59 |
| inertia scale per group | 21 | MM:63 |
| mask per finger group | 19 | p 0.8 |

Continuous 70 + discrete 26; the 013 θ (67) embeds as slots 0-4 with groups 0-13 plus the wrist. Mask rule generalised: min active
= min(6, present groups), at least one group of some thumb slot active. For a 2-digit hand (5 groups) that means all active.

### Digit-count distribution (proposal, as the brief states it)

| digits | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|
| P | 0.05 | 0.10 | 0.15 | 0.40 | 0.15 | 0.15 |
| hand | T + I | T + I + M | T + I + M + R | 013 hand | + extra finger | + second thumb |

Mean digits 4.95, so average link count (and physics cost) is within 1 % of today's (−0.15 links per episode). Drawn per episode in
`MorphologyManager.Sample` from the episode stream before the rest of θ. Under D5 option (a) P(2) moves to 0 and its 0.05 goes to 5.

## 1d. What breaks and what it costs

| consumer | effect | cost |
|---|---|---|
| deployed `Dynamic_Scene` (012 model, InferenceOnly) | unaffected under the Legacy default; would **hang the Editor** with a mismatched model if the layout changed in place | the layout switch (D6) |
| `Dynamic_Scene_Train` / the Run013 player | Legacy by default; Run014 player passes `--topology slots` | none |
| every existing ONNX (012, 013C/D/E, Watch/*) | runs only in Legacy | none; Slots models do not run in Legacy (guard below) |
| CheckpointTheater guard | reads sizes from the components at runtime, so it follows the switch; add the action count and rays | small |
| ArticulatedGates eval | no guard today: add one (ONNX inputs vs the active layout) before `SetModel` | small |
| gate controller, fixed targets, audit, θ injection, CSV | generalise to slots: loops over present groups, class-based caps and pinch set (pinch = index + primary thumb), θ arrays sized by the layout, CSV + `digitCount, thumbCount, slots` (7-char existence) and `lenS0..lenS6` (Slots) | medium (2d) |
| watch overlay | 5 tips and "/14" labels; tip lookup already broken (unparented skeleton) | small: iterate present slots, fix the lookup |
| BoEval harness / `tools/bo_eval` / `tools/bo_optim` | Legacy only (5 / 16 / 14 validation, 22-obs stale) | none now; marked Legacy-only |
| `tools/bo/theta_space.py` | new Slots space alongside the 67-dim space (the 013 tools keep working) | medium (2e) |
| `summarize_pass.py` | layout-free; add the new columns to the summaries | small |
| eval tables (`grid_tables`, `part3_tables`, `gate_013E`, `run_passes` hand variants) | Legacy θ (`mask[12]`, `k[15]`); new 014 tables needed | medium (Part 4) |
| StatsRecorder | add `Success/Digits2..7` (+ gate / lift per count) | small |
| tags | no new tags; slots 5-6 untagged | none |

**Throughput (estimate, to be measured in 3b).** Physics: average link count unchanged (above); 7-digit episodes add 6 links (+32 %
bodies) and 2-digit episodes remove 9. Policy: the trainer pads to 21 tokens (vs 16): the entity embedding scales with tokens × features
(21 × 15 / 16 × 13 = 1.51 ×), the self-attention with tokens² (1.72 ×); 10 ray encoders (29 floats) disappear. Agent side: ≤ 14 sphere
casts per decision replace 10 per decision. Run 013 ran 390-410 steps/s with the trainer's update share not measured separately;
if the network doubled in cost and updates were a third of wall time, throughput would drop about a third to ~270-300 steps/s. My
expectation is 330-380 steps/s; the 300 stop line is within reach of the pessimistic case, which is why 3b measures it before any
long run.

## Risks and the cheapest test for each

| risk | test |
|---|---|
| R1 the bootstrap runs after the agent initialises (sensor shapes stay Legacy) | print the sensor shapes at `Initialize`; 1k-step build smoke, read the ONNX inputs |
| R2 second thumb interpenetrates the pinky / extra finger at rest | G4 stability and a rest-pose penetration audit on the 7-digit hand (Part 3) |
| R3 the widened palm changes the 5-digit hand | the 2f table compares only 5-digit hands (palm unchanged there); the 6/7-digit palm is new by design |
| R4 2-digit hands can never pass the agent gate (D5) | decide before 2b; under (a) they are evaluated zero-shot only |
| R5 digit-count variety slows learning of the 5-digit grasp | smoke criterion 3c (5-digit Success/Canonical > 0 by 500k); the 40 % share keeps 5-digit hands the plurality |
| R6 `morph/I_*` ordering bug (MM:162 vs :172) | out of scope; noted (the harness's fixed θ therefore does not reach link masses; the 2f table uses the nominal hand) |

## Open questions

1. D1-D7 above.
2. Should the token carry an explicit thumb-class flag (a 16th feature)? The slot code and the thumb's limits / lengths identify it
   indirectly; an explicit flag would make "any thumb" learnable in one step for the second thumb.
3. Should 6-digit hands include the thumb + 4 fingers + second thumb variant (split P(6))? It isolates the second thumb's effect from
   the extra finger's.
4. Should the eval lift budget follow training (300)? The harness sets 200 (GATES:261); run 014 can keep 200 for comparability with
   013E or measure both.

## Decisions at checkpoint 1 (2026-09-30) and what Part 2 implemented

| # | decision | implemented as |
|---|---|---|
| D1 | 19 finger joints (thumb 2 + fingers 1-5 x 3 + second thumb 2), 21 tokens, 24 actions | `HandLayout` (ArticulatedHand.cs): digit ids keep the legacy order as a prefix (0 index .. 3 pinky, 4 thumb, 5 extra finger, 6 second thumb); groups 0-13 legacy, 14-16 extra finger, 17-18 second thumb; morphology arrays keep the legacy 16 (wrist at 14-15) and append 16-20 |
| D2 | anatomical slots; the palm widens 21 mm only with the extra finger; smaller hands drop from the ulnar side | `HandSpec.extraFinger`, `extraFingerPalmWidening`; the second thumb mirrors the primary about the palm's across centre |
| D3 | fingertip rays folded into the tokens, computed in code, with the scene RaySensor semantics | each legacy ray sensor's cast settings (ML-Agents' own `RayPerceptionInput`) and its pose relative to its finger bone are captured, then replayed on the matching link every rebuild through `RayPerceptionSensor.Perceive` (tag "Cylinder", range 20 x bone scale, sphere radius 0.5, the 3-float encoding). The extra finger borrows the pinky's sensors, the second thumb the thumb's. Known difference: the legacy sensors read the bones' pose from the last LateUpdate (stale by up to one rendered frame at timeScale 20); the folded rays read the current link pose |
| D4 | vector 28 with per-slot length scales, absent = 0 | `CollectObservationsSlots` |
| D5 | 2-digit hands out of run 014; digit range 3-7; the gate is unchanged | `MorphologyManager.digitDistribution` = P(3, 4, 5, 6a, 6b, 7) = (0.10, 0.15, 0.45, 0.075, 0.075, 0.15): the brief's P(2) moved to 5, the two 6-digit compositions at equal share |
| D6 | code-side layout switch, default = the 013 layout; `--topology slots` via env_args; fail loud; no-op proof | `HandLayout.Resolve` / `ArmGraspAgent.OnEnable` (configures `BrainParameters`, the token buffer and removes the ray components before ML-Agents builds the policy and sensors); `AssertLayout` (Initialize) and `VerifySensors` (first episode, the sensors ML-Agents actually created); `LayoutGuard.ModelMatches` for any model; a legacy scene model is detached in the slot layout |
| D7 | no new tags | links of the extra finger and the second thumb are untagged |
| D8 | effort over present joints only, masked joints as today, unnormalised | `OnActionReceived` (slot layout). Consequence: a hand with more digits pays more effort for the same per-joint activity (24 / 13 = 1.85x between the 7-digit hand, 19 finger + 5 arm terms, and the 3-digit hand, 8 + 5, at equal per-joint action magnitude) |
| open | thumb flag in the tokens | feature 19 (thumb = any thumb slot); final token width **20** = 13 legacy features + 2 rays x 3 floats + the thumb flag |
| open | lift budget in the harness | `ArticulatedGates` config `liftBudgetSteps` sets `task/liftBudgetSteps` (eval at 300 headline and 200 for 013D comparability) |

ONNX input set of the slot layout: **2 inputs**, `obs_0` [21, 20] (JointTokens), `obs_1` [28] (vector); output `continuous_actions` [24].

## Known limitation: digit identity and digit count are confounded

Digits drop from the ulnar side, so the digit count also fixes which digits a hand has: every 3-digit hand is thumb + index + middle, every 4-digit hand
adds the ring finger, and the only 6-digit hands are the two fixed compositions. A difference between digit counts is therefore a difference between these
specific hands, not an effect of "one more finger" in general; e.g. 3 vs 4 digits cannot separate "a third finger" from "the ring finger". The two 6-digit
compositions (thumb + 5 fingers vs thumb + 4 fingers + second thumb) are the only within-count contrast. Separating count from identity would need
position-randomized placement (the brief's spread-and-interpolate variant) or compositions that remove radial digits, both excluded by decision D2.
