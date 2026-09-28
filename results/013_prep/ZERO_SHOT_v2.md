# Zero-shot v2: run 012 on the superquadric bank after the deterministic object reset (2026-09-27)

Build 9539a15 (main). This file replaces the measurements of `ZERO_SHOT.md` that were taken with the carried-tilt spawn (see the
supersession paragraph at the end). Instrument: Editor harness `ArticulatedGates` mode eval, deterministic head, one job worker,
per-episode reseed `DerivedSeed(seed, passIndex)`, throwaway pass after every launch, K = 50, perturbation 1.0, mu 1.0, mass
log-uniform 0.2-1.5 kg, run-012 model; shapes assigned by stratified `fixedIndex`; pose mode by the environment parameter
`object/pose`. Pass CSVs and `passes_v2.json` in `passes_v2/`; the driver is the session script recorded in `passes_v2.json`
(`cfg` per pass).

## Part 1. Deterministic object reset with pose modes (commit 9539a15)

- **Reset path (1a).** One path, shared by training and the eval harness: `ArmGraspAgent.OnEpisodeBegin` (`ArmGraspAgent.cs:367-420`)
  → `ObjectBank.ApplyForEpisode` (`:396`) → `SpawnCylinder` (`:422-447`, the only runtime writer of the object pose in this path;
  `SetPositionAndRotation` at `:446`). Both scenes reference the same agent script (GUID `1c2b6104…` once in `Dynamic_Scene.unity`
  and once in `Dynamic_Scene_Train.unity`); the harness only hooks `BeforeEpisodeBegin`. The other pose writers
  (`BoEvalHarness.cs:298/386/405`, `ArticulatedGates.cs:349/354/383`) belong to the kinematic-era player and the scripted gate modes,
  not to training or eval.
- **Reset semantics (1b).** Every episode: `ApplyForEpisode` selects the shape, draws one pose number (both pose modes, so canonical
  and randomStable passes share every later draw), sets `transform.rotation` to the pose (canonical = the scene object's rotation at
  load, identity, length axis up; randomStable = a settled rest pose from the cache, index = draw mod count), zeroes linear and
  angular velocity, puts the kinematic body to sleep, syncs transforms, recomputes the rest half-height from the collider bounds at
  that orientation and hands `SpawnBaseRotation` to the agent; `SpawnCylinder` then draws the spawn position and the yaw about world Y
  from the episode's stream and applies `Euler(0, yaw, 0) × base`, zeroing velocities again. The kinematic-until-gate flag is restored
  at `:381` as before; `ReleaseObject` (`:687-693`) wakes the body with zero velocity at the gate. Nothing of the previous episode's
  end pose survives, and a pass starts from the same state whether or not another pass ran before it (Part 2).
- **Pose modes exposed.** `object/pose` (0 canonical, 1 randomStable) is an environment parameter like `object/mode`, readable by the
  trainer side-channel and set by the harness config `objectPose`. Default canonical.
- **Stable-pose set (1c).** `Assets/Objects/Bank/stable_poses.json` (committed): for each of the 81 shapes and each of the six body
  axes placed up, a hidden physics scene (project physics settings, one job worker, friction 1.0) drops the shape 1 mm above a flat
  ground with a 3° deterministic tilt about a fixed horizontal axis and simulates 600 steps of 10 ms with angular damping 2 and
  linear damping 0.5 on the cook body (PhysX has no rolling resistance, so a nudged round shape would roll for ever; the damping
  changes when it stops, not where). A candidate is settled when over the last 0.5 s window its orientation changed by less than 1°
  and its centre by less than 1 mm; converged poses are merged by body-up axis within 10°; the stored rotation is the yaw-free
  minimal rotation taking that axis to world up. Result: 338 poses, 317 settled; every shape has 2-6 settled poses (8 shapes with 2,
  25 with 3, 25 with 4, 12 with 5, 11 with 6); no shape has a single pose; 21 candidates on 16 shapes (3, 9, 16, 18 ×3, 22, 23, 27, 32,
  38, 44 ×3, 45, 55, 58, 61 ×2, 67, 75) were still turning by 1-17° in the last window after 6 s and are kept in the cache flagged
  unsettled, never drawn. Only 9 shapes (the anchor and 6, 7, 19, 23, 24, 45, 52, 73) have an upright rest pose: a superellipsoid with
  rounded ends (e1 above about 0.5) cannot stand on its end (tip curvature radius a1²/a3 ≈ 5-15 mm against a centre-of-mass height
  of 40-140 mm), so the canonical pose is a physically unstable orientation for 72 of 81 shapes; it is harmless before the gate
  (the object is kinematic) and is kept as the measurement pose by decision. The anchor cylinder has 6 rest poses (2 upright, 4
  lying, the lying four being the same pose up to a roll about the axis), so randomStable draws a lying cylinder two times in three.
  Determinism: two independent cook runs in separate Play sessions produced byte-identical files (three times, at each revision
  of the cook); the cache is the second-session output.
- **Pedestal (1d).** Slab top footprint 0.771 × 0.773 m centred at (−0.198, 5.494); spawn disk radius 0.09 m around (−0.329, 5.474);
  worst-case horizontal reach of any shape at any yaw = |centre offset| + 0.09 + max(a1, a2, a3) = 0.361 m along x against a
  half-extent of 0.386 m (margin 25 mm), 0.251 m along z (margin 136 mm). No shape overhangs in any pose; no resize needed.
- **Rules relative to the current pose (1e).** Reach target: `GraspPoint` is a palm-frame offset and `GraspPointDistance` the distance
  to the object centre (`ArmGraspAgent.cs:146-148`), no object axis. Contact gate: counts touching finger groups, distinct fingers and
  the thumb from collision callbacks (`:578-593`, `:629-630`), no geometry. Lift threshold: `bounds.min.y ≥ platformTop + 0.03` on
  the world AABB (`:699`), pose-independent. Drop rule (`:700-706`): centre farther than 0.11 m from the grasp point; tilt of
  `transform.up` from `m_ObjectUp0`, which is captured at release (`:694`), so it measures the change of the length axis since the
  gate and is meaningful for a lying pose; AABB bottom below the platform. Shaping uses `Collider.ClosestPoint` (`:870-874`). None
  assumes the upright cylinder; no edit beyond 1b.
- **Diagnostics (1f).** Eval CSV columns `poseMode`, `poseIndex`, `spawnYaw`, `spawnTiltDeg` (tilt of the length axis from world up
  measured two physics steps after the spawn), `spawnCarryover` (rotation after the spawn differs from yaw × pose by more than 0.5°),
  plus `preResetTiltDeg` (the end tilt of the previous episode that the old rule would have carried).
- **Layout proof.** Recompile clean; the ONNX, the scene BehaviorParameters and the DecisionRequester are untouched; the Editor demo
  (deployed 012, anchorOnly + canonical, 20 episodes) ran with 0 errors, `spawnCarryover` false, `spawnTiltDeg` 0.0 while
  `preResetTiltDeg` read 15.9°; the harness throwaway pass 10 / 10.

## Part 2. Per-episode reproducibility retest (anchor, 012, mu 1.0, deterministic head, one worker, seeds 6001-6100)

- **(a) first full pass after launch vs later pass, same session**: repro_first_181755.csv vs repro_later_181841.csv: rows 100 / 100; **byte-identical on every column**
- **(b) first pass of session 1 vs first pass of session 2**: repro_first_181755.csv vs repro_first_182357.csv: rows 100 / 100; **byte-identical on every column**
- **(c) seeds 6091-6100 run alone (the throwaway pass) vs their rows inside the first full pass**: 10 seeds matched; 10 seeds differ, first seed 6091 in columns ['steps', 'transitionStep', 'stepsToLift', 'contacts', 'distinctFingers', 'maxPenMm']; phaseAtBegin alone differs
- **(d) randomStable pass, session 1 vs session 2**: repro_rs_181926.csv vs repro_rs_182443.csv: rows 100 / 100; **byte-identical on every column**
- **(extra) throwaway pass, session 1 vs session 2**: throwaway_181735.csv vs throwaway_182338.csv: rows 10 / 10; **byte-identical on every column**
- first pass aggregates: success 1.000 [0.963, 1.000], ends {'success': 100}; carry-over flags set: 0; max spawnTiltDeg 0.00
- randomStable pass aggregates: success 0.303 [0.221, 0.400], ends {'drop': 5, 'maxStep': 64, 'success': 30}; pose indices used [0, 1, 2, 3, 4, 5]; spawnTiltDeg classes {90: 70, 0: 16, 180: 14}; carry-over flags 0

**Verdict: NOT PER-EPISODE-REPRODUCIBLE** (pass-level identities (a), (b), (d) and the throwaway hold; (c) fails) (4 of 5 identities hold).

### Reading of Part 2

The object pose was the cross-pass state channel. With it removed, a pass is a function of (build, seed block, passIndex, pose mode)
only: the first pass after an Editor launch equals a later pass in the same session, two relaunched sessions give identical files,
the randomStable pass reproduces across sessions, and even the ten-episode throwaway reproduces across sessions. The one-off
"first-pass realisation" of `ANCHOR_REGRESSION.md` is therefore explained: the first pass of a session used to start from the scene's
pristine upright object, later passes from whatever orientation the previous pass had left. Per-episode independence does not hold:
the same seed run alone and inside a pass differ in every case, for six of the ten seeds together with a different decision phase
(`phaseAtBegin`, the Academy step count modulo the decision period at the reset, which depends on the lengths of the preceding
episodes), and for the four seeds with an equal phase as well, so a deterministic physics-scene state still links an episode to its
predecessors (the PhysX scene persists across the per-episode rig rebuild). The reproducible unit stays the whole pass. Under the
brief's rule the verdict word is NOT PER-EPISODE-REPRODUCIBLE, with (a), (b), (d) and the throwaway identity holding and (c) failing
from seed 6091 onward (`steps`, `transitionStep`, `stepsToLift`, `contacts`, `maxPenMm`). Note added to
`results/012/checks/determinism/RNG_INVENTORY.md`: the throwaway pass and the first-episode exclusion are no longer needed for
pass-level reproducibility; both are kept for now.

