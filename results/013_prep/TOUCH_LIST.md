# Run 013 prep, Part 1: object bank + per-episode swap — touch list (2026-09-27)

Every item of the 3a inventory in `docs/design/013_object_randomization.md`, with what the bank change did to it. Rule of the
brief: logic changes only where code reads cylinder geometry (radius / height constants, axis assumptions) in the reach target,
contact gate, lift threshold, drop rule, pedestal placement, diagnostics / overlay. Reward weights, observation contents, action
mapping and the DecisionRequester were checked for geometry dependence and are unchanged. Line numbers are post-edit (main after
e977c39 + this change).

## New files (allowed locations)

| file | role |
|---|---|
| `tools/objects/superquadric_bank.py` | generator: (seed, index) -> (a1, a2, a3, e1, e2), 114-vertex / 224-triangle meshes, OBJ + `bank.csv` (params, analytic and mesh volume, 3-axis extents, hull checks), `BANK_SEED.txt`; `--check-anchor` vertex-diffs the exported anchor |
| `Assets/Objects/Bank/` | `bank.csv`, `BANK_SEED.txt` (13013), `obj_000.obj` (anchor export), `obj_001..064.obj` (train), `obj_065..080.obj` (held-out), Unity `.meta` files |
| `Assets/Scripts/Objects/ObjectBank.cs` | runtime: attaches itself to the `Cylinder`-tagged object at scene load (`RuntimeInitializeOnLoadMethod`, no scene edit), loads the bank, cooks every convex hull once (`Physics.BakeMesh`), swaps mesh + collider per episode by `object/mode` / `object/index` / `object/seed`; default `anchorOnly` touches nothing |
| `tools/objects/run_zero_shot.py` | pass driver for 2a-2e through `tools/bo/gates_runner.run_pass` (unchanged) |

## Bank facts

- Anchor (index 0) = the scene's own `Mesh` object (Unity's built-in cylinder, 88 vertices / 80 triangles, 42 unique positions)
  kept as a reference with the scene scale (0.05, 0.14, 0.05); not regenerated, not reloaded. Its Editor export `obj_000.obj`
  (vertices × scale, metres) matches a regenerated 20-gon prism of radius 25 mm and half-length 140 mm with a maximum
  nearest-neighbour distance of 4.0e-5 mm both ways (float32 rounding), 20 ring vertices per cap, radius 24.99998 mm, half-length
  140.000 mm. Its mesh volume is 540.8 cm³ against the analytic 549.8 cm³ (a 20-gon prism).
- Shapes 1-80: 114 vertices each (16 azimuths × 7 latitude rings + 2 poles), 224 triangles, every vertex an extreme point of the
  strictly convex surface (largest convexity slack −0.15 mm, i.e. all other vertices strictly inside every supporting plane), so
  the convex hull is exactly the 114 vertices: ≤ 255 vertices and ≤ 255 triangles even without coplanar merging. Mesh volume is
  93.7-95.5 % of the analytic superellipsoid volume (faceting). Draw ranges realised: a1 15.1-38.8 mm, a2 15.0-39.5 mm, a3
  40.4-138 mm, e1 0.34-1.50, e2 0.32-1.50, volume 79-660 cm³.
- Orientation: local Y = length axis (a3), vertical on the pedestal like the anchor; local X / Z = the cross-section (a1, a2, e2)
  the hand closes on; the existing yaw draw about world Y and the tilt rule about `transform.up` keep their meaning.
- Rest pose: the object is kinematic until the contact gate (`ArmGraspAgent.cs:381`), so no bank shape can move or topple on the
  pedestal before the gate; the swap places the lowest point of the shape (at the object's current, carried X/Z tilt, as the
  existing spawn does) `restClearance` = 0.5 mm above the platform top through `agent.ObjectHalfHeight`. No shape was skipped.

## Inventory items

