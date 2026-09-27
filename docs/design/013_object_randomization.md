# Run 013 design recon: object-shape randomization (2026-09-27, read-only)

Scope: what the code assumes about the grasped object today, what a superquadric object family would need, three
observation options, curriculum / evaluation / BO-objective questions, and the risks with the cheapest test for each.
No recommendation is made; the trade-offs are laid out for the decision. Nothing under `Assets/` or `tools/` was
changed for this document. Line numbers refer to main at commit a4470b3.

Context that motivates this: the Part B probe (`results/bo/partB/PARTB.md`) found the deployed run-012 policy at
0.95-0.99 success over the training morphology distribution at every stressor tried, so the current task has no range
for a morphology objective. `results/bo/partB/HARD_HANDS.md` finds the only large theta effect to be a masked thumb
base. A new object family is the candidate stressor on which hand geometry (finger length, span, thumb reach) should
matter.

## 3a. Inventory: everything that creates, observes, or assumes the object

The object is **not** a scene GameObject: it is `Cylinder` inside `Assets/Prefabs/New_ExperimentalSetup.prefab`. The
scene removes the prefab's BoxCollider and adds a **convex MeshCollider** on Unity's built-in cylinder mesh. There is no
CapsuleCollider (the README's capsule describes the kinematic-era scene).

### Creation, collider, Rigidbody, mass

| item | where | current behaviour | shape assumption |
|---|---|---|---|
| lookup | `Assets/Scripts/ArmGraspAgent.cs:289` | `GameObject.FindGameObjectWithTag("Cylinder")`; cached as `Transform` + generic `Collider` + `Rigidbody` (`:188-189`, `:273`, `:292`) | tag only; collider type is not assumed |
| prefab object | `Assets/Prefabs/New_ExperimentalSetup.prefab:1949-1974` | GameObject `Cylinder`, tag `Cylinder`, layer Default; MeshFilter = built-in Cylinder mesh (fileID 10206, radius 0.5 / height 2 in mesh units, axis local Y) | visual mesh is a cylinder |
| prefab Rigidbody | `New_ExperimentalSetup.prefab:2018-2032` | mass 1, drag 0, angular drag 0.05, constraints 126 (freeze all) | none |
| scene overrides | `Assets/Scenes/Dynamic_Scene.unity:837-875` | Rigidbody: gravity off, Interpolate, kinematic, ContinuousSpeculative; local scale (0.05, 0.14, 0.05) → **radius 0.025 m, height 0.28 m, axis local Y**; local position (−0.0814, 0.0193, −1.7160) | size lives only in the transform scale |
| scene collider | `Dynamic_Scene.unity:945-952`, `:1053-1074` | prefab BoxCollider removed; added MeshCollider, `m_Convex: 1`, `m_CookingOptions: 30`, mesh = built-in cylinder, no material | a convex hull of the polygonal prism, not an analytic cylinder |
| contact offset | `ArmGraspAgent.cs:293` | `cylinderCollider.contactOffset = m_Hand.contactOffset` (0.0036 m, `ArticulatedHand.cs:131`) | none |
| half-height | `ArmGraspAgent.cs:295` | `m_CylHalfHeight = position.y − bounds.min.y` at Initialize (0.14 m upright) | **assumes the object is upright at Initialize**; used for the rest height (`:423`) and the platform fallback (`:304`) |
| mass draw | `ArmGraspAgent.cs:107`, `:374-376` | `massRange (0.2, 1.5)`; env `mass/min`, `mass/max`; **log-uniform**; observation `Clamp01(ln(m/0.2)/ln(7.5))` normalised with the **serialized** range, not the env range | mass independent of shape |
| mass applied | `ArmGraspAgent.cs:378`, `:678-680` | `rb.mass = m_Mass` at reset and again at release; centre of mass and inertia tensor are never set (PhysX derives them from the convex collider, uniform density) | inertia follows the collider automatically |
| `Dynamic_Scene_Train.unity` | same overrides | differs from `Dynamic_Scene` only in model / BehaviorType and a stale `taskBudgetSteps: 350` (`:1622`) | – |

### Friction

| item | where | current behaviour |
|---|---|---|
| material | `ArmGraspAgent.cs:115-116`, `:296-300` | `objectFriction = 1.0`; `PhysicsMaterial("ObjectMu")` static = dynamic = μ, frictionCombine Maximum, bounce 0 / Minimum; assigned to `cylinderCollider.sharedMaterial` at Initialize |
| harness override | `Assets/Scripts/Diagnostics/ArticulatedGates.cs:69`, `:231-236` | mutates the **live** `sharedMaterial` (the field alone never reaches the physics) |
| BoEval | `Assets/Scripts/BoEval/BoEvalHarness.cs:144`, `:367`, `:370/:439/:457` | its own `BoEvalMu` material per μ level; resets `sharedMaterial = null` afterwards (discards `ObjectMu` for the rest of the session) |
| scene | `Dynamic_Scene.unity:1059`; `New_ExperimentalSetup.prefab:2127` | MeshCollider and Platform BoxCollider both have no material asset |

### Pedestal and spawn pose

| item | where | current behaviour | shape assumption |
|---|---|---|---|
| platform | `New_ExperimentalSetup.prefab:2061-2145`; `Dynamic_Scene.unity:877-903` | built-in cube, BoxCollider size (2, 2, 2) (half-extents = transform scale), kinematic; scale (0.3856, 0.0463, 0.3863), local position (−0.0573, −0.1464, −1.7085) → **top ≈ 0.571 m**, footprint ≈ 0.77 × 0.77 m (a slab, not a narrow pedestal) | none |
| platform top | `ArmGraspAgent.cs:87`, `:302-304` | `m_PlatformTop = platCol.bounds.max.y`; fallback `spawnCenter.y − m_CylHalfHeight` | fallback assumes upright object |
| spawn params | `ArmGraspAgent.cs:57-65`; scene `:1598-1602`, `:1617-1618` | `spawnCenter (−0.329, 0.690, 5.474)`, disk radius 0.09 (env `spawn_radius`), `spawnHeightRange 0.036`, `randomizeYaw`, `reachRange (0.396, 0.54)` from the shoulder pivot, `restOnPlatform` true, `restClearance 0.0005` | – |
| rest height | `ArmGraspAgent.cs:423` | `restY = m_PlatformTop + m_CylHalfHeight + restClearance` | **upright cylinder resting on its flat end** (0.5 mm above the slab; kinematic, so it never settles) |
| yaw | `ArmGraspAgent.cs:426`, `:433` | `Euler(0, U[0, 360), 0) * baseRot`, keeping the current X/Z Euler angles | rotation about world Y assumes a vertical symmetry axis |
| overlap rejection | `ArmGraspAgent.cs:434`, `:442-449` | `Physics.ComputePenetration` of every arm / finger collider against `cylinderCollider` at the candidate pose, 50 attempts | works for any convex collider |
| harness placement | `ArticulatedGates.cs:275`, `:339-358` | `objRadius = 0.5 · bounds.size.x` (default 0.025), `halfH` from the upright AABB, rotation identity | scripted modes only; assumes a round upright object |
| BoEval oracle | `BoEvalHarness.cs:298-307` | teleports to `GraspPoint`, identity rotation, backs off along the palm normal in 1 mm steps | identity rotation = upright cylinder |

### Observations and actions

Vector observation, 24 floats (`ArmGraspAgent.cs:453-476`; scene `VectorObservationSize: 24`, `Dynamic_Scene.unity:1729`):

| index | line | content | object-related |
|---|---|---|---|
| 0-2 | `:455-458` | object **centre** in the palm frame / `targetObsScale` 0.36, clamped to 1.5 | yes (position only) |
| 3 | `:461` | hold progress `m_HoldSteps / holdNeeded` | indirect |
| 4 | `:462` | touching finger groups / 14 | indirect |
| 5 | `:463` | hold-pay progress | no |
| 6 | `:464` | phase code 0 / 0.5 / 1 | indirect |
| 7 | `:465` | `m_MassObs` | yes (mass) |
| 8-17 | `:467-472` | sin / cos of the five arm axes | no |
| 18-22 | `:474` | five finger length scales | no |
| 23 | `:475` | active groups / 14 | no |

No object velocity, orientation, yaw, radius, height or shape enters the vector.

JointTokens BufferSensor, 16 tokens × 13 floats (`ArmGraspAgent.cs:161-162`, `:479-515`; scene `:1778-1780`): one token per
unmasked finger group, then two wrist tokens. Token [12] is the only object entry: `|link pivot − object centre| / workspaceScale (0.15)`
clamped to 1.5 (`:484`, `:492`, `:514`), a centre distance, not a surface distance. Token [1] is the link length / 0.06 (wrist
tokens carry `HandSpan` / 0.06).

**Ray sensors.** Ten `RayPerceptionSensorComponent3D` on the fingertip bones of the active agent are policy inputs as well
(`Dynamic_Scene.unity:425`, `:632`, `:1176`, `:1947`, `:2008`, `:2891`, `:3174`, `:3430`, `:3491`, `:3616`; `m_UseChildSensors: 1` at
`:1745`). Nine detect the tag list `[Cylinder]`, one (`RayRingend`, `:3431`) has an empty tag list; one ray each
(`m_RaysPerDirection: 0`), `m_MaxRayDegrees 70`, `m_RayLength 20`, **`m_SphereCastRadius 0.5`**, `RayPointermiddle` differs
(`m_MaxRayDegrees 0`, `m_RayLength 1`, `:3495-3497`). The deployed ONNX (`results/012/Prosthetic.onnx`) confirms the layout:
`obs_0` [16, 13] (tokens), `obs_1`-`obs_10` nine × [3] and one × [2] (ray sensors: tag one-hot, miss flag, distance), `obs_11` [24]
(vector). **The deployed input layout is therefore 24 + 16×13 + 29 ray floats, not 24 + 16×13.** `CheckpointTheater.cs:92-108`
guards only the 24 and the 16 × 13. A 0.5 m sphere cast over 20 m almost always hits the object, so these inputs are close
to constant today; what they contribute is unknown (risk R3 below). The rays depend on the object's **tag** and collider, not
on its shape parameters.

Actions: 19 continuous (`ArmGraspAgent.cs:150-152`; scene `:1732`): 14 finger setpoint rates (`:553-560`), shoulder flexion /
abduction / elbow velocities (`:537-543`), wrist flexion / pronation delta targets (`:544-550`). Nothing object-specific.

### Task logic

| item | where | current behaviour | shape assumption |
|---|---|---|---|
| grasp point | `ArmGraspAgent.cs:76-77`, `:146-148`; scene `:1609` | palm-frame offset (0.0504, 0.0774, 0.0281), x scaled by `FingerLengthRatio`; `GraspPointDistance` = distance to the **object centre** | the offset is the centre of the region the closed fingers enclose, sized for the 50 mm cylinder (see `HandSpec.oppositionTarget`, `ArticulatedHand.cs:58-59`, "outer surface of a 5 cm cylinder resting on the palm") |
| contact detection | `ArticulatedHand.cs:416`, `:547-560` | `LinkContact` on every link; `OnCollisionEnter/Stay`; self-contacts dropped (`:552`); **tag filter `Cylinder`** (`:553`); finger groups, palm and forearm reported, bicep excluded | tag |
| contact gate | `ArmGraspAgent.cs:565-580`, `:616-617`; scene `:1604`, `:1611-1613` | contact = separation ≤ `contactDistance` 0.001; gate = ≥ 6 touching finger groups, ≥ 2 distinct non-thumb fingers, thumb touching; palm and forearm never count | none |
| gate transition | `ArmGraspAgent.cs:627-634` | phase Lift, `m_TransitionStep`, +0.1, `ReleaseObject()` (dynamic, gravity on, constraints cleared) | none |
| lift | `ArmGraspAgent.cs:90-91`, `:686` | `bounds.min.y ≥ m_PlatformTop + liftClearance (0.03)` on the world AABB | generic, tilt-aware |
| drop | `ArmGraspAgent.cs:103-106`, `:687-693` | centre farther than `dropDistance` 0.11 from `GraspPoint`; **`Angle(transform.up, m_ObjectUp0) > dropTiltDeg 60`** (`m_ObjectUp0` captured at release, `:681`); `bounds.min.y < platformTop − 0.007` | **tilt test treats local Y as the object axis** |
| hold and pulses | `ArmGraspAgent.cs:108-114`, `:646-656`, `:695-722` | `HoldStepsNeeded = K · DecisionPeriod`; on each Lift→Hold entry three pulses scheduled at hold-step indices in [1, holdNeeded − 5]; force = horizontal unit direction × `scale · 1.5 · m · g · U[0.5, 1]`, `AddForce` at the centre of mass; torque = `onUnitSphere · mag · 0.054`, a pure couple; 5 steps each; falling out of the lift clears the schedule | force at the centre of mass, torque lever a constant; no axis or shape used |
| shaping | `ArmGraspAgent.cs:588-602`, `:857-861`; scene `:1608` | potential-based, normalised: 14 segment-to-object distances via `Collider.ClosestPoint` both ways, plus grasp-point-to-**centre** distance; floor 0.018; scale 1/15 | `ClosestPoint` is generic; the centre term assumes a compact object |
| penalties / bonus | `ArmGraspAgent.cs:608`, `:632`, `:648`, `:652`, `:694` | existential −1/MaxStep, effort −2e-5·Σa², safety −1e-4·limit events, phase +0.1, hold +0.004 × 50, success +1, fail −0.2 | none |
| quality terms | `ArmGraspAgent.cs:254-259` | all zero (run-010 compatibility stubs) | – |
| penetration statistic | `ArmGraspAgent.cs:610-613` | max `ComputePenetration` of the 14 segment colliders against `cylinderCollider`, once per step (logged as `maxPenMm`; not a reward term) | generic |
| contact azimuth / height | `ArmGraspAgent.cs:661-669` (`MakeSample`) | azimuth and height about `cylinderTransform.up` | **cylinder axis** (diagnostics only) |

### Diagnostics that read object geometry

| where | what |
|---|---|
| `Assets/Scripts/MorphologyWatchOverlay.cs:45-51`, `:65-73` | nearest `Cylinder`-tagged object; fingertip touch by `ComputePenetration` / closest-point gap; no radius or height |
| `Assets/Scripts/Diagnostics/PhysicsViewOverlay.cs:70-76` | draws a `PrimitiveType.Cylinder` parented to the object at scale 1.02: **assumes a scaled unit cylinder** |
| `ArticulatedGates.cs:109`, `:275`, `:291`, `:341-356`, `:495`, `:538`, `:112-118` | `objRadius` from the AABB, `halfH`, tilt vs world up, placement, penetration; eval CSV columns documented in `results/lineage/LINEAGE_011b_012.md` and the header at `:251` |
| `BoEvalHarness.cs:333-339`, `:362`, `:379-403` | `ContactHeight` along `cyl.up`; logs `cylYaw`; drop test splits displacement into axial (along `cyl.up`) and lateral |
| `Assets/Scripts/Obj_Reset.cs` | `ResetPosition` helper; its GUID appears in no scene or prefab (unused) |

### Reset order and timing

`OnEpisodeBegin` (`ArmGraspAgent.cs:364-416`): log an unfinished episode as `maxStep` → `BeforeEpisodeBegin` hook → counters →
**mass draw** (`:374-376`) → perturbation scale → object made kinematic with the new mass (`:378`) → `m_Morph.ApplyForEpisode()`
(theta sample, `MorphologyManager.cs:158`, then `rig.Rebuild` regenerating the skeleton, `:162` → `:251`) → span / length ratios →
drives → **`SpawnCylinder()`** (`:393`) → initial shaping distances (`:395-403`) → K and `TaskBudgetSteps` (`:414-415`).
DecisionPeriod 10, `TakeActionsBetweenDecisions` (`Dynamic_Scene.unity:1526-1528`); `fixedTimestep 0.01` set at runtime
(`ArticulatedHand.cs:128`, `:195`); `MaxStep 5000` (50 s). The hand skeleton is already destroyed and rebuilt every episode, which
sets the scale for what a per-episode object rebuild would cost.

## 3b. Superquadric option

**Parameterization.** Implicit surface `((|x/a1|^(2/ε2) + |y/a2|^(2/ε2))^(ε2/ε1) + |z/a3|^(2/ε1)) = 1`, with `z` the long axis
(mapped to the object's local Y so the existing tilt test keeps its meaning). `a1, a2` are the cross-section semi-axes, `a3` the
half-length, `ε2` the cross-section shape (1 = circle, → 0 = square, 2 = diamond), `ε1` the axial profile (1 = ellipsoidal ends,
→ 0 = flat ends and straight sides, 2 = pinched). The current object is `a1 = a2 = 25 mm, a3 = 140 mm, ε2 = 1, ε1 → 0`
(a cylinder is the ε1 → 0 limit; ε1 = 0.1-0.2 is the practical stand-in). Superellipsoids are convex for `ε1, ε2 ∈ (0, 2]`
(the superellipse `|x|^p + |y|^p = 1` is convex for `p = 2/ε ≥ 1`), so restricting both ε to `(0, 1]` keeps every object convex,
which is what a convex MeshCollider represents exactly and what the forced-close feasibility logic assumes.

**Proposed ranges, sized to the hand.** Hand length 194 mm (`ArticulatedHand.Anthropometrics()`, `ArticulatedHand.cs:389-390`;
`results/011_artic2/handspec_reference.txt:9`), palm width 85 mm, finger chains 64-86 mm, thumb 97 mm, fingertip radius 7.5 mm,
palm thickness 28 mm; the thumb's opposition geometry is built around a 50 mm object on the palm (`ArticulatedHand.cs:54-59`).
A three-phalanx chain of length L wraps roughly a half-circumference of radius `L/π ≈ 20-27 mm`, which is why the 50 mm cylinder
is a comfortable power grasp and why 80 mm is near the limit of an enveloping grasp for the reference hand; the length scales
0.8-1.2 move that limit by ±20 %.

| parameter | cylinder today | proposed training range | note |
|---|---|---|---|
| a1, a2 (cross-section semi-axes) | 25, 25 mm | 15-40 mm each, aspect a1/a2 ∈ [0.5, 2] | 30 mm diameter = pinch-only for most hands; 80 mm = envelope limit |
| a3 (half-length) | 140 mm | 40-140 mm | short objects (a3 < 60 mm) become pucks / spheres; the 60° tilt rule stops meaning "slipped" |
| ε2 (cross-section) | 1 | 0.3-1.0 | 0.3 is a rounded square: flat faces for the palm and pads |
| ε1 (axial profile) | ≈ 0.1 | 0.2-1.0 | 1.0 = ellipsoid: no flat end to rest on |
| mass | log-uniform 0.2-1.5 kg | see below | |

Sub-families this covers: cylinders and prisms (ε1 small), boxes (both small), ellipsoids / spheres (both 1), capsules
(ε2 = 1, ε1 ≈ 0.5-0.8 and a3 > a1). Cones, handles and non-convex objects are outside the family.

**Mesh generation at spawn.** Parametric sampling `x = a1 · c(η, ε1) · c(ω, ε2)`, `y = a2 · c(η, ε1) · s(ω, ε2)`, `z = a3 · s(η, ε1)`
with the signed-power functions `c(θ, ε) = sign(cos θ)|cos θ|^ε`, `s` likewise, on a grid of `nω × nη`. The visual mesh can be
dense (32 × 16 = 512 vertices); the collider cannot: PhysX convex hulls are limited to **255 vertices** (and 255 faces), and Unity's
cooking (`m_CookingOptions 30` = mesh cleaning, weld, cook-for-faster-simulation, fast midphase) quantizes larger inputs, which
distorts small ε shapes unpredictably. Generate the hull directly under the limit: `nω = 20, nη = 10` gives 200 vertices before
welding the poles; for ε1 → 0 many samples collapse onto the flat ends and weld away. The existing object is itself a faceted
prism (Unity's cylinder mesh), so faceting is not new.

**Collider choice.**
- *Convex MeshCollider per shape*: exact for the family, works with every existing code path (`ClosestPoint`, `ComputePenetration`,
  AABB lift / drop tests, ray sensors, TGS contacts). Cost: cooking on assignment, on the main thread.
- *Compound primitives* (stacked boxes / capsules / spheres): no cooking, analytic contacts, but cannot represent rounded-square
  cross-sections or ellipsoidal ends without many parts, and `ClosestPoint` / `ComputePenetration` only accept a single collider
  (the shaping and penetration code would need to iterate parts). Not a good fit for this family.
- *Two-level*: convex hull for physics, dense mesh for the renderer and overlays (what the scene does today with the built-in mesh).

**Inertia.** PhysX computes the inertia tensor and the centre of mass from the convex collider under uniform density whenever they
are not set (they are not, `ArmGraspAgent.cs:378`, `:678-680`), so a per-shape inertia comes for free once `rb.mass` is set;
`ResetInertiaTensor()` after swapping the collider guarantees the recompute.

**Mass.** Three choices:
1. *Independent draw, unchanged* (log-uniform 0.2-1.5 kg; env `mass/min`, `mass/max`; observation normaliser untouched).
   Simplest; produces unphysical densities at the extremes (a 30 mm sphere at 1.5 kg is ~100,000 kg/m³; the 50 × 280 mm cylinder
   spans 360-2,700 kg/m³ today).
2. *Density × volume*: draw density log-uniform in, say, 200-2,000 kg/m³ (foam-to-dense-plastic; the cylinder's current band),
   mass = ρ · V(a1, a2, a3, ε1, ε2) (closed form with Beta functions, or from the hull volume). Mass then correlates with size,
   which is what a real object bank looks like and gives the policy a size cue for mass. Needs a new normaliser range for
   `m_MassObs` (mass can now leave 0.2-1.5 kg: a 80 × 80 × 280 mm box at 2,000 kg/m³ is 3.6 kg) or a clamp on the product.
3. *Independent draw clamped to a density band*: rejection-sample mass until ρ ∈ [ρmin, ρmax]. Keeps the observation
   semantics of (1) while removing the absurd corner; small objects lose the heavy tail, which is also the tail that broke the
   hard hands in the probe.

**Throughput.** Two implementation paths:
- *Per-episode generation*: build vertices (200 × float3, microseconds), create a `Mesh`, assign to `MeshCollider.sharedMesh` with
  convex on (cooking, main thread; order of 0.1-1 ms for ≤ 255 vertices is the expectation, unmeasured here), destroy the previous
  `Mesh` to avoid leaking (Unity does not garbage-collect `Mesh` objects). Against the per-episode rig rebuild that already
  destroys and recreates ~20 articulation links and colliders, this is a small addition; the headless throughput of run 012
  (~400 steps/s per environment, episodes of 300-600 steps) would lose well under 1 % if cooking stays under 1 ms.
- *Pre-cooked bank*: generate N shapes at `Initialize` (or bake them as assets), pick one per episode. No per-episode cooking,
  deterministic bank indices (a natural held-out split, see 3d), N = 500-2,000 covers a 5-D box finely enough for training. Costs
  N × (mesh + hull) in memory (~50 KB each) and a fixed bank means the "shape distribution" is a finite set, which the BO objective
  can then average exactly.
The PhysX scene persistence across episodes (`LINEAGE_011b_012.md`, instrument definition) already makes single episodes
non-reproducible; swapping collider meshes per episode does not change that property.

**What each 3a item needs** (unchanged / parameter change / logic change):

| item | need | why |
|---|---|---|
| tag lookup, `Collider` / `Rigidbody` caching | unchanged | generic types |
| prefab visual mesh + scene MeshCollider mesh | logic change | mesh and collider set from the sampled shape; the scene's built-in-cylinder references become the default only |
| `m_CylHalfHeight` at Initialize | logic change | must be recomputed per episode from the spawned shape's rest pose (`bounds`), not once |
| mass draw and `m_MassObs` | unchanged (choice 1) / parameter change (choice 3) / logic change (choice 2) | see Mass |
| centre of mass / inertia | unchanged | derived from the collider; add `ResetInertiaTensor()` after the swap |
| friction material | unchanged | assigned to whatever collider is present; the harness override mutates the live material (fine) |
| platform | unchanged | slab |
| rest height `restY` | logic change | rest on the shape's lowest point in its spawn orientation (AABB min), not `halfHeight` |
| yaw about world Y | parameter change → logic change | fine while the long axis is vertical; a lying pose or random orientation needs a full rotation draw and a stable-rest check |
| spawn overlap rejection | unchanged | `ComputePenetration` is generic |
| vector obs 0-2 (centre), 7 (mass) | unchanged | centre stays meaningful |
| token [12] centre distance | unchanged (option i) / unchanged (ii) / logic change (iii) | a surface distance would be better for a 40 mm vs 15 mm radius but is not required |
| ray sensors | unchanged | tag and collider based |
| grasp point offset | parameter change (per-size scaling) or logic change | the offset is a fixed 50 mm-object grasp centre; an 80 mm object wants the point farther out along the palm normal; simplest: scale x by (a_eff / 25 mm) as it already scales by the finger-length ratio |
| contact gate | unchanged | count and thumb rule are shape-free; whether 6 segments is attainable on a 30 mm sphere is a task question, not a code one |
| lift / drop-below-platform | unchanged | AABB based |
| drop tilt | logic change | disable or scale the 60° rule when `a3 / max(a1, a2) < ~1.5` (a puck or sphere rolling in the hand is not a drop); or replace by the displacement of a body-fixed point |
| hold / pulses | unchanged | centre-of-mass force, couple torque; the `perturbTorqueLever` 0.054 m could be tied to the object's size (parameter) |
| shaping | unchanged | `ClosestPoint` handles any convex collider; the centre term is fine for compact convex shapes |
| penetration statistic | unchanged | generic |
| `MakeSample` azimuth / height, BoEval axial / lateral split | logic change if kept | cylinder-axis diagnostics; drop or redefine per shape |
| `PhysicsViewOverlay` | logic change | draw the actual collider hull instead of a unit cylinder |
| `ArticulatedGates` scripted placement (`objRadius`, `halfH`) | logic change if scripted modes are used with new shapes | eval mode itself does not place the object |
| `CheckpointTheater` guard | unchanged (i) / parameter change (ii, iii) | vector size and buffer shape |
| BoEval player build | unchanged for (i) except its 22-obs logging; (ii, iii) rebuild | the build is kinematic-era and not the current instrument anyway |

## 3c. Observation design: three options

Current deployed layout (from the ONNX): vector 24 + JointTokens 16 × 13 + ten ray sensors (29 floats) → 19 actions.

**(i) No shape observation (contact-only).** The policy sees the object centre, its mass, the contact count, the per-token
centre distances and the ray hits, and must infer the shape from what its fingers meet.
- Pros: keeps the exact input layout, so every consumer stays valid: the `Dynamic_Scene` BehaviorParameters, the CheckpointTheater
  guard, the BoEval build and, above all, any Editor / Quest deployment that loads a `Prosthetic.onnx` by shape. A run-012 warm
  start (`init_path`) is possible because the network shapes match. Closest to a prosthesis without vision (the controller
  knows only its own state and contacts).
- Cons: partial observability. The network has no recurrence (`memory_size` 0 in the export), so shape must be read from the
  current step's contacts and the centre distance pattern; the grasp-approach (pre-contact) cannot adapt to a 30 mm vs 80 mm
  object except through the ray sensors, whose 0.5 m sphere cast makes them nearly uninformative. Expect slower learning and a
  cap on the shape range that works.

**(ii) Superquadric parameters in the vector.** Append `a1, a2, a3` (normalised by, say, 0.14 m), `ε1, ε2` and a size scale or
the volume-derived density: 24 → 29-30 floats.
- Pros: cheapest informative option; five numbers fully describe the family; it matches the repo's perception direction (the
  upstream `superquadric-perception` branch, `Assets/SuperquadricClient.cs`, `superquadric_fitter.py` fit exactly these parameters
  from a depth view), so the trained policy's input is what the AR front end would produce. Lets the BO objective ask per-shape
  questions later.
- Cons: **breaks the input layout**: the scene's `VectorObservationSize`, the CheckpointTheater guard (hard-coded 24), the
  BoEval player, and the Editor / Quest deployment path all need the new size and a re-exported model; no run-012 warm start
  (first-layer shape change; a partial weight copy is possible but is new tooling). Privileged information relative to a
  contact-only controller; the fitted parameters at deployment carry perception error the training never saw (a noise
  augmentation would be the usual answer, itself a design choice).

**(iii) A small point set.** 16-32 surface points of the object in the palm frame, either as a fixed 48-96-float vector block or as
a second BufferSensor (variable count, attention already exists in the network).
- Pros: shape-family agnostic (a later non-superquadric bank needs no observation change); directly encodes where the surface is
  relative to the fingers, which is what the approach needs; a second buffer plays to the existing attention architecture.
- Cons: the largest layout change (a new sensor component and a new ONNX input, so it breaks every deployment consumer like (ii)
  and adds a sensor to the scene); point sampling must be deterministic and pose-consistent (fixed parametric (η, ω) grid, not
  random); more input dimensions per step for the same policy size; at deployment the AR side would have to provide a point set
  (a depth-camera partial cloud is one-sided; a fitted superquadric resampled to points is the workable route, which folds back
  into (ii)).

Which keeps the deployment path: **only (i)**. (ii) is the smallest break, and it is the one aligned with the perception branch.

## 3d. Curriculum and evaluation

**How object families enter training.**
- *All at once* (full domain randomization from step 0, fresh lineage as in runs 010 and 012): simplest, no lesson design, one
  run; risk that the wide family slows the reach-and-grasp acquisition that run 012 completed in its first ~2 M steps, or that the
  policy settles on a pinch that only works for small objects.
- *Family curriculum* through environment parameters (the mechanism used for `hold/decisions` and `perturb/scale`): lesson 0 = the
  cylinder (the deployed task), then widen `ε` and the aspect ranges by lessons gated on success ≥ 0.9. Keeps the early curve on
  known ground and gives a per-lesson diagnostic; costs lesson thresholds that stall when a family is genuinely hard (run 011's
  budget lesson is the precedent) and it produces a policy whose training distribution depends on when lessons advanced.
- *Warm start from run 012*: only with option (i); with (ii) / (iii) the input layer changes. A warm start biases toward the
  cylinder grasp; whether that helps or hinders is an empirical question (the run-009 → 010 experience: fresh lineage on a wider
  distribution lost the palm contact that the fine-tuned lineage had).

**What "held-out" means once objects are randomized.** Two independent axes:
- *held-out seeds* over the training shape distribution (the existing instrument; measures in-distribution generalization);
- *held-out shapes*: a bank split (shape indices never seen in training) or excluded parameter sub-regions (e.g. train ε2 ∈ [0.3, 0.4] ∪ [0.6, 1.0], test [0.4, 0.6]); measures interpolation, and excluded corners (largest a1 with smallest ε2) measure extrapolation.
With a pre-cooked bank both are one line of bookkeeping; with per-episode generation the sub-region rule is the only option.
Seed bookkeeping: blocks 1001-3100, 4001-4100, 5001-5100, 6001-6100 and 7001-7300 are spent; 8001-8300 is earmarked for the BO
confirmation (`tools/bo/confirm.py`); a 013 evaluation would start a new block (9001+). Evaluating 012 on the new object bank
(with option (i), nothing prevents it) gives the baseline the way 011b did for 012.

**What the BO objective becomes.**
- *Mean over an object bank*: one scalar per theta, the form `tools/bo/campaign.py` already optimizes; averages away the
  interesting structure (a hand that is perfect on cylinders and useless on 80 mm boxes can score the same as a uniform 0.8).
- *Worst family* (min over families of per-family success): the robust-design objective; needs enough episodes per family for
  the min to be measured (the Wilson half-width at n = 100 is ±0.05-0.1, so a min over 5 families at 100 episodes each is a
  noisy statistic; 500 episodes per theta is 5 × the probe's cost, about 4 minutes per theta on the Editor instrument).
- *Per-family vector*: multi-objective (Pareto over families, or a scalarization with weights chosen for the prosthesis use
  case); the GP in `tools/bo/gp.py` is scalar, so this is new optimizer code.
Whatever the choice, the pass-level noise measured in `HARD_HANDS.md` (the hand-14 baseline moved from 0.57 to 0.64 between
Editor sessions with identical seeds) sets the resolution: objective differences under ~0.1 at n = 100 are not decisions.

## 3e. Risks and unknowns, each with the cheapest resolving test

| # | risk / unknown | cheapest test |
|---|---|---|
| R1 | convex cooking per episode is slow enough to matter, or leaks `Mesh` objects | Editor script: cook 1,000 random 200-vertex hulls in a loop, time it and watch `Mesh` count in the Memory Profiler (10 min) |
| R2 | TGS contact stability was tuned on a cylinder (`results/011_artic2/contact_solver_survey.md`, penetration 2-3 mm); sharp edges (ε2 = 0.3) and flat faces may show larger penetration or jitter | run the existing G6 push / penetration harness (`ArticulatedGates` scripted modes) on a rounded box of the cylinder's size |
| R3 | the ten ray sensors are near-constant today (0.5 m sphere cast, 20 m range); with varied shapes they may become informative in an uncontrolled way, or they may already be dead inputs | log the ten ray outputs over 200 steps of the deployed policy on the cylinder (are they constant?); then the same on a 30 mm sphere |
| R4 | the 60° tilt rule mislabels a rolling puck or sphere as a drop, and fails to catch a slipping long object that stays upright | analytic: enumerate the family's aspect ratios; decide the rule per aspect class before training, and check the drop-reason distribution on a 100-episode scripted release |
| R5 | resting pose: an ellipsoid or a short prism does not rest upright; the object is kinematic until the gate so it cannot topple before release, but after release it may already be unstable on the slab | script: release each shape without a grasp and record whether it topples within 1 s; that decides between an upright-only family, a lying pose, or a stable-pose search at spawn |
| R6 | the fixed grasp-point offset (50 mm object) puts the target inside an 80 mm object or too far out for a 30 mm one, and the shaping centre term pulls the palm through small objects | forced-close placement across sizes with the reference hand (the Part A oracle; the articulated version in `tools/bo/feasibility.py` rejects even the reference hand today, so the oracle needs work first: an unknown of its own) |
| R7 | partial observability under option (i) caps the workable shape range; option (ii) leaks perception-free information | two 2 M-step smokes, (i) vs (ii), same shape bank, compare grasp-gate rate per family (about 3 h each on 8 headless environments) |
| R8 | mass-size coupling: an independent mass makes tiny heavy objects and large light ones; density coupling changes the mass observation range and the hard-hand heavy tail | decide by inspection of the density table for the proposed ranges; if density coupling is chosen, one env-parameter probe of the `m_MassObs` normaliser limits |
| R9 | throughput of the headless build with per-episode meshes (GC from `Mesh` creation) | 10 k-step smoke through `mlagents-learn` with the existing throughput probe config (`Config/throughput_probe_011.yaml`) |
| R10 | the hard-hand result (a masked thumb base is the one large theta effect) will dominate any object-shape objective too, because `MorphologyManager.Sample` lets the thumb base be masked with only the thumb IP active (16 % of draws) | none needed for a decision; it is a training-distribution question (require the thumb base active, or keep it and accept that BO will first rediscover the thumb) |
| R11 | pass-level chaos: the instrument reproduces passes within a session but the hand-14 baseline moved 0.57 → 0.64 across sessions; an object bank adds more state to the persistent PhysX scene | measure: three sessions of the same shape-bank pass for one theta before any BO on 013 |
| R12 | the AR perception side (superquadric fitter) produces parameters with error and latency the policy never saw | out of scope for the simulation design; note that option (ii) is the only one where this mismatch can be modelled by input noise |

Decision points left open on purpose: shape ranges (table in 3b), mass coupling (1 / 2 / 3), collider path (per-episode vs bank),
observation option (i / ii / iii), curriculum (all-at-once / family lessons / warm start), held-out definition, BO objective form,
and whether the thumb-base mask stays in the training distribution.