## Part 3. Anchor regression (canonical, anchorOnly, 012, seeds 7001-7300, mu 1.0)

- success 1.000 [0.987, 1.000] (reference eval_n300_v2 mu 1.0: 1.000 [0.988, 1.000]; its same-session repeats 0.993): inside the reference interval; lifted 1.000, palm 0.967, contacts 5.61, hold 500.0, ends {'success': 300}
- failures: none
- spawnTiltDeg now: max 0.00°, all episodes < 0.1°: True; carry-over flags 0
- what the old rule would have carried (preResetTiltDeg, the end tilt of the previous episode): >= 10° in 170 of 300 episodes (57 %), median 11.0°, 90th percentile 17.2°, max 35.4° (old instrument, measured 2026-09-27 on the same seeds: 58 % >= 10°, median 11.2°, p90 21.9°, max 37.7°)

## Part 4a. Zero-shot, pose mode **canonical** (012, random theta, K = 50, perturb 1.0, mu 1.0, mass as 012, seeds 9001-9300)

### Train bank (1-64), canonical: 900 episodes

- success 0.636 [0.604, 0.666]; lifted 0.683 [0.652, 0.713] (n=900); palm at end 0.828 [0.802, 0.851] (n=900); thumb 0.840 [0.815, 0.862] (n=900); reach-failure rate 0.236; contacts at end 4.08; mean hold steps 325
- ends {'success': 572, 'reach failure': 212, 'drop': 116}; drop phases {'between pulses': 15, 'lift phase': 75, 'before first pulse': 19, 'during pulse 1': 1, 'after last pulse': 6}; spawnTiltDeg classes {0: 900}; carry-over flags 0
- per-shape success: 64 shapes, min 0.00, median 0.79, max 1.00, SD 0.357; shapes <= 0.70: 26; shapes at 1.00: 11; episodes per shape 14-15

By e1 (axial profile):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e1 0.3-0.7 | 28 | 394 | 0.622 [0.573, 0.668] (n=394) | 0.668 | 0.812 | 4.16 | 0.234 | 7 / 1 / 7 / 3 / 39 |
| e1 0.7-1.1 | 16 | 224 | 0.647 [0.583, 0.707] (n=224) | 0.688 | 0.857 | 4.10 | 0.263 | 5 / 0 / 3 / 1 / 11 |
| e1 1.1-1.5 | 20 | 282 | 0.645 [0.588, 0.699] (n=282) | 0.702 | 0.826 | 3.95 | 0.216 | 7 / 0 / 5 / 2 / 25 |

By e2 (cross-section shape):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e2 0.3-0.7 | 21 | 295 | 0.644 [0.588, 0.697] (n=295) | 0.698 | 0.834 | 4.18 | 0.197 | 6 / 0 / 8 / 2 / 31 |
| e2 0.7-1.1 | 21 | 296 | 0.514 [0.457, 0.570] (n=296) | 0.547 | 0.814 | 3.54 | 0.382 | 5 / 1 / 3 / 1 / 21 |
| e2 1.1-1.5 | 22 | 309 | 0.744 [0.693, 0.790] (n=309) | 0.799 | 0.835 | 4.49 | 0.133 | 8 / 0 / 4 / 3 / 23 |

By cross-section 2·max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| cross-section 50-65 mm | 15 | 211 | 0.607 [0.539, 0.670] (n=211) | 0.659 | 0.882 | 4.04 | 0.242 | 5 / 0 / 2 / 3 / 22 |
| cross-section 65-80 mm | 40 | 562 | 0.633 [0.593, 0.672] (n=562) | 0.680 | 0.790 | 4.10 | 0.233 | 12 / 0 / 10 / 3 / 50 |
| cross-section < 50 mm | 9 | 127 | 0.693 [0.608, 0.766] (n=127) | 0.740 | 0.906 | 4.03 | 0.236 | 2 / 1 / 3 / 0 / 3 |

By length 2·a3:

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| length 130-210 mm | 28 | 395 | 0.825 [0.785, 0.860] (n=395) | 0.853 | 0.878 | 4.92 | 0.104 | 4 / 0 / 5 / 2 / 17 |
| length 210-280 mm | 15 | 210 | 0.933 [0.891, 0.960] (n=210) | 0.971 | 0.733 | 5.07 | 0.019 | 3 / 0 / 3 / 1 / 3 |
| length < 130 mm | 21 | 295 | 0.169 [0.131, 0.216] (n=295) | 0.251 | 0.827 | 2.25 | 0.566 | 12 / 1 / 7 / 3 / 55 |

By aspect a3 / max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| aspect 1.5-3 | 34 | 480 | 0.581 [0.537, 0.625] (n=480) | 0.644 | 0.829 | 4.03 | 0.267 | 13 / 1 / 10 / 5 / 44 |
| aspect < 1.5 | 8 | 112 | 0.089 [0.049, 0.157] (n=112) | 0.107 | 0.902 | 1.89 | 0.679 | 2 / 0 / 0 / 0 / 24 |
| aspect >= 3 | 22 | 308 | 0.919 [0.883, 0.944] (n=308) | 0.955 | 0.799 | 4.94 | 0.026 | 4 / 0 / 5 / 1 / 7 |

By volume tercile (cuts 224, 314 cm³):

| tercile | shapes | episodes | success | lifted | palm |
|---|---|---|---|---|---|
| low | 21 | 296 | 0.432 [0.377, 0.489] (n=296) | 0.497 | 0.848 |
| mid | 21 | 295 | 0.614 [0.557, 0.667] (n=295) | 0.658 | 0.841 |
| high | 22 | 309 | 0.851 [0.807, 0.887] (n=309) | 0.887 | 0.796 |

Per-shape success, ranked (all shapes):