| 3a item | file:line (post-edit) | status | note |
|---|---|---|---|
| object lookup by tag, generic `Collider` / `Rigidbody` cache | `ArmGraspAgent.cs:292-298` | unchanged | tag `Cylinder` kept on the swapped object |
| prefab visual mesh, scene MeshCollider mesh | `New_ExperimentalSetup.prefab:1974`, `Dynamic_Scene.unity:1053-1074` | unchanged (assets) | the swap assigns `MeshFilter.sharedMesh` / `MeshCollider.sharedMesh` at runtime (`ObjectBank.cs`, `Apply`); the scene's mesh is what index 0 restores |
| scene transform scale (0.05, 0.14, 0.05) | `Dynamic_Scene.unity:853-863` | unchanged (asset) | bank meshes are in metres: the swap sets `localScale = 1` and restores the scene scale for index 0 |
| contact offset on the object | `ArmGraspAgent.cs:296` | unchanged | re-applied by the swap (read before, set after) |
| `m_CylHalfHeight` at Initialize | `ArmGraspAgent.cs:298` | **logic change (pedestal placement)** | new accessor `ObjectHalfHeight` (`:174-176`); `ObjectBank` sets it per episode for a bank shape (`position.y − bounds.min.y` at the current rotation) and restores the Initialize value for the anchor; `SpawnCylinder` (`:427`) unchanged |
| mass draw, `m_MassObs`, `mass/min` `mass/max` | `ArmGraspAgent.cs:107`, `:374-376` | unchanged | drawn exactly as 012 (decision) |
| mass applied, centre of mass / inertia | `ArmGraspAgent.cs:381`, `:678-684` | unchanged | the swap calls `ResetCenterOfMass` / `ResetInertiaTensor` on the object Rigidbody so PhysX recomputes them from the new convex hull (Unity default: uniform density) |
| friction material `ObjectMu` | `ArmGraspAgent.cs:115-116`, `:299-303` | unchanged | stays on the collider through the mesh swap; harness override untouched |
| platform, `m_PlatformTop` | `ArmGraspAgent.cs:87`, `:305-307` | unchanged | |
| spawn parameters, disk, reach range | `ArmGraspAgent.cs:57-65`, `:425-439` | unchanged | |
| rest height `restY` | `ArmGraspAgent.cs:427` | unchanged code, new input | uses `m_CylHalfHeight`, now per-episode for bank shapes (see above) |
| yaw about world Y with carried X/Z tilt | `ArmGraspAgent.cs:430`, `:437` | unchanged | length axis vertical for every shape |
| spawn overlap rejection (`ComputePenetration`) | `ArmGraspAgent.cs:438`, `:446-453` | unchanged | generic for convex colliders |
| reset order: bank call inserted | `ArmGraspAgent.cs:396` | **logic change (pedestal placement)** | `ObjectBank.Instance.ApplyForEpisode(this)` after the object is made kinematic with its mass and after the rig rebuild, immediately before `SpawnCylinder()`; no-op in `anchorOnly` |
| vector observation 0-23 | `ArmGraspAgent.cs:457-479` | unchanged | object centre and mass only; no geometry read |
| JointTokens (16 × 13), token [12] centre distance | `ArmGraspAgent.cs:483-518` | unchanged | no geometry read |
| ten ray sensors (tag `Cylinder`) | `Dynamic_Scene.unity:425` … `:3616` | unchanged | tag kept; ONNX inputs `obs_1`-`obs_10` unchanged |
| actions (19), mapping | `ArmGraspAgent.cs:150-152`, `:528-564` | unchanged | |
| DecisionRequester (period 10) | `Dynamic_Scene.unity:1526-1528` | unchanged | |
| grasp point (palm-frame offset, finger-length scaling) | `ArmGraspAgent.cs:76-77`, `:146-148` | unchanged | reads no object geometry; the 50 mm-object sizing of the offset is a task property, left as is |
| contact detection (tag filter, links) | `ArticulatedHand.cs:547-560` | unchanged | |
| contact gate (6 segments, 2 fingers, thumb) | `ArmGraspAgent.cs:569-584`, `:620-621` | unchanged | no geometry read |
| gate transition, `ReleaseObject` | `ArmGraspAgent.cs:631-638`, `:678-684` | unchanged | |
| lift threshold (AABB bottom + 0.03) | `ArmGraspAgent.cs:90-91`, `:690` | unchanged | AABB based, generic |
| drop rule: distance, tilt 60° about `transform.up`, below platform | `ArmGraspAgent.cs:103-106`, `:691-697` | unchanged | the axis is the length axis for every bank shape; the tilt rule's meaning weakens for aspect a3 / max(a1, a2) < 1.5 (13 of 80 shapes); left unchanged on purpose so the task definition is identical for the zero-shot comparison; flagged for the 013 design |
| hold, pulses (centre-of-mass force, couple torque) | `ArmGraspAgent.cs:108-114`, `:650-660`, `:699-726` | unchanged | no shape dependence |
| shaping (`ClosestPoint`, centre term), penalties, bonus | `ArmGraspAgent.cs:591-605`, `:611`, `:860-864` | unchanged | `ClosestPoint` is generic; no reward weight touched |
| penetration statistic (`maxPenMm`) | `ArmGraspAgent.cs:613-616` | unchanged | generic |
| `MakeSample` azimuth / height about `transform.up` | `ArmGraspAgent.cs:664-672` | unchanged | diagnostics only; the axis is the length axis |
| `MorphologyWatchOverlay` | `MorphologyWatchOverlay.cs:45-73` | unchanged | reads no radius / height |
| `PhysicsViewOverlay` object drawing | `PhysicsViewOverlay.cs:70-81` | **logic change (overlay)** | drew a unit cylinder primitive at scale 1.02 (assumed the object is a scaled unit cylinder); now draws the object's `MeshCollider.sharedMesh` in the object's frame at 1.02, rebuilt with the skeleton every episode |
| `ArticulatedGates` scripted placement (`objRadius`, `halfH` from the upright AABB) | `ArticulatedGates.cs:118`, `:284`, `:350-367` | unchanged | scripted modes only (not used here); eval mode does not place the object |
| `ArticulatedGates` eval: object plumbing | `ArticulatedGates.cs:101-109`, `:186-192`, `:218-226`, `:276`, `:285`, `:295` | **Diagnostics plumbing** | config `objectMode`, `objectIndex`, `objectSeed`, `objectStratify`; pushes `object/mode`, `object/index`, `object/seed` to the environment parameters; per-episode stratified `object/index` in the eval hook; snapshot of the ended episode's object in the hook; nine trailing CSV columns (`objIndex,objSplit,objA1mm,objA2mm,objA3mm,objE1,objE2,objVolCm3,objSpawnTiltDeg`) only when an object mode or stratification is configured, so an unconfigured pass writes the previous CSV format byte for byte |
| `CheckpointTheater` guard (24, 16 × 13) | `CheckpointTheater.cs:92-108` | unchanged | |
| `BoEvalHarness` (kinematic-era player) | `BoEvalHarness.cs` | unchanged | not the current instrument |
| `Obj_Reset.cs` | – | unchanged | unused script |

Stop-condition check (1c): none of the reward terms, observation contents, action mapping or DecisionRequester code paths read
cylinder geometry; nothing in them was edited (`git diff` of `ArmGraspAgent.cs` is the accessor and the one call above).

## 1d layout proof

- The ONNX (`results/012/Prosthetic.onnx` = the deployed `Assets/Models/Prosthetic.onnx`) and the scene's BehaviorParameters are
  untouched (no scene, prefab or model file in the diff). The model's 12 inputs (`obs_0` [16, 13], `obs_1`-`obs_10` ray sensors
  9 × [3] + 1 × [2], `obs_11` [24]) and 19 continuous actions are as recorded in the design document.
- After the recompile (0 console errors) the Editor demo (`Dynamic_Scene`, InferenceOnly, deployed model, 45 s of Play with no
  harness) ran with 0 errors; `[ObjectBank] loaded 81 entries (seed 13013)`, anchor exported, mode `anchorOnly` by default.
- The harness throwaway pass (seeds 6091-6100, deployed 012, deterministic head) ran 10 / 10 successes through the unchanged
  sensor stack, which is the operational shape check (a sensor / input mismatch fails at policy creation).