| shape | success | n | lifted | palm | cross mm | length mm | e1 | e2 | volume cm³ | ends |
|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 0.00 | 15 | 0.13 | 0.80 | 43 | 81 | 0.61 | 0.93 | 79 | {'reach': 13, 'drop': 2} |
| 7 | 0.00 | 14 | 0.00 | 0.93 | 69 | 85 | 0.51 | 0.58 | 255 | {'reach': 11, 'drop': 3} |
| 14 | 0.00 | 14 | 0.00 | 0.93 | 78 | 90 | 0.77 | 0.74 | 176 | {'reach': 11, 'drop': 3} |
| 18 | 0.00 | 14 | 0.07 | 0.86 | 66 | 88 | 1.05 | 1.06 | 162 | {'reach': 13, 'drop': 1} |
| 27 | 0.00 | 14 | 0.00 | 0.93 | 48 | 83 | 1.12 | 0.77 | 85 | {'reach': 14} |
| 44 | 0.00 | 14 | 0.00 | 0.93 | 79 | 82 | 1.05 | 1.01 | 243 | {'reach': 13, 'drop': 1} |
| 55 | 0.00 | 14 | 0.00 | 1.00 | 75 | 90 | 1.29 | 0.92 | 148 | {'reach': 11, 'drop': 3} |
| 10 | 0.07 | 14 | 0.14 | 0.64 | 67 | 116 | 1.29 | 0.79 | 239 | {'drop': 6, 'reach': 7} |
| 23 | 0.07 | 14 | 0.14 | 0.93 | 60 | 112 | 0.38 | 0.70 | 297 | {'drop': 7, 'reach': 6} |
| 22 | 0.14 | 14 | 0.50 | 0.57 | 72 | 125 | 1.08 | 1.39 | 177 | {'reach': 7, 'drop': 5} |
| 52 | 0.14 | 14 | 0.14 | 0.71 | 50 | 99 | 0.45 | 0.76 | 169 | {'drop': 1, 'reach': 11} |
| 24 | 0.21 | 14 | 0.29 | 0.71 | 71 | 106 | 0.43 | 1.21 | 224 | {'reach': 5, 'drop': 6} |
| 48 | 0.21 | 14 | 0.21 | 1.00 | 76 | 103 | 0.70 | 0.63 | 254 | {'drop': 4, 'reach': 7} |
| 61 | 0.21 | 14 | 0.29 | 0.86 | 63 | 114 | 1.30 | 0.73 | 202 | {'drop': 4, 'reach': 7} |
| 19 | 0.29 | 14 | 0.43 | 0.93 | 71 | 121 | 0.45 | 0.42 | 477 | {'reach': 6, 'drop': 4} |
| 30 | 0.29 | 14 | 0.50 | 0.57 | 64 | 119 | 1.30 | 1.18 | 176 | {'drop': 6, 'reach': 4} |
| 45 | 0.29 | 14 | 0.29 | 0.86 | 73 | 103 | 0.40 | 1.49 | 179 | {'reach': 5, 'drop': 5} |
| 47 | 0.29 | 14 | 0.43 | 0.64 | 50 | 118 | 0.58 | 0.52 | 214 | {'drop': 7, 'reach': 3} |
| 54 | 0.29 | 14 | 0.29 | 0.79 | 69 | 111 | 0.65 | 0.87 | 216 | {'reach': 7, 'drop': 3} |
| 29 | 0.43 | 14 | 0.64 | 0.79 | 77 | 124 | 1.24 | 0.47 | 274 | {'drop': 5, 'reach': 3} |
| 46 | 0.43 | 14 | 0.43 | 0.93 | 75 | 136 | 1.13 | 0.59 | 414 | {'reach': 5, 'drop': 3} |
| 59 | 0.50 | 14 | 0.57 | 0.93 | 56 | 141 | 0.36 | 0.34 | 252 | {'reach': 5, 'drop': 2} |
| 1 | 0.60 | 15 | 0.60 | 0.87 | 62 | 145 | 1.34 | 1.44 | 156 | {'reach': 5, 'drop': 1} |
| 9 | 0.64 | 14 | 0.79 | 1.00 | 52 | 127 | 1.24 | 0.53 | 120 | {'drop': 2, 'reach': 3} |
| 42 | 0.64 | 14 | 0.79 | 0.71 | 74 | 143 | 0.34 | 1.36 | 279 | {'reach': 3, 'drop': 2} |
| 2 | 0.67 | 15 | 0.73 | 0.80 | 70 | 162 | 0.62 | 0.39 | 391 | {'drop': 3, 'reach': 2} |
| 32 | 0.71 | 14 | 0.71 | 0.93 | 73 | 198 | 0.59 | 1.15 | 586 | {'drop': 2, 'reach': 2} |
| 38 | 0.71 | 14 | 0.79 | 0.79 | 76 | 166 | 0.93 | 1.26 | 333 | {'reach': 1, 'drop': 3} |
| 13 | 0.79 | 14 | 0.79 | 0.93 | 67 | 158 | 0.35 | 1.43 | 242 | {'reach': 3} |
| 21 | 0.79 | 14 | 0.79 | 1.00 | 59 | 144 | 0.47 | 0.64 | 312 | {'reach': 3} |
| 51 | 0.79 | 14 | 0.93 | 0.86 | 49 | 148 | 0.59 | 0.78 | 215 | {'drop': 2, 'reach': 1} |
| 60 | 0.79 | 14 | 0.86 | 0.86 | 45 | 157 | 0.65 | 1.45 | 127 | {'drop': 2, 'reach': 1} |
| 62 | 0.79 | 14 | 0.86 | 0.64 | 77 | 262 | 1.04 | 1.23 | 324 | {'reach': 2, 'drop': 1} |
| 5 | 0.86 | 14 | 0.86 | 0.93 | 40 | 138 | 0.36 | 1.28 | 116 | {'drop': 2} |
| 12 | 0.86 | 14 | 0.86 | 0.64 | 73 | 255 | 1.47 | 0.34 | 438 | {'drop': 2} |
| 25 | 0.86 | 14 | 0.86 | 0.57 | 72 | 145 | 0.45 | 0.87 | 296 | {'reach': 1, 'drop': 1} |
| 26 | 0.86 | 14 | 0.86 | 0.93 | 60 | 162 | 0.84 | 1.06 | 202 | {'reach': 2} |
| 50 | 0.86 | 14 | 0.93 | 0.93 | 64 | 190 | 0.51 | 0.68 | 553 | {'reach': 1, 'drop': 1} |
| 57 | 0.86 | 14 | 0.93 | 0.71 | 72 | 219 | 0.56 | 0.41 | 432 | {'drop': 1, 'reach': 1} |
| 63 | 0.86 | 14 | 0.86 | 0.71 | 75 | 191 | 0.90 | 0.99 | 268 | {'drop': 1, 'reach': 1} |
| 64 | 0.86 | 14 | 1.00 | 0.36 | 78 | 251 | 1.34 | 0.73 | 302 | {'drop': 2} |
| 4 | 0.87 | 15 | 0.93 | 0.80 | 75 | 192 | 1.46 | 0.80 | 256 | {'drop': 1, 'reach': 1} |
| 8 | 0.93 | 14 | 1.00 | 0.86 | 72 | 249 | 1.23 | 1.32 | 422 | {'drop': 1} |
| 15 | 0.93 | 14 | 0.93 | 0.93 | 53 | 196 | 1.15 | 0.59 | 273 | {'drop': 1} |
| 16 | 0.93 | 14 | 1.00 | 0.93 | 71 | 261 | 1.39 | 1.35 | 398 | {'drop': 1} |
| 17 | 0.93 | 14 | 1.00 | 0.86 | 48 | 227 | 1.06 | 0.36 | 262 | {'drop': 1} |
| 28 | 0.93 | 14 | 0.93 | 1.00 | 62 | 198 | 0.58 | 1.18 | 274 | {'reach': 1} |
| 31 | 0.93 | 14 | 0.93 | 0.50 | 69 | 276 | 1.21 | 0.35 | 502 | {'reach': 1} |
| 35 | 0.93 | 14 | 0.93 | 0.93 | 44 | 160 | 1.02 | 1.23 | 107 | {'reach': 1} |
| 39 | 0.93 | 14 | 1.00 | 0.36 | 76 | 250 | 0.54 | 0.70 | 464 | {'drop': 1} |
| 41 | 0.93 | 14 | 0.93 | 0.93 | 70 | 180 | 1.03 | 0.54 | 272 | {'reach': 1} |
| 49 | 0.93 | 14 | 0.93 | 0.86 | 74 | 206 | 0.64 | 1.33 | 501 | {'reach': 1} |
| 53 | 0.93 | 14 | 1.00 | 1.00 | 78 | 199 | 1.34 | 1.16 | 456 | {'drop': 1} |
| 6 | 1.00 | 14 | 1.00 | 0.79 | 66 | 178 | 0.40 | 0.49 | 601 | {} |
| 11 | 1.00 | 14 | 1.00 | 1.00 | 48 | 190 | 1.23 | 0.94 | 156 | {} |
| 20 | 1.00 | 14 | 1.00 | 0.57 | 71 | 267 | 0.52 | 0.99 | 387 | {} |
| 33 | 1.00 | 14 | 1.00 | 0.93 | 66 | 268 | 1.02 | 1.02 | 340 | {} |
| 34 | 1.00 | 14 | 1.00 | 1.00 | 54 | 226 | 1.17 | 1.00 | 226 | {} |
| 36 | 1.00 | 14 | 1.00 | 1.00 | 46 | 213 | 0.88 | 0.55 | 269 | {} |
| 37 | 1.00 | 14 | 1.00 | 0.93 | 65 | 206 | 0.60 | 1.12 | 322 | {} |
| 40 | 1.00 | 14 | 1.00 | 0.86 | 71 | 210 | 0.88 | 1.23 | 333 | {} |
| 43 | 1.00 | 14 | 1.00 | 0.86 | 71 | 206 | 1.09 | 1.19 | 314 | {} |
| 56 | 1.00 | 14 | 1.00 | 0.93 | 64 | 178 | 1.14 | 0.90 | 205 | {} |
| 58 | 1.00 | 14 | 1.00 | 0.79 | 75 | 247 | 0.60 | 1.31 | 562 | {} |

### Held-out bank (65-80), canonical: 599 episodes

- success 0.534 [0.494, 0.574]; lifted 0.626 [0.587, 0.664] (n=599); palm at end 0.815 [0.782, 0.844] (n=599); thumb 0.790 [0.755, 0.820] (n=599); reach-failure rate 0.307; contacts at end 3.69; mean hold steps 279
- ends {'success': 320, 'reach failure': 184, 'drop': 94, 'liftBudget': 1}; drop phases {'lift phase': 44, 'before first pulse': 28, 'between pulses': 13, 'during pulse 1': 1, 'after last pulse': 8}; spawnTiltDeg classes {0: 599}; carry-over flags 0
- per-shape success: 16 shapes, min 0.00, median 0.68, max 0.95, SD 0.342; shapes <= 0.70: 8; shapes at 1.00: 0; episodes per shape 37-38

By e1 (axial profile):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e1 0.3-0.7 | 5 | 185 | 0.508 [0.437, 0.579] (n=185) | 0.562 | 0.768 | 3.68 | 0.368 | 4 / 0 / 2 / 3 / 14 |
| e1 0.7-1.1 | 5 | 187 | 0.818 [0.757, 0.867] (n=187) | 0.898 | 0.888 | 4.72 | 0.102 | 8 / 0 / 4 / 2 / 1 |
| e1 1.1-1.5 | 6 | 227 | 0.322 [0.264, 0.385] (n=227) | 0.454 | 0.793 | 2.87 | 0.427 | 16 / 1 / 7 / 3 / 29 |

By e2 (cross-section shape):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e2 0.3-0.7 | 6 | 225 | 0.533 [0.468, 0.597] (n=225) | 0.618 | 0.809 | 3.68 | 0.324 | 12 / 1 / 2 / 3 / 14 |
| e2 0.7-1.1 | 4 | 150 | 0.240 [0.179, 0.314] (n=150) | 0.347 | 0.733 | 2.71 | 0.527 | 7 / 0 / 7 / 2 / 19 |
| e2 1.1-1.5 | 6 | 224 | 0.732 [0.671, 0.786] (n=224) | 0.821 | 0.875 | 4.37 | 0.143 | 9 / 0 / 4 / 3 / 11 |

By cross-section 2·max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| cross-section 50-65 mm | 6 | 226 | 0.469 [0.405, 0.534] (n=226) | 0.531 | 0.854 | 3.47 | 0.389 | 7 / 0 / 5 / 1 / 19 |
| cross-section 65-80 mm | 10 | 373 | 0.574 [0.523, 0.623] (n=373) | 0.684 | 0.791 | 3.83 | 0.257 | 21 / 1 / 8 / 7 / 25 |

By length 2·a3:

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| length 130-210 mm | 9 | 337 | 0.727 [0.677, 0.772] (n=337) | 0.810 | 0.881 | 4.38 | 0.148 | 15 / 0 / 5 / 4 / 17 |
| length 210-280 mm | 2 | 74 | 0.851 [0.753, 0.915] (n=74) | 0.946 | 0.595 | 5.00 | 0.054 | 2 / 0 / 2 / 2 / 1 |
| length < 130 mm | 5 | 188 | 0.064 [0.037, 0.108] (n=188) | 0.170 | 0.782 | 1.95 | 0.691 | 11 / 1 / 6 / 2 / 26 |

By aspect a3 / max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| aspect 1.5-3 | 11 | 412 | 0.451 [0.404, 0.500] (n=412) | 0.563 | 0.823 | 3.39 | 0.357 | 25 / 1 / 11 / 6 / 35 |
| aspect < 1.5 | 1 | 37 | 0.000 [0.000, 0.094] (n=37) | 0.000 | 0.892 | 1.65 | 0.811 | 0 / 0 / 0 / 0 / 7 |
| aspect >= 3 | 4 | 150 | 0.893 [0.834, 0.933] (n=150) | 0.953 | 0.773 | 5.03 | 0.047 | 3 / 0 / 2 / 2 / 2 |

By volume tercile (cuts 171, 236 cm³):

| tercile | shapes | episodes | success | lifted | palm |
|---|---|---|---|---|---|
| low | 5 | 189 | 0.169 [0.123, 0.229] (n=189) | 0.312 | 0.762 |
| mid | 5 | 186 | 0.677 [0.607, 0.740] (n=186) | 0.769 | 0.860 |
| high | 6 | 224 | 0.723 [0.661, 0.778] (n=224) | 0.772 | 0.821 |

Per-shape success, ranked (all shapes):

| shape | success | n | lifted | palm | cross mm | length mm | e1 | e2 | volume cm³ | ends |
|---|---|---|---|---|---|---|---|---|---|---|
| 72 | 0.00 | 38 | 0.05 | 0.74 | 64 | 99 | 1.44 | 0.93 | 95 | {'drop': 6, 'reach': 32} |
| 73 | 0.00 | 37 | 0.00 | 0.89 | 71 | 92 | 0.58 | 0.78 | 258 | {'reach': 30, 'drop': 7} |
| 67 | 0.08 | 38 | 0.29 | 0.74 | 68 | 111 | 1.45 | 0.56 | 167 | {'reach': 23, 'drop': 12} |
| 80 | 0.08 | 37 | 0.11 | 0.81 | 59 | 93 | 0.54 | 0.32 | 137 | {'reach': 30, 'drop': 4} |
| 69 | 0.16 | 38 | 0.39 | 0.74 | 59 | 121 | 1.11 | 1.01 | 122 | {'drop': 17, 'reach': 15} |
| 68 | 0.47 | 38 | 0.58 | 0.82 | 66 | 134 | 1.50 | 1.33 | 171 | {'drop': 8, 'reach': 11, 'liftBudget': 1} |
| 71 | 0.53 | 38 | 0.71 | 0.79 | 67 | 136 | 1.47 | 0.65 | 122 | {'reach': 8, 'drop': 10} |
| 75 | 0.65 | 37 | 0.81 | 0.81 | 72 | 139 | 0.93 | 1.41 | 173 | {'reach': 7, 'drop': 6} |
| 76 | 0.70 | 37 | 0.70 | 0.95 | 57 | 149 | 1.11 | 0.58 | 204 | {'reach': 8, 'drop': 3} |
| 74 | 0.76 | 37 | 0.86 | 0.92 | 66 | 134 | 0.80 | 1.15 | 212 | {'reach': 5, 'drop': 4} |
| 77 | 0.76 | 37 | 0.81 | 0.95 | 71 | 151 | 0.54 | 1.50 | 387 | {'drop': 5, 'reach': 4} |
| 78 | 0.81 | 37 | 0.95 | 0.57 | 75 | 267 | 0.55 | 0.83 | 533 | {'drop': 5, 'reach': 2} |
| 79 | 0.81 | 37 | 0.89 | 0.81 | 68 | 203 | 1.09 | 1.39 | 178 | {'drop': 3, 'reach': 4} |
| 65 | 0.89 | 37 | 0.95 | 0.62 | 73 | 272 | 0.69 | 0.47 | 660 | {'reach': 2, 'drop': 2} |
| 66 | 0.92 | 38 | 0.95 | 0.95 | 57 | 175 | 1.05 | 0.64 | 255 | {'drop': 1, 'reach': 2} |
| 70 | 0.95 | 38 | 0.97 | 0.95 | 55 | 186 | 0.70 | 1.30 | 236 | {'drop': 1, 'reach': 1} |

## Part 4a. Zero-shot, pose mode **randomStable** (012, random theta, K = 50, perturb 1.0, mu 1.0, mass as 012, seeds 9001-9300)

### Train bank (1-64), randomStable: 897 episodes

- success 0.004 [0.002, 0.011]; lifted 0.012 [0.007, 0.022] (n=897); palm at end 0.785 [0.757, 0.810] (n=897); thumb 0.043 [0.032, 0.059] (n=897); reach-failure rate 0.899; contacts at end 0.77; mean hold steps 3
- ends {'drop': 84, 'reach failure': 806, 'success': 4, 'liftBudget': 3}; drop phases {'lift phase': 79, 'before first pulse': 3, 'after last pulse': 1, 'between pulses': 1}; spawnTiltDeg classes {10: 14, 80: 83, 90: 678, 100: 108, 170: 14}; carry-over flags 0
- per-shape success: 64 shapes, min 0.00, median 0.00, max 0.07, SD 0.017; shapes <= 0.70: 64; shapes at 1.00: 0; episodes per shape 13-15

By e1 (axial profile):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e1 0.3-0.7 | 28 | 392 | 0.010 [0.004, 0.026] (n=392) | 0.018 | 0.778 | 0.85 | 0.865 | 0 / 0 / 1 / 1 / 44 |
| e1 0.7-1.1 | 16 | 224 | 0.000 [0.000, 0.017] (n=224) | 0.009 | 0.777 | 0.72 | 0.951 | 1 / 0 / 0 / 0 / 10 |
| e1 1.1-1.5 | 20 | 281 | 0.000 [-0.000, 0.013] (n=281) | 0.007 | 0.801 | 0.69 | 0.904 | 2 / 0 / 0 / 0 / 25 |

By e2 (cross-section shape):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e2 0.3-0.7 | 21 | 295 | 0.010 [0.003, 0.029] (n=295) | 0.027 | 0.756 | 0.94 | 0.814 | 2 / 0 / 1 / 1 / 46 |
| e2 0.7-1.1 | 21 | 295 | 0.003 [0.001, 0.019] (n=295) | 0.007 | 0.780 | 0.66 | 0.959 | 0 / 0 / 0 / 0 / 10 |
| e2 1.1-1.5 | 22 | 307 | 0.000 [0.000, 0.012] (n=307) | 0.003 | 0.818 | 0.70 | 0.922 | 1 / 0 / 0 / 0 / 23 |

By cross-section 2·max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| cross-section 50-65 mm | 15 | 210 | 0.010 [0.003, 0.034] (n=210) | 0.014 | 0.805 | 0.71 | 0.924 | 0 / 0 / 1 / 0 / 12 |
| cross-section 65-80 mm | 40 | 560 | 0.004 [0.001, 0.013] (n=560) | 0.014 | 0.786 | 0.83 | 0.866 | 3 / 0 / 0 / 1 / 67 |
| cross-section < 50 mm | 9 | 127 | 0.000 [0.000, 0.029] (n=127) | 0.000 | 0.748 | 0.57 | 1.000 | 0 / 0 / 0 / 0 / 0 |

By length 2·a3:

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| length 130-210 mm | 28 | 393 | 0.003 [0.000, 0.014] (n=393) | 0.010 | 0.791 | 0.74 | 0.893 | 2 / 0 / 0 / 0 / 39 |
| length 210-280 mm | 15 | 210 | 0.000 [0.000, 0.018] (n=210) | 0.005 | 0.733 | 0.78 | 0.890 | 0 / 0 / 0 / 0 / 22 |
| length < 130 mm | 21 | 294 | 0.010 [0.003, 0.030] (n=294) | 0.020 | 0.813 | 0.80 | 0.912 | 1 / 0 / 1 / 1 / 18 |

By aspect a3 / max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| aspect 1.5-3 | 34 | 478 | 0.008 [0.003, 0.021] (n=478) | 0.021 | 0.805 | 0.82 | 0.870 | 3 / 0 / 1 / 1 / 51 |
| aspect < 1.5 | 8 | 111 | 0.000 [-0.000, 0.033] (n=111) | 0.000 | 0.820 | 0.77 | 0.946 | 0 / 0 / 0 / 0 / 6 |
| aspect >= 3 | 22 | 308 | 0.000 [0.000, 0.012] (n=308) | 0.003 | 0.740 | 0.69 | 0.925 | 0 / 0 / 0 / 0 / 22 |

By volume tercile (cuts 224, 314 cm³):

| tercile | shapes | episodes | success | lifted | palm |
|---|---|---|---|---|---|
| low | 21 | 294 | 0.003 [0.001, 0.019] (n=294) | 0.003 | 0.782 |
| mid | 21 | 294 | 0.003 [0.001, 0.019] (n=294) | 0.017 | 0.796 |
| high | 22 | 309 | 0.006 [0.002, 0.023] (n=309) | 0.016 | 0.777 |

Per-shape success, ranked (all shapes):

| shape | success | n | lifted | palm | cross mm | length mm | e1 | e2 | volume cm³ | ends |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 0.00 | 14 | 0.00 | 0.86 | 62 | 145 | 1.34 | 1.44 | 156 | {'reach': 14} |
| 2 | 0.00 | 15 | 0.00 | 0.60 | 70 | 162 | 0.62 | 0.39 | 391 | {'drop': 5, 'reach': 10} |
| 3 | 0.00 | 15 | 0.00 | 0.60 | 43 | 81 | 0.61 | 0.93 | 79 | {'reach': 15} |
| 4 | 0.00 | 15 | 0.00 | 0.80 | 75 | 192 | 1.46 | 0.80 | 256 | {'reach': 13, 'drop': 2} |
| 5 | 0.00 | 14 | 0.00 | 0.79 | 40 | 138 | 0.36 | 1.28 | 116 | {'reach': 14} |
| 7 | 0.00 | 14 | 0.00 | 0.93 | 69 | 85 | 0.51 | 0.58 | 255 | {'reach': 13, 'drop': 1} |
| 8 | 0.00 | 14 | 0.00 | 0.86 | 72 | 249 | 1.23 | 1.32 | 422 | {'reach': 13, 'drop': 1} |
| 9 | 0.00 | 14 | 0.00 | 0.79 | 52 | 127 | 1.24 | 0.53 | 120 | {'reach': 14} |
| 10 | 0.00 | 14 | 0.00 | 0.93 | 67 | 116 | 1.29 | 0.79 | 239 | {'reach': 13, 'drop': 1} |
| 11 | 0.00 | 14 | 0.00 | 0.57 | 48 | 190 | 1.23 | 0.94 | 156 | {'reach': 14} |
| 12 | 0.00 | 14 | 0.00 | 0.79 | 73 | 255 | 1.47 | 0.34 | 438 | {'reach': 10, 'drop': 4} |
| 13 | 0.00 | 14 | 0.00 | 0.79 | 67 | 158 | 0.35 | 1.43 | 242 | {'reach': 13, 'drop': 1} |
| 14 | 0.00 | 14 | 0.00 | 0.71 | 78 | 90 | 0.77 | 0.74 | 176 | {'reach': 14} |
| 15 | 0.00 | 14 | 0.00 | 0.64 | 53 | 196 | 1.15 | 0.59 | 273 | {'reach': 14} |
| 16 | 0.00 | 14 | 0.00 | 0.71 | 71 | 261 | 1.39 | 1.35 | 398 | {'reach': 13, 'drop': 1} |
| 17 | 0.00 | 14 | 0.00 | 0.64 | 48 | 227 | 1.06 | 0.36 | 262 | {'reach': 14} |
| 18 | 0.00 | 14 | 0.00 | 0.79 | 66 | 88 | 1.05 | 1.06 | 162 | {'reach': 14} |
| 20 | 0.00 | 14 | 0.00 | 0.64 | 71 | 267 | 0.52 | 0.99 | 387 | {'reach': 13, 'liftBudget': 1} |
| 21 | 0.00 | 14 | 0.00 | 0.79 | 59 | 144 | 0.47 | 0.64 | 312 | {'reach': 14} |
| 22 | 0.00 | 14 | 0.00 | 0.86 | 72 | 125 | 1.08 | 1.39 | 177 | {'reach': 14} |
| 24 | 0.00 | 14 | 0.00 | 0.71 | 71 | 106 | 0.43 | 1.21 | 224 | {'reach': 11, 'drop': 3} |
| 25 | 0.00 | 13 | 0.00 | 0.92 | 72 | 145 | 0.45 | 0.87 | 296 | {'reach': 13} |
| 26 | 0.00 | 14 | 0.00 | 0.71 | 60 | 162 | 0.84 | 1.06 | 202 | {'reach': 14} |
| 27 | 0.00 | 14 | 0.00 | 0.86 | 48 | 83 | 1.12 | 0.77 | 85 | {'reach': 14} |
| 28 | 0.00 | 14 | 0.00 | 0.86 | 62 | 198 | 0.58 | 1.18 | 274 | {'reach': 14} |
| 29 | 0.00 | 14 | 0.07 | 0.86 | 77 | 124 | 1.24 | 0.47 | 274 | {'reach': 13, 'drop': 1} |
| 30 | 0.00 | 14 | 0.00 | 0.86 | 64 | 119 | 1.30 | 1.18 | 176 | {'reach': 14} |
| 31 | 0.00 | 14 | 0.00 | 0.57 | 69 | 276 | 1.21 | 0.35 | 502 | {'reach': 8, 'drop': 6} |
| 32 | 0.00 | 14 | 0.00 | 0.86 | 73 | 198 | 0.59 | 1.15 | 586 | {'reach': 9, 'drop': 5} |
| 33 | 0.00 | 14 | 0.00 | 0.79 | 66 | 268 | 1.02 | 1.02 | 340 | {'reach': 14} |
| 34 | 0.00 | 14 | 0.00 | 0.86 | 54 | 226 | 1.17 | 1.00 | 226 | {'reach': 14} |
| 35 | 0.00 | 14 | 0.00 | 0.93 | 44 | 160 | 1.02 | 1.23 | 107 | {'reach': 14} |
| 36 | 0.00 | 14 | 0.00 | 0.93 | 46 | 213 | 0.88 | 0.55 | 269 | {'reach': 14} |
| 37 | 0.00 | 14 | 0.00 | 0.79 | 65 | 206 | 0.60 | 1.12 | 322 | {'reach': 13, 'drop': 1} |
| 38 | 0.00 | 14 | 0.00 | 0.86 | 76 | 166 | 0.93 | 1.26 | 333 | {'reach': 13, 'drop': 1} |
| 39 | 0.00 | 14 | 0.00 | 0.79 | 76 | 250 | 0.54 | 0.70 | 464 | {'reach': 13, 'drop': 1} |
| 40 | 0.00 | 14 | 0.00 | 0.71 | 71 | 210 | 0.88 | 1.23 | 333 | {'reach': 13, 'drop': 1} |
| 41 | 0.00 | 14 | 0.07 | 0.43 | 70 | 180 | 1.03 | 0.54 | 272 | {'reach': 9, 'drop': 5} |
| 42 | 0.00 | 14 | 0.00 | 0.86 | 74 | 143 | 0.34 | 1.36 | 279 | {'reach': 14} |
| 43 | 0.00 | 14 | 0.00 | 0.86 | 71 | 206 | 1.09 | 1.19 | 314 | {'reach': 14} |
| 44 | 0.00 | 14 | 0.00 | 0.86 | 79 | 82 | 1.05 | 1.01 | 243 | {'reach': 13, 'drop': 1} |
| 45 | 0.00 | 13 | 0.00 | 0.92 | 73 | 103 | 0.40 | 1.49 | 179 | {'reach': 13} |
| 46 | 0.00 | 14 | 0.00 | 0.86 | 75 | 136 | 1.13 | 0.59 | 414 | {'reach': 10, 'drop': 4} |
| 47 | 0.00 | 14 | 0.00 | 0.71 | 50 | 118 | 0.58 | 0.52 | 214 | {'reach': 13, 'liftBudget': 1} |
| 48 | 0.00 | 14 | 0.00 | 0.86 | 76 | 103 | 0.70 | 0.63 | 254 | {'reach': 13, 'drop': 1} |
| 49 | 0.00 | 14 | 0.00 | 0.79 | 74 | 206 | 0.64 | 1.33 | 501 | {'reach': 12, 'drop': 2} |
| 50 | 0.00 | 14 | 0.00 | 0.86 | 64 | 190 | 0.51 | 0.68 | 553 | {'reach': 12, 'drop': 2} |
| 51 | 0.00 | 14 | 0.00 | 0.71 | 49 | 148 | 0.59 | 0.78 | 215 | {'reach': 14} |
| 53 | 0.00 | 14 | 0.07 | 1.00 | 78 | 199 | 1.34 | 1.16 | 456 | {'drop': 5, 'reach': 9} |
| 54 | 0.00 | 14 | 0.00 | 0.64 | 69 | 111 | 0.65 | 0.87 | 216 | {'reach': 14} |
| 55 | 0.00 | 14 | 0.00 | 0.79 | 75 | 90 | 1.29 | 0.92 | 148 | {'reach': 14} |
| 56 | 0.00 | 14 | 0.00 | 0.93 | 64 | 178 | 1.14 | 0.90 | 205 | {'reach': 12, 'drop': 2} |
| 57 | 0.00 | 14 | 0.07 | 0.57 | 72 | 219 | 0.56 | 0.41 | 432 | {'reach': 9, 'drop': 5} |
| 58 | 0.00 | 14 | 0.00 | 0.79 | 75 | 247 | 0.60 | 1.31 | 562 | {'reach': 12, 'drop': 2} |
| 59 | 0.00 | 14 | 0.00 | 0.71 | 56 | 141 | 0.36 | 0.34 | 252 | {'reach': 12, 'drop': 2} |
| 60 | 0.00 | 14 | 0.00 | 0.71 | 45 | 157 | 0.65 | 1.45 | 127 | {'reach': 14} |
| 61 | 0.00 | 14 | 0.00 | 0.79 | 63 | 114 | 1.30 | 0.73 | 202 | {'reach': 14} |
| 62 | 0.00 | 14 | 0.00 | 0.64 | 77 | 262 | 1.04 | 1.23 | 324 | {'reach': 13, 'drop': 1} |
| 63 | 0.00 | 14 | 0.07 | 0.86 | 75 | 191 | 0.90 | 0.99 | 268 | {'reach': 13, 'drop': 1} |
| 64 | 0.00 | 14 | 0.00 | 0.71 | 78 | 251 | 1.34 | 0.73 | 302 | {'reach': 14} |
| 6 | 0.07 | 14 | 0.07 | 0.86 | 66 | 178 | 0.40 | 0.49 | 601 | {'reach': 10, 'drop': 3} |
| 19 | 0.07 | 14 | 0.14 | 0.93 | 71 | 121 | 0.45 | 0.42 | 477 | {'liftBudget': 1, 'drop': 6, 'reach': 6} |
| 23 | 0.07 | 14 | 0.14 | 0.79 | 60 | 112 | 0.38 | 0.70 | 297 | {'reach': 9, 'drop': 4} |
| 52 | 0.07 | 14 | 0.07 | 0.93 | 50 | 99 | 0.45 | 0.76 | 169 | {'drop': 3, 'reach': 10} |

### Held-out bank (65-80), randomStable: 598 episodes

- success 0.000 [0.000, 0.006]; lifted 0.005 [0.002, 0.015] (n=598); palm at end 0.774 [0.739, 0.806] (n=598); thumb 0.025 [0.015, 0.041] (n=598); reach-failure rate 0.941; contacts at end 0.57; mean hold steps 0
- ends {'reach failure': 563, 'drop': 35}; drop phases {'lift phase': 32, 'before first pulse': 2, 'between pulses': 1}; spawnTiltDeg classes {10: 5, 80: 93, 90: 420, 100: 75, 170: 5}; carry-over flags 0
- per-shape success: 16 shapes, min 0.00, median 0.00, max 0.00, SD 0.000; shapes <= 0.70: 16; shapes at 1.00: 0; episodes per shape 36-38

By e1 (axial profile):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e1 0.3-0.7 | 5 | 184 | 0.000 [-0.000, 0.020] (n=184) | 0.005 | 0.772 | 0.64 | 0.875 | 1 / 0 / 0 / 0 / 22 |
| e1 0.7-1.1 | 5 | 187 | 0.000 [0.000, 0.020] (n=187) | 0.000 | 0.813 | 0.51 | 0.973 | 0 / 0 / 0 / 0 / 5 |
| e1 1.1-1.5 | 6 | 227 | 0.000 [0.000, 0.017] (n=227) | 0.009 | 0.744 | 0.57 | 0.969 | 1 / 0 / 1 / 0 / 5 |

By e2 (cross-section shape):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| e2 0.3-0.7 | 6 | 225 | 0.000 [0.000, 0.017] (n=225) | 0.009 | 0.769 | 0.64 | 0.929 | 2 / 0 / 0 / 0 / 14 |
| e2 0.7-1.1 | 4 | 150 | 0.000 [0.000, 0.025] (n=150) | 0.000 | 0.753 | 0.52 | 0.953 | 0 / 0 / 0 / 0 / 7 |
| e2 1.1-1.5 | 6 | 223 | 0.000 [0.000, 0.017] (n=223) | 0.004 | 0.794 | 0.53 | 0.946 | 0 / 0 / 1 / 0 / 11 |

By cross-section 2·max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| cross-section 50-65 mm | 6 | 226 | 0.000 [0.000, 0.017] (n=226) | 0.004 | 0.770 | 0.55 | 0.965 | 1 / 0 / 0 / 0 / 7 |
| cross-section 65-80 mm | 10 | 372 | 0.000 [0.000, 0.010] (n=372) | 0.005 | 0.777 | 0.59 | 0.927 | 1 / 0 / 1 / 0 / 25 |

By length 2·a3:

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| length 130-210 mm | 9 | 336 | 0.000 [0.000, 0.011] (n=336) | 0.006 | 0.789 | 0.53 | 0.946 | 1 / 0 / 1 / 0 / 16 |
| length 210-280 mm | 2 | 74 | 0.000 [0.000, 0.049] (n=74) | 0.014 | 0.716 | 0.62 | 0.892 | 1 / 0 / 0 / 0 / 7 |
| length < 130 mm | 5 | 188 | 0.000 [0.000, 0.020] (n=188) | 0.000 | 0.771 | 0.62 | 0.952 | 0 / 0 / 0 / 0 / 9 |

By aspect a3 / max(a1, a2):

| family | shapes | episodes | success | lifted | palm | contacts | reach fail | drops before first pulse / during / between / after / lift phase |
|---|---|---|---|---|---|---|---|---|
| aspect 1.5-3 | 11 | 411 | 0.000 [-0.000, 0.009] (n=411) | 0.005 | 0.769 | 0.57 | 0.954 | 1 / 0 / 1 / 0 / 17 |
| aspect < 1.5 | 1 | 37 | 0.000 [0.000, 0.094] (n=37) | 0.000 | 0.838 | 0.76 | 0.865 | 0 / 0 / 0 / 0 / 5 |
| aspect >= 3 | 4 | 150 | 0.000 [0.000, 0.025] (n=150) | 0.007 | 0.773 | 0.54 | 0.927 | 1 / 0 / 0 / 0 / 10 |

By volume tercile (cuts 171, 236 cm³):

| tercile | shapes | episodes | success | lifted | palm |
|---|---|---|---|---|---|
| low | 5 | 189 | 0.000 [0.000, 0.020] (n=189) | 0.000 | 0.741 |
| mid | 5 | 186 | 0.000 [0.000, 0.020] (n=186) | 0.011 | 0.796 |
| high | 6 | 223 | 0.000 [0.000, 0.017] (n=223) | 0.004 | 0.785 |

Per-shape success, ranked (all shapes):

| shape | success | n | lifted | palm | cross mm | length mm | e1 | e2 | volume cm³ | ends |
|---|---|---|---|---|---|---|---|---|---|---|
| 65 | 0.00 | 37 | 0.03 | 0.68 | 73 | 272 | 0.69 | 0.47 | 660 | {'reach': 31, 'drop': 6} |
| 66 | 0.00 | 38 | 0.00 | 0.92 | 57 | 175 | 1.05 | 0.64 | 255 | {'reach': 35, 'drop': 3} |
| 67 | 0.00 | 38 | 0.00 | 0.79 | 68 | 111 | 1.45 | 0.56 | 167 | {'reach': 36, 'drop': 2} |
| 68 | 0.00 | 38 | 0.03 | 0.84 | 66 | 134 | 1.50 | 1.33 | 171 | {'reach': 36, 'drop': 2} |
| 69 | 0.00 | 38 | 0.00 | 0.74 | 59 | 121 | 1.11 | 1.01 | 122 | {'reach': 38} |
| 70 | 0.00 | 38 | 0.00 | 0.74 | 55 | 186 | 0.70 | 1.30 | 236 | {'reach': 38} |
| 71 | 0.00 | 38 | 0.00 | 0.68 | 67 | 136 | 1.47 | 0.65 | 122 | {'reach': 38} |
| 72 | 0.00 | 38 | 0.00 | 0.68 | 64 | 99 | 1.44 | 0.93 | 95 | {'reach': 38} |
| 73 | 0.00 | 37 | 0.00 | 0.84 | 71 | 92 | 0.58 | 0.78 | 258 | {'reach': 32, 'drop': 5} |
| 74 | 0.00 | 37 | 0.00 | 0.81 | 66 | 134 | 0.80 | 1.15 | 212 | {'reach': 36, 'drop': 1} |
| 75 | 0.00 | 37 | 0.00 | 0.89 | 72 | 139 | 0.93 | 1.41 | 173 | {'reach': 36, 'drop': 1} |
| 76 | 0.00 | 37 | 0.03 | 0.73 | 57 | 149 | 1.11 | 0.58 | 204 | {'reach': 34, 'drop': 3} |
| 77 | 0.00 | 36 | 0.00 | 0.78 | 71 | 151 | 0.54 | 1.50 | 387 | {'drop': 8, 'reach': 28} |
| 78 | 0.00 | 37 | 0.00 | 0.76 | 75 | 267 | 0.55 | 0.83 | 533 | {'drop': 2, 'reach': 35} |
| 79 | 0.00 | 37 | 0.00 | 0.70 | 68 | 203 | 1.09 | 1.39 | 178 | {'reach': 37} |
| 80 | 0.00 | 37 | 0.00 | 0.81 | 59 | 93 | 0.54 | 0.32 | 137 | {'reach': 35, 'drop': 2} |

## Part 4b. Range verdicts

- **canonical: SHAPE-RANGE** — overall 0.595 [0.570, 0.620] over 1499 episodes (train 0.636, held-out 0.534); shapes <= 0.70: 34 of 80; min shape 0.00.
- **randomStable: SHAPE-RANGE** — overall 0.003 [0.001, 0.007] over 1495 episodes (train 0.004, held-out 0.000); shapes <= 0.70: 80 of 80; min shape 0.00.

Paired canonical − randomStable (train + held-out): 1495 paired episodes (same seed, passIndex and shape; mass, theta and yaw draws shared).

| family | pairs | canonical | randomStable | canonical − randomStable [95 % CI] |
|---|---|---|---|---|
| e1 0.3-0.7 | 576 | 0.583 | 0.007 | +0.576 [+0.535, +0.618] |
| e1 0.7-1.1 | 411 | 0.725 | 0.000 | +0.725 [+0.682, +0.768] |
| e1 1.1-1.5 | 508 | 0.500 | 0.000 | +0.500 [+0.457, +0.543] |
| e2 0.3-0.7 | 520 | 0.596 | 0.006 | +0.590 [+0.547, +0.633] |
| e2 0.7-1.1 | 445 | 0.420 | 0.002 | +0.418 [+0.372, +0.464] |
| e2 1.1-1.5 | 530 | 0.738 | 0.000 | +0.738 [+0.700, +0.775] |
| cross-section 50-65 mm | 436 | 0.534 | 0.005 | +0.530 [+0.482, +0.578] |
| cross-section 65-80 mm | 932 | 0.608 | 0.002 | +0.606 [+0.575, +0.638] |
| cross-section < 50 mm | 127 | 0.693 | 0.000 | +0.693 [+0.613, +0.773] |
| length 130-210 mm | 729 | 0.779 | 0.001 | +0.778 [+0.748, +0.808] |
| length 210-280 mm | 284 | 0.912 | 0.000 | +0.912 [+0.879, +0.945] |
| length < 130 mm | 482 | 0.127 | 0.006 | +0.120 [+0.090, +0.151] |
| aspect 1.5-3 | 889 | 0.520 | 0.004 | +0.515 [+0.482, +0.548] |
| aspect < 1.5 | 148 | 0.061 | 0.000 | +0.061 [+0.022, +0.099] |
| aspect >= 3 | 458 | 0.910 | 0.000 | +0.910 [+0.884, +0.937] |
| **all** | 1495 | 0.594 | 0.003 | +0.591 [+0.566, +0.616] |

### Reading of Part 4a-4b

- **Canonical (upright, the measurement pose).** Train bank 0.636 [0.604, 0.666] over 900 episodes, held-out 0.534 over 599,
  overall 0.595 [0.570, 0.620], against 1.000 for the anchor on the same instrument and 0.264 / 0.107 on the old carried-tilt
  instrument. The three train passes agree to within 0.02 (0.650 / 0.627 / 0.630; old instrument 0.234 / 0.338 / 0.220) and the two
  held-out passes to within 0.02 (0.542 / 0.527; old 0.017 / 0.197): the pass-level scatter of `ZERO_SHOT.md` was the orientation
  chain, not shape noise. Reach failures fall from 51 % to 24 % of train episodes; every episode starts at 0.0° tilt with no carry-over
  flag.
- **The shape axis is length.** Objects 210-280 mm long succeed in 93 % of upright episodes (aspect ≥ 3: 92 %), 130-210 mm in 82 %,
  shorter than 130 mm in 17 % (aspect < 1.5: 7 %). Cross-section width (30-80 mm) is flat within 0.56-0.69, e1 is flat (0.62-0.65),
  and e2 keeps the non-monotone pattern of the first measurement (round cross-sections 0.46, rounded square 0.62, rounded diamond
  0.74), now on clean data: the policy grasps a 280 mm bar of any cross-section and a 90 mm puck of none. Per shape: 11 of 64 train
  shapes at 1.00, 26 at or below 0.70, min 0.00; the shapes at 0.00 are the short ones. Verdict **SHAPE-RANGE** (overall 0.595 ≤ 0.85;
  34 of 80 shapes ≤ 0.70), and this time the range is a property of the shapes, with a per-shape resolution of 14-15 episodes and
  no orientation confound.
- **randomStable (rest poses).** Overall 0.003 (train 0.004, held-out 0.000; 88-93 % reach failures), verdict **SHAPE-RANGE** trivially.
  On the anchor cylinder the same mode gives 0.303 (Part 2: two of six rest poses are upright and the policy grasps those). For 72 of
  the 81 shapes every rest pose is lying, so randomStable is, for this policy, a lying-object test, and run 012 has no grasp for a
  lying object of any length: the paired difference canonical − randomStable is +0.61 [+0.58, +0.64] overall and +0.93 on the long
  objects where the upright grasp works best. This is the gap 013 is meant to close by training in randomStable mode, and it is the
  largest single effect in this file.
- **Supersession.** The zero-shot numbers of `ZERO_SHOT.md` (train 0.264, held-out 0.107, overall 0.201; the per-shape ranking; the
  spawn-tilt split; the "length" effect at 0.065 / 0.33 / 0.42; the hand × shape grid at −0.128 paired; the pass-level spreads) were
  measured with the carried-tilt spawn and are superseded by this file. Still valid from that file: the bank itself, the 2a regression
  reasoning and the observation that a lying object defeats the policy; the last point is now measured directly by the randomStable
  passes. The wrist-pronation single-move pass (0.80) and the `HARD_HANDS.md` sensitivity passes were run on the old instrument at
  perturbation 2.0 / 0.4-3.0 kg with the anchor cylinder, where the carried tilt was at most 38° and 012-class hands succeed at 1.000
  on tilted cylinders; they are not superseded, but the combined pass of Part 4d is the first measurement of that hand on the new reset.

## Part 4c. Hand x shape grid, randomStable (fixed theta, 80 shapes x 5 episodes per hand, seeds 9301-9400 x passIndex 0-3, paired across hands)

| hand | thumb base | episodes | success | lifted | palm | contacts | reach fail | drops (lift phase / before first pulse / during / between / after) |
|---|---|---|---|---|---|---|---|---|
| 5 | masked | 399 | 0.008 [0.003, 0.022] (n=399) | 0.010 | 0.732 | 0.71 | 0.875 | 45 / 0 / 0 / 0 / 0 |
| 11 | masked | 396 | 0.008 [0.003, 0.022] (n=396) | 0.010 | 0.785 | 0.73 | 0.937 | 21 / 0 / 0 / 1 / 0 |
| 14 | masked | 399 | 0.005 [0.001, 0.018] (n=399) | 0.010 | 0.777 | 0.73 | 0.942 | 15 / 1 / 0 / 1 / 0 |
| 1 | active | 396 | 0.008 [0.003, 0.022] (n=396) | 0.015 | 0.798 | 0.61 | 0.970 | 6 / 3 / 0 / 0 / 0 |
| 2 | active | 397 | 0.013 [0.005, 0.029] (n=397) | 0.013 | 0.728 | 0.66 | 0.935 | 21 / 0 / 0 / 0 / 0 |
| 3 | active | 397 | 0.010 [0.004, 0.026] (n=397) | 0.010 | 0.688 | 0.76 | 0.892 | 38 / 0 / 0 / 0 / 0 |

Per hand x e1 (axial profile) (success; n per cell):

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | hard mean | easy mean | hard − easy |
|---|---|---|---|---|---|---|---|---|---|
| e1 0.3-0.7 | 0.02 (165) | 0.02 (164) | 0.01 (165) | 0.02 (164) | 0.03 (164) | 0.02 (164) | 0.02 | 0.02 | -0.01 |
| e1 0.7-1.1 | 0.00 (105) | 0.00 (104) | 0.00 (105) | 0.00 (104) | 0.00 (105) | 0.00 (105) | 0.00 | 0.00 | +0.00 |
| e1 1.1-1.5 | 0.00 (129) | 0.00 (128) | 0.00 (129) | 0.00 (128) | 0.00 (128) | 0.00 (128) | 0.00 | 0.00 | +0.00 |

Per hand x e2 (cross-section shape) (success; n per cell):

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | hard mean | easy mean | hard − easy |
|---|---|---|---|---|---|---|---|---|---|
| e2 0.3-0.7 | 0.02 (135) | 0.02 (133) | 0.01 (135) | 0.02 (133) | 0.04 (134) | 0.03 (134) | 0.02 | 0.03 | -0.01 |
| e2 0.7-1.1 | 0.00 (125) | 0.00 (124) | 0.00 (125) | 0.00 (124) | 0.00 (124) | 0.00 (124) | 0.00 | 0.00 | +0.00 |
| e2 1.1-1.5 | 0.00 (139) | 0.00 (139) | 0.00 (139) | 0.00 (139) | 0.00 (139) | 0.00 (139) | 0.00 | 0.00 | +0.00 |

Per hand x cross-section 2·max(a1, a2) (success; n per cell):

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | hard mean | easy mean | hard − easy |
|---|---|---|---|---|---|---|---|---|---|
| cross-section 50-65 mm | 0.01 (104) | 0.00 (102) | 0.01 (104) | 0.00 (102) | 0.01 (102) | 0.00 (102) | 0.01 | 0.00 | +0.00 |
| cross-section 65-80 mm | 0.01 (250) | 0.01 (249) | 0.00 (250) | 0.01 (249) | 0.02 (250) | 0.02 (250) | 0.01 | 0.01 | -0.01 |
| cross-section < 50 mm | 0.00 (45) | 0.00 (45) | 0.00 (45) | 0.00 (45) | 0.00 (45) | 0.00 (45) | 0.00 | 0.00 | +0.00 |

Per hand x length 2·a3 (success; n per cell):

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | hard mean | easy mean | hard − easy |
|---|---|---|---|---|---|---|---|---|---|
| length 130-210 mm | 0.01 (184) | 0.02 (182) | 0.01 (184) | 0.02 (182) | 0.02 (183) | 0.02 (183) | 0.01 | 0.02 | -0.01 |
| length 210-280 mm | 0.00 (85) | 0.00 (85) | 0.00 (85) | 0.00 (85) | 0.00 (85) | 0.00 (85) | 0.00 | 0.00 | +0.00 |
| length < 130 mm | 0.01 (130) | 0.00 (129) | 0.01 (130) | 0.00 (129) | 0.02 (129) | 0.01 (129) | 0.01 | 0.01 | -0.00 |

Per hand x aspect a3 / max(a1, a2) (success; n per cell):

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | hard mean | easy mean | hard − easy |
|---|---|---|---|---|---|---|---|---|---|
| aspect 1.5-3 | 0.01 (224) | 0.01 (221) | 0.01 (224) | 0.01 (221) | 0.02 (222) | 0.02 (222) | 0.01 | 0.02 | -0.01 |
| aspect < 1.5 | 0.00 (45) | 0.00 (45) | 0.00 (45) | 0.00 (45) | 0.00 (45) | 0.00 (45) | 0.00 | 0.00 | +0.00 |
| aspect >= 3 | 0.00 (130) | 0.00 (130) | 0.00 (130) | 0.00 (130) | 0.00 (130) | 0.00 (130) | 0.00 | 0.00 | +0.00 |

Paired per episode (same seed, pass and shape across the six hands): hard mean 0.007, easy mean 0.010, hard − easy -0.003 [-0.009, +0.002] over 396 episodes.

### Reading of Part 4c

The grid is at the floor and cannot resolve an interaction. In randomStable mode every hand scores 0.005-0.013 over 396-399 episodes
with 88-97 % reach failures and the few gated episodes dropped in the lift phase; paired per episode across the six hands the
thumb-base-masked hands sit at 0.007 against 0.010 for the easy hands, a difference of -0.003 [-0.009, +0.002] over 396 paired
episodes. Mechanically the six hands fail the same way: the palm reaches the object in 69-80 % of episodes (palm flag at the end) but
the average contact count at the end is 0.6-0.8 finger groups, i.e. the hand lands on a surface 30-80 mm high and neither wraps the
fingers under the object nor brings the thumb to it, so the six-segment gate is never met; whether the thumb base is driven makes no
difference when the thumb never reaches the object. The hard-hand deficit measured on upright objects (-0.128 paired on the old
instrument; 0.10 vs 0.23) is therefore neither confirmed nor refuted on rest poses: on this policy the question needs a policy that
grasps lying objects at all, i.e. the 013 training in randomStable mode. On the upright bank the hand x shape interaction was not
re-measured in this brief (the grid was specified in randomStable mode).

## Part 4d. Hand 14: thumb base re-activated AND wrist-pronation k at the median (anchor, canonical, perturb 2.0 / mass 0.4-3.0 kg, seeds 4001-4100)

- combined move: success 1.000 [0.963, 1.000], lifted 1.000, palm 0.990, reach failures 0, ends {'success': 100}; easy-hand range at this level 0.93-1.00 → **reaches the easy-hand range** (note: this pass runs on the deterministic reset; the single-move passes below ran on the old carried-tilt instrument)
- single moves (old instrument, HARD_HANDS.md / ZERO_SHOT.md): baseline 0.57 / 0.64; thumb base alone 0.89 [0.81, 0.94]; wrist pronation alone 0.80 [0.71, 0.87]; both plus the two other |z| moves were not run.

- Reading: with both moves the hand reaches 100 / 100 with palm contact 0.99 and no reach failure, above the easy-hand range at
  this level (0.93-1.00) and above either single move (thumb base 0.89, wrist pronation 0.80); the two axes found in
  `HARD_HANDS.md` and `ZERO_SHOT.md` are sufficient together to remove hand 14's deficit. Caveat: the combined pass ran on the new
  reset (no carried tilt) while the single-move passes ran on the old one; on the anchor cylinder the carried tilt was at most 38°
  and 012 succeeds at 1.000 on such tilts, so the instrument change is unlikely to account for the difference, but a same-instrument
  baseline of hand 14 at this level was not re-run.

## Files

43 passes (deterministic head, one job worker, per-episode reseed, run-012 model), log `passes_v2.json` with the config of every pass, run log `run_v2.log`, hand-14 theta `theta_h14_thumbBase_wristPron.json`.

- `throwaway_181735.csv`: n 10, success 1.000 [0.722, 1.000], reach failures 0, hash 2676025693a9c0e2
- `repro_first_181755.csv`: n 100, success 1.000 [0.963, 1.000], reach failures 0, hash 98c71b6ea102bd42
- `repro_later_181841.csv`: n 100, success 1.000 [0.963, 1.000], reach failures 0, hash 98c71b6ea102bd42
- `repro_rs_181926.csv`: n 99, success 0.303 [0.221, 0.400], reach failures 64, hash 6d05406887c1d71e
- `throwaway_182338.csv`: n 10, success 1.000 [0.722, 1.000], reach failures 0, hash 2676025693a9c0e2
- `repro_first_182357.csv`: n 100, success 1.000 [0.963, 1.000], reach failures 0, hash 98c71b6ea102bd42
- `repro_rs_182443.csv`: n 99, success 0.303 [0.221, 0.400], reach failures 64, hash 6d05406887c1d71e
- `anchor_canonical_7001_7300.csv`: n 300, success 1.000 [0.987, 1.000], reach failures 0, hash 97dc6dee31200cb5
- `train_canonical_9001_9300_p0.csv`: n 300, success 0.650 [0.594, 0.702], reach failures 67, hash 0ee1eff3d5a514bf
- `train_canonical_9001_9300_p1.csv`: n 300, success 0.627 [0.571, 0.679], reach failures 71, hash 638cbf6deb7b269b
- `train_canonical_9001_9300_p2.csv`: n 300, success 0.630 [0.574, 0.683], reach failures 74, hash 7e81b9facd058323
- `heldout_canonical_9001_9300_p0.csv`: n 299, success 0.542 [0.485, 0.597], reach failures 88, hash 97ef8b15e655fae8
- `heldout_canonical_9001_9300_p1.csv`: n 300, success 0.527 [0.470, 0.582], reach failures 96, hash b2e8fc4e1d2b3294
- `train_randomStable_9001_9300_p0.csv`: n 299, success 0.013 [0.005, 0.034], reach failures 263, hash 0b5a1d08285d8dcf
- `train_randomStable_9001_9300_p1.csv`: n 299, success 0.000 [0.000, 0.013], reach failures 279, hash 415f747c48ed08b5
- `train_randomStable_9001_9300_p2.csv`: n 299, success 0.000 [0.000, 0.013], reach failures 264, hash c4a894223ca698ee
- `heldout_randomStable_9001_9300_p0.csv`: n 299, success 0.000 [0.000, 0.013], reach failures 276, hash 67c2b0e0d294357d
- `heldout_randomStable_9001_9300_p1.csv`: n 299, success 0.000 [0.000, 0.013], reach failures 287, hash f8ae508478389716
- `grid_rs_h05_9301_9400_p0.csv`: n 99, success 0.020 [0.006, 0.071], reach failures 86, hash d090f1a0d3a352c9
- `grid_rs_h05_9301_9400_p1.csv`: n 100, success 0.010 [0.002, 0.054], reach failures 89, hash 1acf3465919779db
- `grid_rs_h05_9301_9400_p2.csv`: n 100, success 0.000 [0.000, 0.037], reach failures 92, hash d0b8b0676769d090
- `grid_rs_h05_9301_9400_p3.csv`: n 100, success 0.000 [0.000, 0.037], reach failures 82, hash 24add7007dc447ed
- `grid_rs_h11_9301_9400_p0.csv`: n 99, success 0.020 [0.006, 0.071], reach failures 95, hash 13ba23bccd0d11ff
- `grid_rs_h11_9301_9400_p1.csv`: n 99, success 0.010 [0.002, 0.055], reach failures 95, hash ee70f4ed95fc0c52
- `grid_rs_h11_9301_9400_p2.csv`: n 99, success 0.000 [0.000, 0.037], reach failures 91, hash 8ba500944edf0434
- `grid_rs_h11_9301_9400_p3.csv`: n 99, success 0.000 [0.000, 0.037], reach failures 90, hash 6bea1b5aa7142aca
- `grid_rs_h14_9301_9400_p0.csv`: n 99, success 0.020 [0.006, 0.071], reach failures 93, hash 1ea115d95f790536
- `grid_rs_h14_9301_9400_p1.csv`: n 100, success 0.000 [0.000, 0.037], reach failures 96, hash 87491b6d5d084150
- `grid_rs_h14_9301_9400_p2.csv`: n 100, success 0.000 [0.000, 0.037], reach failures 95, hash 8674c9f76e94762e
- `grid_rs_h14_9301_9400_p3.csv`: n 100, success 0.000 [0.000, 0.037], reach failures 92, hash 52ad5af1a90818f8
- `grid_rs_h01_9301_9400_p0.csv`: n 99, success 0.020 [0.006, 0.071], reach failures 96, hash 2575ee848d67954e
- `grid_rs_h01_9301_9400_p1.csv`: n 99, success 0.010 [0.002, 0.055], reach failures 96, hash 3c28b4fa0d27a417
- `grid_rs_h01_9301_9400_p2.csv`: n 99, success 0.000 [0.000, 0.037], reach failures 96, hash 78f03edf5b2e33d1
- `grid_rs_h01_9301_9400_p3.csv`: n 99, success 0.000 [0.000, 0.037], reach failures 96, hash 4bec10c73a4d0ecb
- `grid_rs_h02_9301_9400_p0.csv`: n 99, success 0.030 [0.010, 0.085], reach failures 92, hash 8ad65c487fb41779
- `grid_rs_h02_9301_9400_p1.csv`: n 99, success 0.010 [0.002, 0.055], reach failures 92, hash 8f766fc92c443d7f
- `grid_rs_h02_9301_9400_p2.csv`: n 100, success 0.010 [0.002, 0.054], reach failures 94, hash 3b6761cccad611cb
- `grid_rs_h02_9301_9400_p3.csv`: n 99, success 0.000 [0.000, 0.037], reach failures 93, hash 847ef533d5670c8d
- `grid_rs_h03_9301_9400_p0.csv`: n 99, success 0.020 [0.006, 0.071], reach failures 90, hash d01abb8e76eb178b
- `grid_rs_h03_9301_9400_p1.csv`: n 99, success 0.010 [0.002, 0.055], reach failures 89, hash 855d26b080d8c9aa
- `grid_rs_h03_9301_9400_p2.csv`: n 100, success 0.010 [0.002, 0.054], reach failures 90, hash 2362cd27f764de27
- `grid_rs_h03_9301_9400_p3.csv`: n 99, success 0.000 [0.000, 0.037], reach failures 85, hash c9abfb1b7a3d7fc7
- `h14_thumbBase_wristPron_4001_4100.csv`: n 100, success 1.000 [0.963, 1.000], reach failures 0, hash 7274d61beebcc668
