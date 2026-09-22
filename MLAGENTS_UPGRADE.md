# ML-Agents 2.0.2 → 3.0.0 Upgrade — Agent Script Audit

**Project:** Augmented-Reality-Artificial-Intelligence
**Package:** `com.unity.ml-agents` — now `3.0.0` (confirmed in `Packages/manifest.json`)
**Scope:** every `.cs` file in `Assets/Scripts/` that inherits from `Agent`
**Constraint honored:** no `.unity` / `.prefab` files touched; no game logic, reward
values, or observations changed.
**Date:** 2026-06-19

---

## Agent scripts found

`grep ": Agent"` over `Assets/Scripts/` returns exactly three subclasses:

1. `Assets/Scripts/ArmGraspAgent.cs`
2. `Assets/Scripts/ArmGraspAgentCopy.cs`
3. `Assets/Scripts/CSVArmGraspAgent.cs`

---

## Breaking-change checklist (from the task) vs. actual code

Each script was scanned for every deprecated pattern in the upgrade notes. **None were
present** — all three scripts were already written against the modern (2.0+/3.0.0)
ML-Agents API, so no API rewrites were necessary.

| Old API (pre-3.0.0) | New API (3.0.0) | Found in scripts? | Action |
|---|---|---|---|
| `using MLAgents` | `using Unity.MLAgents` | No — already `using Unity.MLAgents;` | none |
| (missing) | `using Unity.MLAgents.Actuators` for `ActionBuffers` | Already present | none |
| (missing) | `using Unity.MLAgents.Sensors` for `VectorSensor` | Already present | none |
| `OnActionReceived(float[] vectorAction)` | `OnActionReceived(ActionBuffers actions)` | Already `ActionBuffers` | none |
| `vectorAction[i]` | `actions.ContinuousActions[i]` | Already `actions.ContinuousActions` | none |
| `CollectObservations()` | `CollectObservations(VectorSensor sensor)` | Already takes `VectorSensor` | none |
| `AddVectorObs(...)` | `sensor.AddObservation(...)` | Already `sensor.AddObservation` | none |
| `GiveLikelyRewardForPotentialAction(...)` | `AddReward(...)` | Not used anywhere | none |
| `AgentReset()` | `OnEpisodeBegin()` | Already `OnEpisodeBegin()` | none |
| `Done()` | `EndEpisode()` | Neither called in any script | none |

Verification command (all patterns, zero matches):

```
grep -nE "AddVectorObs|AgentReset|\bDone\s*\(|GiveLikelyRewardForPotentialAction|using MLAgents|OnActionReceived\s*\(\s*float|CollectObservations\s*\(\s*\)|vectorAction" Assets/Scripts/
→ no matches
```

---

## Changes actually made

No API call was rewritten, because none of the deprecated patterns existed.

The only edits were a **cleanup of three stale comments**: a previous pass (under an
earlier, now-superseded task that assumed 2.0.2 was still installed) had added a
`// TODO(fix):` block above the `using Unity.MLAgents;` line in each of the three Agent
scripts, warning that 2.0.2 + Barracuda would not compile under Unity 6. With 3.0.0 now
installed (Barracuda replaced by Sentis), those comments were factually wrong and have
been removed.

| File | Edit |
|------|------|
| `Assets/Scripts/ArmGraspAgent.cs` | Removed stale 5-line `TODO(fix)` comment above `using Unity.MLAgents;` |
| `Assets/Scripts/ArmGraspAgentCopy.cs` | Removed stale 5-line `TODO(fix)` comment above `using Unity.MLAgents;` |
| `Assets/Scripts/CSVArmGraspAgent.cs` | Removed stale 5-line `TODO(fix)` comment above `using Unity.MLAgents;` |

Per-script confirmation of the resulting 3.0.0-correct surface:

- **ArmGraspAgent.cs** — `Initialize`, `OnEpisodeBegin`, `CollectObservations(VectorSensor)`,
  `OnActionReceived(ActionBuffers)` using `actions.ContinuousActions`, reward via `AddReward`. ✅
- **ArmGraspAgentCopy.cs** — same override set; contact rewards via `AddReward`. ✅
- **CSVArmGraspAgent.cs** — same override set; `OnCollisionStay`/`OnCollisionExit`
  reward handling via `AddReward`. ✅

---

## Result

All three `Agent` scripts are API-compatible with `com.unity.ml-agents` 3.0.0 with no
code-logic changes. After Unity reimports the upgraded package and recompiles, these
scripts should build clean.

> Note: the API-mapping list in the task (e.g. `AddVectorObs`, `AgentReset`, `Done`) is
> the **0.x/1.x → 2.0** migration. Those signatures were already adopted in this codebase,
> and the 2.0 → 3.0 jump did not further change the C# `Agent` surface (it swapped the
> Barracuda inference backend for Sentis), so the scripts carry over unchanged.

### Housekeeping
`COMPILE_ERRORS.md` and `MIGRATION_FIXES.md` (written under the earlier task, stating that
2.0.2 was installed) were stale once 3.0.0 landed and have been **deleted**.

---

## Addendum (2026-08-31): 3.0.0 → 4.1.0

3.0.0 did **not** compile on Unity 6000.3.18f1 after all. Unity 6000.3 no longer ships a real
`com.unity.sentis`: the built-in `com.unity.sentis@2.2.0` is a `"type": "shim"` that only
depends on `com.unity.ai.inference` (Inference Engine, namespace `Unity.InferenceEngine`). The
manifest pin `"com.unity.sentis": "2.1.1"` was overridden by that built-in shim, so ML-Agents
3.0.0's `using Unity.Sentis` produced 657 `CS0234`/`CS0246` errors (`Tensor`, `TensorShape`,
`Model`, `ModelAsset`, `BackendType` not found) inside the package itself. A project in that
state also hung the Editor at the "Opening project…" splash (Burst: `Failed to resolve assembly
'Assembly-CSharp'`).

Fix applied to `Packages/manifest.json`:
- `com.unity.ml-agents`: `3.0.0` → `4.1.0` (targets `com.unity.ai.inference` 2.6.1, which
  Unity 6000.3 already resolves)
- removed the dead `com.unity.sentis` pin

Result: `recompile_status` → `completed, failed=false, errors=[]`; 0 `error CS` lines in
Editor.log; `Assembly-CSharp.dll` built. The three `Agent` scripts needed no changes — 4.x kept
the `Agent`/`ActionBuffers`/`VectorSensor` surface and only swapped the inference backend
(4.0.0 also raised the minimum Editor to 6000.0 and merged `com.unity.ml-agents.extensions`
into the main package). Update the Python `mlagents` trainer to the release that pairs with
4.1.0 before training again.

---

## Addendum (2026-08-31): Python trainer for package 4.1.0

There is **no PyPI `mlagents` release that pairs with Unity package 4.x** — PyPI's latest is the
release-22 trainer (pairs with package 3.0.0), and the package's Installation page still says
`mlagents==1.1.0`, which is stale. Do **not** `pip install mlagents`.

The 4.1.0 trainer is source-only from the `develop` branch (latest tag `release_23` = package
4.0.0). `setup.py` pins `python_requires=">=3.10.1,<=3.10.12"` and `torch>=2.1.1,<=2.8.0`.

Installed on this machine:
- Python **3.10.11** via `winget install Python.Python.3.10` (system 3.14 / 3.12 will not install it).
  Available as `py -3.10`.
- Clone: `C:\Users\chris\ml-agents` (`develop` @ `3ecb446`, `--depth 1`).
- Venv: `C:\Users\chris\ml-agents\venv` — `torch 2.8.0+cpu`, then
  `pip install ./ml-agents-envs` and `pip install ./ml-agents`.
- `pip show mlagents` → **1.2.0.dev0** (mlagents-envs 1.2.0.dev0, Communicator API 1.5.0).

Smoke run (verified):
```
C:\Users\chris\ml-agents\venv\Scripts\mlagents-learn.exe config\trainer_config.yaml --run-id=<id>
```
then Play in the Editor (`Dynamic_Scene`). Trainer log:
`Connected to Unity environment with package version 4.1.0 and communication version 1.5.0`,
`Connected new brain: Prosthetic?team=0`, then
`Prosthetic. Step: 10000. Time Elapsed: 67.5 s. Mean Reward: 0.543 ... Training.`
Unity console: `Registered Communicator in Agent`. `config/trainer_config.yaml` needed no changes.

To run from a fresh shell: `C:\Users\chris\ml-agents\venv\Scripts\activate` (or call the exe by
full path as above). Update the trainer with `git -C C:\Users\chris\ml-agents pull` followed by
re-running the two `pip install ./…` commands.

Fallback (not needed): the `release_23` branch trainer would also handshake with package 4.1.0
(same Communicator API 1.5.0) but lacks the gymnasium / LSTM-SAC fixes on `develop`.

### GPU (CUDA) benchmark — 2026-08-31

A second venv `C:\Users\chris\ml-agents\venv-cuda` has `torch 2.8.0+cu126` (RTX 3090 detected,
`torch.cuda.is_available() == True`) plus the same `mlagents 1.2.0.dev0`. Fresh runs from the
Editor (`Dynamic_Scene`, same `trainer_config.yaml`, back-to-back, run-ids `bench_cpu` / `bench_cuda`):

| device | Step 10000 | Step 20000 | steady-state per 10k |
|--------|-----------:|-----------:|---------------------:|
| `--torch-device cpu`  | 53.3 s | 94.8 s  | ~41.5 s |
| `--torch-device cuda` | 89.3 s | 164.1 s | ~74.9 s |

**CUDA is ~1.7–1.8× slower** for this project. GPU was in use (36 % util, ~1 GB VRAM), so it is
the expected outcome for a small network (2×128 + LSTM 128) on vector observations: the per-step
CPU↔GPU transfer overhead outweighs any compute gain, and the real bottleneck is Unity stepping
physics. **Train on CPU** (`venv`). Keep `venv-cuda` only for experiments with visual observations
or much larger networks; otherwise it can be deleted. The larger speedups available are on the Unity
side: a standalone build with `--env <exe> --num-envs N`, and `--time-scale`.

Note: ML-Agents exits Play mode in the Editor automatically when the trainer disconnects.

### Parallel headless environments (standalone build) — 2026-08-31

Built `Dynamic_Scene` as a Windows player: `Builds\Prosthetic\Prosthetic.exe` (StandaloneWindows64,
0 errors; `/Builds/` is gitignored — rebuild with the `unity` MCP `build` tool or File ▸ Build after
scene/script changes). To make headless instances start cleanly, **Initialize XR on Startup** was
turned off for the Standalone group in `Assets/XR/XRGeneralSettings.asset` (`m_InitManagerOnStart: 0`;
its only Standalone loader was the deprecated Windows MR loader). Re-enable if a PC-VR standalone
build is ever needed.

Benchmarks (same `trainer_config.yaml`, i9-12900F 16C/24T, steady-state seconds per 10k steps):

| setup | per 10k steps | vs Editor |
|-------|--------------:|----------:|
| Editor, CPU torch                     | ~41.5 s | 1.0× |
| Editor, CUDA torch                    | ~74.9 s | 0.55× |
| build, `--num-envs 8`, CPU torch      | **~12.3 s** (10k @ 27.0 s, 30k @ 54.7 s) | **~3.4×** |
| build, `--num-envs 8`, CUDA torch     | ~34 s (30k @ 109.5 s) | 1.2× |
| build, `--num-envs 16`, CPU torch     | ~16 s (30k @ 57.6 s) | ~2.6× |

Conclusions: CPU torch + 8 headless envs is the fastest configuration; more envs don't help (the
single-threaded PPO update becomes the bottleneck); CUDA loses in every configuration for this
network. **Standard training command** (cmd or PowerShell, from the repo root):

```
C:\Users\chris\ml-agents\venv\Scripts\mlagents-learn.exe config\trainer_config.yaml --run-id=<id> --env Builds\Prosthetic\Prosthetic.exe --num-envs 8 --no-graphics
```
No Play button needed — the trainer launches the 8 players itself. Add `--resume` to continue a run.
Note: with `--num-envs` the Editor is not involved, so scene/script changes require a rebuild first.

### Watching a trained model in the Editor (inference) — 2026-09-01

**Bug fixed:** pressing Play with no trainer listening puts the agent in inference mode, and it threw
`InvalidOperationException: Tensor data cannot be read from, use .ReadbackAndClone()` every step. Cause:
`ArmAnimation ▸ Behavior Parameters ▸ Inference Device` was **ComputeShader** (GPU backend) and
ML-Agents 4.1.0 indexes tensors directly (`ApplierImpl.cs:47`, `ObservationWriter.cs:193`), which the
Inference Engine only allows for CPU tensors. Fix: Inference Device → **Default** (CPU/Burst) on both
`BehaviorParameters` in `Dynamic_Scene` (saved). Verified: 510 agent steps / 10 s, 0 errors, arm animates.

**Training does not update the Editor's model by itself.** The scene references the static asset
`Assets/Models/Prosthetic.onnx`; training writes `results\<run-id>\Prosthetic.onnx` (at each checkpoint —
`checkpoint_interval: 500000` — and when training stops with Ctrl+C). To make the Editor use a trained policy:

```
tools\deploy_model.cmd <run-id>
```
copies `results\<run-id>\Prosthetic.onnx` → `Assets/Models/Prosthetic.onnx` (previous kept as `.onnx.bak`,
gitignored), Unity reimports, then press Play to watch. Verified end-to-end with run 001's model
(exported by the `develop` trainer, loads and runs under package 4.1.0).

Workflow: `mlagents-learn … --env Builds\Prosthetic\Prosthetic.exe --num-envs 8 --no-graphics` → Ctrl+C when
done → `tools\deploy_model.cmd <run-id>` → Play in Editor. Do **not** press Play while `--env` training runs
(harmless, but the Editor just runs inference on the old model and is unrelated to the training).

---

## Phase D (2026-09-01): target randomization + reaching (Option A, 5 arm axes)

**Actuation set (decision):** RL controls the 14 finger groups plus 5 arm axes — shoulder flexion
(`Bicep.r` local X), shoulder abduction (`Bicep.r` local Z), elbow flexion (`forearm.r` local X),
wrist flexion (`palm.r` local X) and wrist pronation (`palm.r` local Y). Humeral rotation stays
frozen (not needed: the 50-spawn scripted reach test aligned the palm everywhere in the spawn volume).

**Rationale:** the proximal joints (shoulder, elbow) emulate the user's gross reach; the distal joints
(wrist, fingers) are the prosthesis. A distal-only ablation — freeze shoulder/elbow, scripted IK brings
the palm to a standoff pose, RL owns wrist + fingers — is planned later for transradial realism. The arm
actuation is therefore built so freezing the proximal axes is the serialized toggle
`ArmGraspAgent.actuateProximalJoints` (action/observation sizes unchanged), not a rewrite.

**Rig audit (edit pose):** `ArmAnimation/Armature/Bone/Bicep.r` (shoulder pivot, 0.749 m) →
`forearm.r` (elbow, 0.818 m) → `palm.r` (wrist) → fingers. Every bone's long axis is local +Y (Blender
import, local scale 100). Elbow rest angle 132.6°. Full extension 1.567 m; the original cylinder sat
1.60 m from the shoulder — at the edge of reach — so spawns are placed **1.1–1.5 m** from the shoulder
pivot instead. No Animator/Joints/ArticulationBodies. `JointOrientation` (Myo) sets the root rotation
to identity at Play (edit pose is 3.1° off) and was verified constant over 120 frames (0.00000° drift),
so it is left enabled. Rig changes: CapsuleCollider added to `forearm.r` (0.818 m × r 0.045 m); the
palm BoxCollider was already 0.044 m thick (an earlier audit misread it as zero) and is unchanged.

**Spawn:** uniform in a horizontal disk (`spawnRadius`, overridable by the `spawn_radius` environment
parameter) around `spawnCenter` (0.48, 0.488, 5.57), + uniform height in [0, `spawnHeightRange`],
random yaw; rejection-sampled to `reachRange` [1.1, 1.5] m from the shoulder and no overlap with the
reset arm pose. **Observations 55** = 42 (as before) + sin/cos × 5 arm axes + cylinder position relative
to the palm (palm frame, / `targetObsScale` = 1 m). **Actions 19** = 14 fingers + 5 arm axes.
**Reward:** + potential-based shaping on the distance from the palm *grasp point*
(`graspPointOffset`, where the cylinder sits in the run-004 grasp) to the cylinder surface, same
coefficient scale as the finger shaping; everything else unchanged. Arm axes use the same rate-limited,
limit-clamped, ComputePenetration-bisected actuation as the fingers (all 16 arm/hand colliders).

Run 004's model (28+14 obs, 14 actions) is **incompatible** with this scene; never `--resume` from it.

## Deferred: squeeze depth / penetration clamp (2026-09-07)

The drop-test control experiments (`results/008/controls/REPORT.md`) showed that grasp failures are axial slides
limited by normal-force retention: the kinematic hand only generates contact force by resolving the 0.5 mm
`penetrationTolerance` clamp, and that force relaxes unless the posture wedges the object. Two environment levers
exist: contact friction (adopted as the evaluation curve, mu = 0.6 / 1.0 / 1.5, primary 1.0) and a controllable
squeeze depth (letting the policy command penetration beyond the clamp, or a compliant contact model). The squeeze-depth
/ clamp change is **deferred to future work**: it changes the contact clamp in `ArmGraspAgent.cs`, the observation
semantics and the comparability with runs 004-008, so it needs its own study.

## Editor hang: inference with a model that does not match the sensor layout (2026-09-09)

Entering Play mode with Behavior Type InferenceOnly (or Default without a trainer) while the referenced ONNX model was
trained for a different observation layout (the run 009 model, 55-float vector obs, against the run 010 layout of a 22-float
vector obs plus a 16x13 BufferSensor) does **not** produce an error: the Editor's main thread spins at 100% (three cores busy),
the Pipeline server stops answering, and the process must be killed (relaunch then shows the "Recovering Scene Backups" and
"Packages with Errors" dialogs; decline the recovery, dismiss the package dialog). Until a compatible model is deployed the
scene is kept on HeuristicOnly; when a new model is deployed, switch to InferenceOnly and confirm Play mode runs before
committing the scene.

## Integrator reconciliation and step-response verification (2026-09-14)

`ArmGraspAgent` integrates every finger group and wrist axis with the exact one-step transition matrix of the linear
spring-damper (`TransitionMatrix`, closed form per (k, b, I) and the fixed timestep, branching on the damping regime).
That update is unconditionally stable for k >= 0, b >= 0, I > 0, so `MorphologyManager` no longer projects externally
supplied springs into the semi-implicit Euler stability region: `Project()`/`IsStable()` became `CheckPlausibility()`/
`IsPlausible()`, which only log and count (`OutOfRangeEvents`, recorded as `Morph/OutOfRangeEvents`) requests outside the
plausibility bounds omega in [1, `maxPlausibleOmega` = 40] rad/s, zeta in [0, `maxPlausibleZeta` = 0.9], and sanitize
non-physical values (I <= 0, k < 0, b < 0). The sampled ranges are unchanged. Serialized fields keep their scene values
through `FormerlySerializedAs`.

Step test (`results/010_verify/step_exact.csv`, indexBase free joint, 45 deg setpoint step, 50 steps of 0.02 s, reference
hand): max |angle - analytic| = 0.0001 deg on all three triples (omega, zeta) = (12, 0.4), (25, 0.7), (40, 0.9), i.e.
0.0002% of the step, against the 2% criterion; velocities agree to 0.002 deg/s. The archived
`results/010_verify/step.csv` / `session2/step.csv` (2026-09-09) match semi-implicit Euler to 0.0001 deg and deviate from
the analytic solution by 4.8 / 9.3 / 19.8 deg, so they were produced before the integrator change and are superseded.

Diagnostic gotcha found while re-running: the first Play-mode episode samples theta (scene `randomizeByDefault` = 1), and a
harness that merely disables further sampling keeps that draw, including the actuation mask, so masked groups sit at their
neutral pose (indexBase -30 deg) during a "deterministic" test. `MorphVerify` now resets link scales and the mask in its step
and close modes; every close/grid CSV from 2026-09-09 (`results/010_verify/session2/`) was run before that fix and carries an
unknown random mask.

## Grasp-point reachability at small length scales: diagnosis (2026-09-14, awaiting approval before any fix)

Forced-close tests (`MorphVerify` close/closegrid modes, mask and link scales reset explicitly, cylinder teleported to
`GraspPoint`, `results/010_verify/clean/`). Palm frame as measured from the palm BoxCollider (4.4 x 19.4 x 20.7 cm) and
the finger-base positions: **x = palm normal (closing direction), y = along the fingers, z = across the palm** (the
`EffectiveGraspPointOffset` comment has x and z swapped).

- With the current formula (x, y scaled by handSpan / handSpanRef, z fixed) the target sits at (0.168, 0.185) m for scale
  0.8, (0.186, 0.205) for 1.0, (0.204, 0.225) for 1.2. Clean closes: 0.8 -> 4 contacts, gate never; 1.0 -> 7 contacts,
  gate at step 68; 1.2 -> 8 contacts, gate at step 32 (`close_<scale>.csv`). The "1.2 regression" seen on 2026-09-09
  was stale-mask noise (see the previous section).
- Absolute placement grids (`absgrid_<scale>.csv`, x in 0.08..0.20, y in 0.17..0.215, 220 steps): scale 0.8 reaches
  8-10 contacts and gates in 22-70 steps for x <= 0.14 with y >= 0.20 (best (0.11, 0.215): 10 contacts, gate at step 22);
  the formula's (0.17, 0.185) cell gives 4 contacts and never gates. Scale 1.0: robust region x = 0.11-0.14, y >= 0.185
  (9-10 contacts); the reference (0.186, 0.205) lies at the edge (5-7 contacts, gate 66-132 or never at (0.20, 0.20)).
  Scale 1.2: x = 0.11-0.20 with y >= 0.185 all gate. The largest workable x grows with finger length (about 0.14 / 0.17 /
  0.20 m at 0.8 / 1.0 / 1.2), while the workable y band (>= 0.185-0.20 m) is the same at every scale.
- Geometry (`converge_<scale>.csv`, forced close with no object): the finger-base segments sit at y = 0.265-0.314 m and
  the palm box is identical at every scale (link scaling moves only the segments distal to each pivot), so the along-finger
  position of the enclosed region does not move with the hand span; only the closing radius (x reach: fingertip ends at
  x = 0.18 / 0.23 / 0.27 m after the base phase) scales with finger length.

Conclusion: (a) the offset formula misplaces the target: it shrinks y with the span (y should stay fixed) and scales x
with the span ratio (0.903 at scale 0.8), which is weaker than the finger-length ratio (0.8) that the closing radius
actually follows; the reference x itself is also 3-5 cm beyond the most robust region even at scale 1.0. (b) is false:
the environment is feasible at scale 0.8 (10 contacts, thumb + 2 fingers, gate in 22 steps). (c) is false: the same
scripted close gates as soon as the target is placed correctly. Contact rows with >= 6 contacts but gate = 0 (small x,
small y at scales 1.0/1.2) failed the thumb / distinct-finger conditions of the gate, not the contact count.

## Fix: grasp-point reference at the robust centre, finger-length-ratio scaling (2026-09-15)

`graspPointOffset` reference is now **(0.14, 0.215, 0.078) m** (was (0.186, 0.205, 0.078)), the robust centre of the
scale-1.0 forced-close placement grid: at x = 0.14 the y = 0.215 cell gives 9 contacts with the gate at step 27, versus
7 contacts at step 132 for y = 0.20; the old reference sat at the edge of the workable region (5-7 contacts, gate late or
never). `EffectiveGraspPointOffset` scales only x, by `MorphologyManager.FingerLengthRatio` (sum of the 14 link lengths
over the reference sum); y and z are fixed, because the finger-base pivots and the palm do not move with the link
scales. The wedge thresholds keep the hand-span scaling (they measure finger spread, not the closing radius). The axis
comment is corrected: x = palm normal (closing direction), y = along the fingers, z = across the palm. The scene
(`Dynamic_Scene.unity`) carries the new value on both ArmAnimation agents.

Rationale for changing the reference rather than only its scaling: the offset feeds only the potential-based reach
shaping and spawn sampling, both policy-invariant / task-neutral, and run 010 is a fresh lineage, so continuity with the
run-009 reference protects nothing. Retro note: runs 004-009 trained with shaping aimed at that marginal grasp point.

Verification (`results/010_verify/clean/fixedR_close_*.csv`; forced close at the reference morphology: mask, link
scales and springs reset (fingers omega = 25, wrist 15 rad/s, zeta = 0.7, nominal inertia), object teleported to
`GraspPoint` and backed off along the palm normal until free of the open hand, 300 steps):

| scale | rest-pose back-off | contacts (final) | gate first met (step) | finger penetration |
|---|---|---|---|---|
| 0.8 | 51 mm (open thumb blocks the target) | 7 | 67 | 0.50 mm |
| 1.0 | 16 mm | 7 | 135 | 0.50 mm |
| 1.2 | 0 mm | 7 | 65 | 0.50 mm |
| 0.8, random mask seed 1 (12 active) | 51 mm | 6 | 129 | 0.50 mm |
| 0.8, random mask seed 2 (12 active; middleBase + ringBase masked) | 51 mm | 5 | never | 0.50 mm |
| 0.8, random mask seed 3 (11 active) | 51 mm | 6 | 67 | 0.50 mm |

The gate holds until the end of every run that reaches it. The seed-2 draw masks two base joints; a scripted wrap cannot
close those fingers, so this is a property of the mask distribution, not of the offset. The earlier variants
(`fixed_close_*.csv`: minimum-translation push-out, lands in the marginal y band; `fixedN_close_*.csv`: palm-normal
push-out but with the first episode's random springs still in place) give the same contact counts within one and are
kept only for the record. Placement grids without overlap resolution (`absgrid_*.csv`) overstate small-x cells, where the
teleported object already intersects the thumb base at rest.

Harness gotcha (repeated here because it invalidates older data): the first Play-mode episode samples theta when the
scene has `randomizeByDefault` = 1, and a harness that only disables further sampling keeps that draw, mask included.
All close/grid CSVs from 2026-09-09 (`results/010_verify/session2/`) were produced that way and carry unknown masks.
The same applies to the spring parameters (k, b, I): every diagnostic before `fixedR_*` ran with the first episode's random
draw; contact counts at settle do not depend on it, closing times do.

## Exploit checks under impedance actuation (2026-09-15, `results/010_checks/`)

Scripted controller (`GraspHarness` v5, temporary diagnostic): runs at the reference morphology (mask, link scales and
springs reset before the first trial), wrist axes commanded through `SetArmSetpoint`, shoulder/elbow as velocity
actions, measurement points wait for the joints to settle, the reach accepts a pose within 6 cm (the new grasp point sits
inside the open hand, so the servo stalls in thumb contact) with a bounded retreat fallback, and the cycler releases at
30 held steps (fingers lag their setpoints by ~12 steps; at 45 the hold reached 50 before contact broke). Scene decision
period 5 as in training.

| mode | result | 2026-09-07 (run 009 reference) |
|---|---|---|
| complete (yaw 0/120/240) | 3/3 success, 264-435 steps, Q 0.20, return 0.90 + 0.23 + 1.00 - 0.05 = 2.07-2.08 | 3/3, 147 steps, 1.98 |
| telescope (no teleport) | 3/3 success; agent shaping vs analytic telescoped sum: 0.9006 / 0.8991 / 0.9364 vs 0.90055 / 0.89908 / 0.93641 (within 5e-5) | within 4e-5 |
| cycle (hold 30 / release 60 to MaxStep) | 31 cycles, no success, return 0.88 + 0.23 + 0 - 1.00 = 0.11 | 0.11 vs 1.98 |
| stack (fingers only, thumb open) | contacts 6, thumb 0, gate 0, Q 0.188, no bonus | Q 0.156 vs 0.195 |
| straddle (palm press + extended index) | contacts 5, gate 0, Q 0.167, no bonus | Q 0.116 |
| shelf (object resting on curled fingers) | contacts 2, gate 0, Q 0.063, no bonus | Q 0.063 |

Reward code path unchanged since run 009 apart from the added effort (-2e-5 * sum a^2 per step) and safety
(-1e-4 per joint-limit saturation per step) terms, which are not in the per-episode CSV columns above.

## Run 010 pipeline: player build, 10k smoke, ONNX-attention go/no-go (2026-09-15)

**Scene interim setting changed from HeuristicOnly to Default with no model reference.** The 10k smoke against a
HeuristicOnly build timed out (`UnityTimeOutException`: in HeuristicOnly the agents never request decisions from the
trainer). Default is the normal training configuration; with the incompatible run-009 model reference cleared
(`m_Model: {fileID: 0}`) the Editor falls back to the heuristic when no trainer is connected, so the documented hang
cannot occur. `Assets/Models/Prosthetic.onnx` (run 009) stays in the project untouched and is re-referenced only once a
compatible signed-off model exists.

Player build: `unity command build` (StandaloneWindows64, `Builds/Prosthetic/Prosthetic.exe`), 0 errors, level0 and
Assembly-CSharp.dll dated 2026-09-15 11:04.

10k smoke (`results/010_smoke10k_b/`, fresh lineage, `hold_decisions` = 2, `morph/randomize` = 1, 8 envs, 62 s): trains
and exports `Prosthetic-10096.onnx` (568 KB vs 138 KB for run 009). Mean reward -2.1 to -2.5 at this point is the
existential, effort and safety terms under a near-random policy. Model inputs: `obs_0` [16 x 13] (JointTokens
BufferSensor), `obs_1..obs_10` (the ten legacy ray sensors, 3 or 2 floats each), `obs_11` [22] (vector observation);
outputs `continuous_actions` [19]. Opset 9, 178 nodes; new op types vs run 009: MatMul, Softmax, Transpose, Reshape,
ReduceMean, ReduceSum, Pow, Sqrt, Less (the attention block).

**ONNX-attention go/no-go: GO.**
1. Edit mode, Inference Engine directly (`ModelLoader.Load` + `Worker(CPU)` + one `Schedule` on zero inputs): model
   imports as 106 layers (Dense, MatMul, Softmax, Transpose, ReduceMean, Swish, ...), one inference 75 ms, 19 finite
   outputs.
2. Play mode, ML-Agents path: `BehaviorParameters.Model` = the smoke asset, `BehaviorType` = InferenceOnly, Burst
   device, set at runtime (not saved). 5,884 Academy steps in ~100 s, one episode completed at MaxStep, the Editor
   answered all 12 watchdog polls, no ML-Agents model-check errors in the console. (With the run-009 model this
   configuration hung the Editor within seconds on 2026-09-09.)

150k smoke (`results/010_smoke150k/`, K = 2, random theta, 858 s = about half the 009 trainer's speed because of the
attention network and impedance physics): mean reward -2.9 -> -1.3..-1.5, shaping return -1.1 -> +0.43, minimum
grasp-point distance 0.28 -> 0.08 m, no successful grasp yet (fresh lineage; 009 continued from 008's checkpoint). Effort
return -0.21 and safety return -0.58..-0.75 per 5000-step episode (5.8k-7.5k joint-limit saturations). Morph stats confirm
per-episode randomization (LengthScaleMean 0.99-1.03, ActiveGroups 11.3-11.7, OmegaMean 25.9, OutOfRangeEvents 0).

**Run 010 config (`config/run_010.yaml`):** fresh lineage, 6M steps, `morph/randomize` 1, hold-decision curriculum
2 -> 4 -> 6 -> 8 -> 10 with the lesson threshold restated on this reward scale: a completing grasp returns about 2.0
(shaping 0.9 + Q 0.2 + bonus 1.0 minus step costs) and a non-grasping episode -1.5 to -2.0, so lessons advance at a
smoothed mean reward of 1.0 (min lesson length 150), i.e. when a clear majority of episodes grasp. Launched 2026-09-15
with `--env Builds/Prosthetic/Prosthetic.exe --num-envs 8 --no-graphics`; expected wall time about 9 h at the smoke's rate.

## Run 010: training (2026-09-15/16, `results/010/`)

6M steps in 10.1 h (8 headless envs; note that eight orphaned player processes from the failed HeuristicOnly smoke
attempt ran alongside for the whole run and roughly halved the trainer's throughput; they were terminated afterwards).
Fresh lineage, per-episode theta, hold-decision curriculum 2 -> 4 -> 6 -> 8 -> 10 with threshold 1.0: lessons advanced at
1.82M / 1.88M / 1.94M / 2.00M steps (each after the 150-episode minimum), so 4M steps trained at K = 10.

Last 500k steps (TensorBoard means): success 0.91, cumulative reward 1.75, shaping 0.87, quality 0.34, bonus 0.91,
existential -0.14, effort -0.04, safety -0.19; contacts at end 7.3, Q at end 0.36, steps to success 276, episode length
140 decisions; morphology stats confirm randomization (active groups 11.2, length-scale mean 1.00, omega mean 26.0).
Final model `results/010/Prosthetic.onnx` (6,000,035 steps). NOT deployed at this point: `Assets/Models/Prosthetic.onnx` stays run 009
pending the held-out evaluation and sign-off (deployed 2026-09-18, see below).

## Run 010: held-out evaluation, theta-binned (2026-09-16, `results/010/validation/`)

`GraspDiagnostic` v5 in Play mode (model `results/010/Prosthetic.onnx` assigned at runtime, InferenceOnly, Burst),
fresh seeds 3001-3100 (`seeds.txt`; 1001-1100 and 2001-2100 were already used for model decisions), theta sampled per
episode from the training distribution and logged per episode (`episodes.csv` columns `lenMean..fingerLengthRatio`),
drop test at mu = 0.6 / 1.0 / 1.5 x 3 repeats after a 50-step hold, pass = centre displacement < 0.1 m after 2 s.
Report: `THETA_REPORT.md` (generated by `theta_report.py`, bootstrap 95% CIs).

| | run 010 (random theta, seeds 3001-3100) | run 009 (reference hand, seeds 2001-2100) |
|---|---|---|
| grasp success (hold reached within MaxStep) | 0.90 [0.84, 0.95] | 1.00 |
| drop-pass given hold, mu = 0.6 / 1.0 / 1.5 | 0.59 / 0.73 / 0.81 | 0.60 / 0.86 / 0.98 |
| pass x success at mu = 1.0 | 0.66 [0.57, 0.75] | 0.86 |

By theta (mu = 1.0 pass given hold; success):
- mean link-length scale 0.80-0.93 (n = 13): pass 0.60, success 0.77; 0.93-1.07 (n = 81): 0.76, 0.93; 1.07-1.20
  (n = 6): 0.60, 0.83. Single-finger bins spread the sample: thumb scale 0.80-0.93 / 0.93-1.07 / 1.07-1.20 -> pass
  0.77 / 0.74 / 0.68 and success 0.94 / 0.94 / 0.82 (long thumbs grasp slower, 435 vs ~200 steps); index scale ->
  pass 0.77 / 0.61 / 0.81, success 0.97 / 0.88 / 0.84.
- finger omega: the mean over 14 groups sits in 21-31 rad/s for 97 of 100 episodes (pass 0.74, success 0.90); by the
  minimum finger omega, 12-14 (n = 64): pass 0.73, success 0.86; 14-17 (n = 28): 0.81, 0.96; 17-40 (n = 8): 0.50, 1.00.
- active finger groups: 6-9 (n = 9): pass 0.78, success 1.00 (slow, 523 steps); 10-11 (n = 48): 0.67, 0.81;
  12-13 (n = 35): 0.79, 0.97; 14 (n = 8): 0.75, 1.00.

Reading: the policy generalizes across the theta range (no bin collapses; success >= 0.77 and mu = 1.0 pass >= 0.60 in
every bin with n >= 5), with the weakest cells at the short-finger end and for 10-11 active groups. Against the run-009
reference-hand numbers the mu = 1.0 pass is lower (0.73 vs 0.86), but the comparison is not like-for-like: 010 is a
fresh lineage evaluated on random morphologies including masked joints and soft springs, 009 was fine-tuned from 008
and evaluated on the reference hand only. A reference-hand evaluation of 010 (theta fixed) and a longer or 009-initialized
lineage are the natural next comparisons. **Run 010 is not deployed** at this point; `Assets/Models/Prosthetic.onnx` remains run 009
(reference-hand evaluation and deployment: 2026-09-18 entry below).

Cleanup: the temporary diagnostics (`MorphVerify`, `GraspHarness`, `GraspDiagnostic`) and the temporary model assets
(`Smoke010.onnx`, `Run010.onnx`) were removed from `Assets/`; their sources and the runner scripts are archived in
`results/010_verify/session3/` and `results/010/validation/`.

## Run 010: trainer-log integrity check, reference-hand evaluation, deployment (2026-09-18)

**Trainer-log integrity: exactly 8 environments.** Because eight orphaned player processes from the failed
HeuristicOnly smoke ran alongside run 010 (see the training entry above), the trainer log was checked for the number of
environments that actually registered. `results/010/010.log` opens with exactly eight registration pairs (lines 1-16),
one per `--num-envs 8` worker:

```
[INFO] Connected to Unity environment with package version 4.1.0 and communication version 1.5.0   (x8)
[INFO] Connected new brain: Prosthetic?team=0   (x8)
```

`results/010/run_logs/` holds `Player-0.log` .. `Player-7.log` (eight), each with `Registered Communicator in Agent.`;
`010.err.log` is empty; training ran to step 6,000,035 and exported the final model. Sixteen registrations would have
meant the orphans had joined the trainer; eight means they were CPU contention only (they roughly halved throughput and
contributed no experience). The run is clean.

**Reference-hand evaluation of run 010 (`results/010/validation/reference_hand/`, run 2026-09-15 22:11; diagnostic-grade).**
`GraspDiagnostic` v5 with `referenceHand = true` (`GraspDiagnostic_v5_referenceHand.cs`, runner `runeval_ref.sh`): every
episode on the fixed reference hand (all link-length scales 1.000, full 14-bit mask, 14 active groups, finger omega
25 rad/s, zeta 0.7, wrist omega 15, theta sampling off; `episodes.csv` shows `randomizing = 0` and the nominal theta on
every row), model `results/010/Prosthetic.onnx` assigned at runtime, InferenceOnly, Burst, drop test at mu = 0.6 / 1.0 /
1.5 x 3 repeats. Seeds 2001-2100 (`seeds.txt`) are the seeds of the 008/009 comparison, so this evaluation is
**diagnostic-grade** (those seeds were already used for a model decision; 1001-1100 and 3001-3100 are spent too).
Summary: `SUMMARY.md` (`refhand_summary.py`); paired per-seed comparison at every mu: `PAIRED_MU.md` (`paired_mu.py`,
the mu = 1.0 method applied to all three: per-seed pass fraction over the 3 repeats, 0 when the hold was not reached,
bootstrap 95% CI with 4000 resamples). Both policies reached the hold in 100/100 episodes.

| μ | 010 reference hand | 009 reference hand | paired 010 − 009 [95% CI] | 010 wins / losses / ties | 010 random θ (seeds 3001–3100, given hold) |
|---|---|---|---|---|---|
| 0.6 | 0.690 [0.600, 0.777] | 0.603 [0.520, 0.690] | +0.087 [−0.037, +0.207] | 36 / 22 / 42 | 0.59 |
| **1.0** | 0.770 [0.690, 0.850] | **0.857 [0.800, 0.910]** | −0.087 [−0.190, +0.010] | 20 / 22 / 58 | 0.73 |
| 1.5 | 0.880 [0.817, 0.937] | 0.980 [0.960, 0.997] | −0.100 [−0.167, −0.040] | 5 / 14 / 81 | 0.81 |

Any-repeat / 3-of-3 pass: 010 70% / 68%, 77% / 77%, 89% / 86%; 009 72% / 47%, 94% / 75%, 100% / 95% (mu 0.6 / 1.0 / 1.5).
010's higher mean at mu = 0.6 (0.69 vs 0.60) is, per seed, +0.087 [-0.037, +0.207] with 36 wins, 22 losses and 42 ties;
at mu = 1.5 the difference is -0.100 [-0.167, -0.040]. Grasp statistics at hold, 010 on the reference hand (009 in
parentheses): steps to hold 150 (009: 87), return 2.26 (2.72), mean Q 0.41 (0.83), contacts 8.7 (7.4), vertical spread 0.13 m (0.45), palm contact 0/100 (98/100), antipodality 0.44 (0.65). The comparison is not like-for-like (010: fresh 6M-step lineage trained on random
morphologies; 009: fine-tuned from 008, reference hand only), and a fresh seed set is required for publication numbers.

**Deployment (2026-09-18, user sign-off).** `results/010/Prosthetic.onnx` (final export, step 6,000,035) copied to
`Assets/Models/Prosthetic.onnx` (asset GUID unchanged, no `.bak` left behind):

```
sha256  cf75a06b2ccd4e77f19d3f5aab9c84c9df4aa26b3972ab17e12265221d77a81f  Assets/Models/Prosthetic.onnx  (= results/010/Prosthetic.onnx)
sha256  5ade5001fac461b7c097a26a3479ef5391434b0a3367df402d015b69955d5ea6  results/009/Prosthetic.onnx    (previous deployed model, kept)
```

`Dynamic_Scene.unity`: both `BehaviorParameters` (the `ArmAnimation` object and the inactive `New_ExperimentalSetup`
prefab instance) set to Behavior Type InferenceOnly with the run-010 model referenced, reverting the interim
Default / no-model setting of 2026-09-15. Play-mode check after the save (Editor 6000.3.18f1, Burst/CPU inference,
no trainer): 1,706 Academy steps and 10 completed episodes in 34 s, agent stepping under InferenceOnly with the run-010
model, cumulative rewards 0.30-1.05 mid-episode, zero console errors, the Editor answered every poll (no hang), Play mode
exited cleanly; the post-Play scene state was discarded, not saved.
For the next `--env` training run the player must be rebuilt from a Default-behavior scene as before.

## BO outer loop, Part A: fixed-theta evaluation endpoint (2026-09-18, `tools/bo_eval/`, `Assets/Scripts/BoEval/`)

**Route: Unity-side harness in a headless player, driven by a job file, results read from CSVs** (the fallback route,
chosen up front rather than after a failed ONNX-in-Python attempt). Reasons: (1) the ground-truth metric is the drop
test, which is Unity-side physics on the live scene (`Physics.Simulate` from the pre-drop state, friction sweep) no
matter where the policy runs, so a Python policy would still need a Unity-side harness for everything but the action;
(2) the exported run-010 graph samples its `continuous_actions` head with a `RandomNormalLike` node (the
`deterministic_continuous_actions` head is the mean), and the scene runs with `DeterministicInference = 0`, so the
recorded evaluations used Unity's sampled head with the Inference Engine's noise stream, which a Python replica cannot
match action for action; (3) running the deployed asset through the same ML-Agents `InferencePolicy` + Inference Engine
CPU backend in a player is the recorded evaluation's code path by construction, and the drop-test code is the
GraspDiagnostic v5 code verbatim. ONNX-in-Python was not built.

Mechanics: `BoEvalBootstrap` (`RuntimeInitializeOnLoadMethod`) attaches `BoEvalHarness` to the active `ArmAnimation`
only when the player is started with `-boEvalJob <job.json>` (in the Editor, a path in `Temp/boeval_job.txt` does the
same at the same point of the Play timeline, for the fidelity check); the committed scene is used unchanged and the
player is built from it (`unity command build --target StandaloneWindows64 --outputPath Builds/BoEval/BoEval.exe
--confirm true`; `Builds/` is gitignored). Theta is pinned through the `MorphologyManager` serialized fields with
`randomizeByDefault = false` before every episode (`k = I w^2`, `b = 2 zeta I w`, `I = nominalI * inertiaScale`; the
harness resets twice at start so the nominal inertia is recomputed at the requested link scales before the first
evaluated episode); a player without a trainer has no environment parameters, so the pinned fields are what
`ApplyForEpisode` keeps. Episodes: `Random.InitState(seed)` before each reset, hold = 50 consecutive held steps
(`requiredHoldDecisions` raised so the agent never ends on success), MaxStep ends are logged as failures and reseeded,
exactly as GraspDiagnostic v5. Forced-close oracle (`feasible`): MorphVerify close mode under HeuristicOnly (object at
`GraspPoint`, backed off along the palm normal, staged wrap, 300 steps), `feasible` = hold criterion met at any step;
at the reference hand: gate at step 68, 8 contacts, 14 mm push-out (the 2026-09-15 `fixedR_close_1.0` run reported
16 mm / 7 contacts / step 135 with the pre-fix grasp point). Outputs per call under `results/bo_eval/runs/<stamp>/`:
`episodes.csv`, `drops.csv` (reference-evaluation columns), `oracle.json`, `result.json`, optional `decisions.csv`.
Python (`tools/bo_eval`, stdlib only): `evaluate(theta, n_episodes=20, mu_levels=(1.0,), seed_block=(4001, 4020),
deterministic=False, oracle=True, ...)` returns success (count, rate, bootstrap CI), drop-pass per mu (given hold and
times success, CIs, any / 3-of-3), palm-contact rate, mean contacts, mean steps to hold, `feasible` + oracle record,
CSV paths; `feasible(theta)` runs the oracle alone; `reference_theta()`, `random_theta(rng)` (training distribution).
Q is not reported. **Seeds 4001-4100 are reserved for BO evaluations** (common random numbers across candidates);
1001-1100 / 2001-2100 / 3001-3100 are refused unless `allow_spent_seeds=True`. 100 episodes x 3 mu ~ 20 s wall.

**Gate 1, inference fidelity (deterministic head, tolerance 1e-3).** (a) Fixed observation set: 64 synthetic input sets
(BufferSensor tokens with 6-16 active rows and zero padding, rays in [0, 1], vector observation in range) through the
deployed asset with `Unity.InferenceEngine.Worker(BackendType.CPU)` in the Editor (edit mode) and in the player: max
|difference| of `deterministic_continuous_actions` = 0.0 (bitwise); player run-to-run 0.0; the sampled head differs
between Editor and player by up to 1.67 (different noise streams) but is identical between two player runs (the stream
is seeded per process). (b) Trajectory: reference hand, seeds 2001-2002, deterministic head, harness attached at scene
load in both: 267/267 decisions have identical vector observations and identical actions (max |difference| 0.0), steps to
hold 143 / 125 in both, all 6 drops pass in both. A first attempt with the harness attached mid-Play in the Editor did
not match (the arm state before the first evaluated episode differed) and was discarded.

**Gate 2, reproduction of the recorded reference-hand evaluation (seeds 2001-2100, sampled head as recorded).**

| mu | recorded (Editor, 2026-09-15) | endpoint (player) | per-seed pass-fraction match | per-repeat row match |
|---|---|---|---|---|
| 0.6 | 0.690 [0.600, 0.777] | 0.663 [0.567, 0.753] | 68 % | 70 % |
| 1.0 | 0.770 [0.690, 0.850] | 0.747 [0.660, 0.827] | 72 % | 72 % |
| 1.5 | 0.880 [0.817, 0.937] | 0.840 [0.767, 0.907] | 77 % | 80 % |

Success 100/100 in both; spawn pose identical for 100/100 seeds; steps to hold identical for 6/100 (the sampled head's
noise stream differs between the Editor session that produced the record and the player), mean 152.9 vs 150.3. All three
endpoint means lie inside the recorded CIs. The endpoint is reproducible run to run: a second identical call gave
900/900 identical drop rows and 100/100 identical steps to hold.

**Gate 3, theta takes effect.** `random_theta(Random(7))`: scales 0.930 / 0.860 / 1.060 / 0.829 / 1.014, finger omega
13.05-38.54 (mean 22.63), zeta mean 0.579, wrist omega mean 19.67, mask 11111110111110 (12 active). Every episode row
reports exactly these values (`randomizing = 0`, lenMean 0.9387, handSpan 0.4295, fingerLengthRatio 0.9367) against
1.000 / 25.00 / 0.700 / 15.00 / 14 / 0.4447 / 1.0009 for the reference hand. Result on seeds 4001-4020: feasible (gate at
step 70, 7 contacts, 22 mm push-out), success 20/20, pass given hold 0.17 / 0.58 / 0.70 at mu 0.6 / 1.0 / 1.5.

**Palm contact by theta (read-only, `tools/bo_eval/palm_by_theta.py`, `results/bo_eval/analysis/palm_by_theta.md`;
records `results/010/validation/episodes.csv`, random theta, 90 of 100 held).** Palm-contact rate at hold: all random
theta 0.01 (1/90); by mean link-length scale 0.80-0.93: 0.10 (n = 10), 0.93-1.07: 0.00 (75), 1.07-1.20: 0.00 (5); by
active finger groups 6-9: 0.00 (9), 10-11: 0.00 (39), 12-13: 0.03 (34), 14: 0.00 (8); 010 reference hand 0.00 (100);
009 reference hand 0.98 (100). Mean contacts at hold 7.7 (random theta) / 8.7 (010 reference) / 7.4 (009). The
palm-less pinch grip is global to the 010 policy, not specific to the reference hand.

## BO outer loop, Part B: Bayesian morphology optimization campaign (2026-09-18, `tools/bo_optim/`, report `results/bo_optim/REPORT.md`)

Objective: pass x success at mu = 1.0 from `tools/bo_eval.evaluate` (20 episodes, seeds 4001-4020, sampled action head), one
player build (sha256 ff457d2c...) for the whole campaign, drift checks after every 25 objective evaluations all byte-identical
to the anchor (5/5). Surrogate: exact GP (Matern-5/2 with one lengthscale per parameter block x exponential Hamming kernel on
the mask, torch), acquisition EI x P(feasible) (L2 logistic feasibility model on the forced-close oracle outcomes) ranked over
10,000 random constrained draws + 5,000 local perturbations per iteration. Arms: BO (reference anchor + 29 random, then 70
iterations) and a 100-draw random baseline; confirmation of the top 5 of each arm on seeds 4021-4100 (80 episodes, mu 0.6 /
1.0 / 1.5). 200 candidates, 126 oracle-feasible (63 %), 42 min wall. Headline (confirmation, mu = 1.0): BO top 5 confirm at
0.988-1.000 (mean 0.993; two candidates at 80/80), random top 5 at 0.921-1.000 (mean 0.965); the reference hand scores 0.750
on the optimization block. The 20-episode objective saturates (31/68 feasible BO candidates at 20/20), so the arms separate
in the bulk and in confirmation rather than in best-so-far. The BO winners form one family (indexMiddle and ringBase masked,
index and thumb scales 0.80-0.87, other fingers 1.05-1.20, omega 27-30 rad/s); palm contact stays at zero everywhere. The
report, `candidates.csv`, `best_so_far.csv/.svg`, `build_hash.json`, `summary.json` and `records.jsonl` are force-added
under `results/bo_optim/` (the `results/` tree is otherwise ignored); per-candidate CSVs stay local.

## Articulated hand, spike 2 (2026-09-22, branch `articulated-hand`): human-scale rescale and physical-units stiffness

**Rescale (commit 44f118d).** The imported armature is about 2.5x human size (open hand 524 mm wrist to middle
fingertip, palm 232 mm across the base pivots, forearm 818 mm). `ArticulatedHand.modelScale` (0.36) is the ONE scale
constant: `CaptureBones` sets the armature transform to `armatureBaseScale x modelScale` before the bones are captured, so
link lengths, anchor offsets, capsule radii / lengths and capsule-volume masses (density 1000 kg/m^3) all follow it; the
palm link mass is set directly (0.33 kg). Result: hand 187.5 mm, palm 82.3 mm, forearm 294 mm, hand mass 0.420 kg
(`results/011_artic2/s1_anthropometrics.txt`). Every metric constant in the scene, agent and gate harness was walked to
human scale (see the commit message for the table); object 5.0 cm diameter x 28 cm, mass range 0.2-1.5 kg unchanged.

**Theta stiffness in physical units.** `MorphologyManager` no longer samples a natural frequency. Each impedance group
samples k (N m/rad) log-uniformly inside its joint-class range, anchored on `results/011_artic2/stiffness_survey.md`:
MCP (base) 0.5-6, PIP (middle) 0.3-3, DIP (end) 0.1-1, thumb base 1-15 (2-3x MCP, unverified), thumb end 0.3-3 (= PIP,
unverified), wrist 1-10 (unverified). zeta is uniform in [0.3, 1.0]; b = 2 zeta sqrt(k I) with I the geometric subtree
inertia x the sampled inertia scale (which still acts on the link masses); omega = sqrt(k / I) is DERIVED and only
logged (`Morph/OmegaMean`, `Morph/StiffnessMean`; hundreds of rad/s at human-scale inertia). The reference hand uses the
geometric mean of each range (base 1.73, middle 0.95, end 0.32, thumb 3.87 / 0.95, wrist 3.16) with zeta 0.7. The
environment-parameter keys `morph/k_*`, `morph/b_*`, `morph/I_*` are unchanged (they were already physical); the
plausibility check is now on k (`plausibleStiffness` 0.01-30 N m/rad) and zeta (<= 1.2), logging the derived omega. The
JointTokens k feature (log10 k / 3) is unchanged and spans -0.33..0.39 over the new range; the inertia feature was
recentred for 1e-7..1e-4 kg m^2. `tools/bo_eval` and `tools/bo_optim` still describe theta in (omega, zeta) and are
protected: they refer to the run-010 hand and need their own update before any BO on this branch.

**Force limits (N m).** Fingers from the survey maxima: MCP 2.5, PIP 1.5, DIP 0.7; thumb base 4, thumb end 1.5, wrist 6
(spike-2 tuning values, flagged as future theta candidates); shoulder / elbow 100 / 60 (human maxima, velocity drives).

**Self-collision.** `ignoreAllSelfCollision` is off: only parent-child, palm-finger-base and forearm-palm pairs are
ignored; finger-finger, thumb-finger and fingertip-palm contacts are real (the harness counts link-link collision
callbacks). Contact offset 3.6 mm (0.01 m project default x modelScale) on every link and on the object.

**Calibration at the reference k** (index base step to -30 deg, `results/011_artic2/s2_calibrate.csv`): k 1.732 N m/rad,
I 3.24e-5 kg m^2, derived omega 231 rad/s, rise 10-90 % 20 ms (theory 9 ms, i.e. two 10 ms steps), overshoot 2.1 %,
steady -30.2 deg, flexion toward the palm normal.

**Result of the gates: STOPPED at G1.** With the survey stiffness and limits the human-scale hand does not hold any mass
(0.2 / 0.6 / 1.5 kg 0/3 each; also 0/2 at 20, 50 and 100 g). Mechanism from the contact traces: the scripted envelope
closes the thumb and the fingertips onto the cylinder's distal side first, their contact normals point along the palm
toward the wrist, the palm surface never touches the object (2-7 mm gap), and the object slides out past the heel of the
hand and tips over. The same failure survives self-collision off, 5 ms steps, 32 / 16 solver iterations, contact offset
3.6 mm, three softer target sets, three placements (72 / 85 / 100 / 115 mm from the palm pivot), stiffness x 3.46 (top of
the survey range), targets at the joint limits, and closing on a dynamic object from the first step. The closed fist
also shows the middle finger blocked at -60 deg by the converging index and ring fingers (16 of 91 non-ignored pairs
overlapping, worst 7.8 mm); no pair overlaps at the open pose. Decision needed on the grasp geometry (finger flexion
planes fan by up to 48 deg across the hand; thumb sweeps along the palm rather than across it) before G1-G6 can mean
anything. The obsolete kinematic `LiftHarness` was deleted.

## Articulated hand, spike 3 (2026-09-22, branch `articulated-hand`): anatomical audit and anchor-frame rebuild

**Audit (harness mode `audit`, `results/011_artic2/rig_audit_before.txt`).** The generated skeleton inherited the
animation armature's bone frames: finger flexion axes tilted 7-39 deg out of the palm plane (pinky middle / end 39 and
37 deg) and fanned 32 deg in the plane; the rest pose carried up to 34 deg of curl (pinky dorsal, index tip palmar) and
the pinky pointed 13.5 deg toward the thumb; the thumb flexed in the finger plane (axis 143 deg from the middle finger
axis, no opposition), its pad landing 42 mm from the index tip at full flexion; the palm link's twist axis was the palm
normal, so "wrist flexion" was radial / ulnar deviation. Flexion signs, joint limits, link masses and capsule radii were
fine. Link proportions (index 30 / 25 / 18 mm vs human 39 / 21 / 15; middle finger's middle phalanx longer than its
proximal), base-pivot spacing 26 / 19 / 26 mm and the 16 mm palm box are armature artefacts that need re-authoring.

**Rebuild (`ArticulatedHand.anatomicalAxes`, `restSplayDeg`, `convergenceDeg`, `oppositionTargetPalmMm`).** Finger
chains are rebuilt straight in the palm plane from the imported knuckle pivots with a rest splay of +8 / 0 / -6 / -14
deg (index..pinky, + = toward the thumb; no measured source, inside the abduction ranges of the survey), one link frame
per finger (local X = palm normal, Y = finger, Z = nominal flexion axis), flexion planes tilted +2 / +3 / +8 / +13 deg
from the palm's long axis toward the thumb (half of the derived Lister cascade, `results/011_artic2/hand_axes_survey.md`),
both thumb joints flexing in the plane through the thumb's rest direction and an opposition target at (138, 46, 28) mm
(along, out, across) in the palm frame, and the palm anchor rotated so twist = flexion / extension (across the palm),
swing Y = pronation, swing Z = deviation (locked). Measured after the rebuild (`rig_audit_after.txt`): axis elevation 0
for all fingers, flexed fingertips drift 2.5 / 3.2 / 9.2 / 12.9 mm toward the thumb, thumb sweeps 104 mm across the
palm (tip at the ulnar palm at its limits), thumb axis 89 deg from the finger axes (Cheema's 45-60 deg is MC1 pronation,
a different measure). Closed fist (`s3_fist_dump.txt`): every finger reaches -70 deg or more (before: middle blocked at
-32 by the thumb / -60 by the neighbours), no finger-finger crossing; remaining overlaps are fingertips pressed into
the palm box and the thumb resting on the flexed index under drive load (2-8 mm, 4 pairs > 2 mm at 32 / 16 solver
iterations).

**G1 on the fixed rig: STOPPED, 0/9** (`g1_lift_s3.csv`, slip dump `g1_slip_s3_dump.txt`, renders `g1_slip_s3_*.png`).
At the forced release the scripted envelope (-75 / -85 / -60) has curled the fingertips under the 5 cm cylinder: the
only contacts are index / middle / ring tips at (along 0.07-0.09, out 0.04) with normals (-0.7, +0.7, 0), pushing the
object toward the wrist and away from the palm; the palm never touches; the thumb (32 / 37 deg of 45 / 60) is blocked
on the index base (12 mm overlap) instead of reaching the object. The object drifts out at 0.05-0.1 m/s and tips.
Per the task rule no target iteration was done. Harness bug fixed on the way: an inserted diagnostic line had split an
if / else so runs without `stiffnessScale` re-enabled morphology randomization (affected only the 20-100 g and
dynamic-object diagnostics of spike 2, which are therefore void).

## Articulated hand, spike 4 (2026-09-22, branch `articulated-hand`): close-until-contact + preload test driver, STOPPED at the sanity gate / G1

**Controller (harness only, `ArticulatedGates` config `contactController`).** Each joint group flexes its drive target
at `closeRateDegPerSec` (90) until its own link reports contact with the object (`ArmGraspAgent.IsGroupTouching`, the
existing read-only flag), then holds `preloadDeg` (12) beyond the contact angle; groups that never touch stop at the
power-grasp caps (MCP 60, PIP 70, DIP 30, thumb 45 / 45). Pinch = index + thumb, other fingers held open. The agent's
own contact gate is blocked during the scripted close by raising its public `requiredContactSegments` (restored at the
release), because it fired 35 steps into the close and released the object before the preload was in. Placement ignores
the thumb links (they lie in front of the palm at rest) and puts the object 115 mm from the palm pivot, under the
proximal phalanges. No training-side file changed except the rig parameters below; the agent's action path, reward,
observations and MorphologyManager are untouched.

**Rig parameters touched (ArticulatedHand).** `oppositionTargetPalmMm` out 46 -> 90 mm; new `thumbRestAbductionDeg`
(100) and `thumbRestPalmarDeg` (0): the thumb chain is rebuilt from its imported base pivot along an abducted rest
direction so it stays proximal of the object.

**Result: the sanity gate fails and G1 is 0/9 with and without the thumb** (`results/011_artic2/s4_sanity_*`,
`g1_s4_nothumb.csv`, slip dump `g1_s4_slip_dump.txt`, renders `g1_s4_slip_*.png`). The controller does form a wrap:
6-8 contacts, all four fingers on the cylinder, 0.3-0.5 N m grip, no self-overlap. But every contact normal points along
the palm toward the wrist or away from the palm: the object rests ON the proximal phalanges (centre 49 mm in front of
the knuckle axis, because the finger capsules' palmar flesh offset is 18 mm and the palm face 22 mm from the bone axes)
and the 73-91 mm fingers reach only to 60 mm out, below the object's top (76 mm), so the middle and tip segments meet its
distal side instead of wrapping over it; the palm never touches. The thumb, whose pivot is the MCP-level bone 31 mm in
front of the palm with no CMC joint, cannot pass over the object in any fixed plane: it is blocked at 13-21 deg during
the close and shoves the object distally at release (thumb contact normals (1.0, 0.07, 0)). Both are collider /
armature geometry defects (palmar flesh offsets, palm thickness, missing CMC), outside anchor-frame fixes; no preload /
cap tuning was done.

## Articulated hand, spike 5 (2026-09-22, branch `articulated-hand`): spec-driven skeleton (HandSpec) — gates pass

**HandSpec.** The finger, thumb and palm skeleton is generated from a serialized anthropometric table
(`ArticulatedHand.spec`, class `HandSpec`, dump in `results/011_artic2/handspec_reference.txt`, sources in
`hand_anthropometry_survey.md` and `hand_axes_survey.md`) instead of the armature bones; the bone capture is kept only
for the arm (modelScale 0.36 still scales the armature) and for the mesh follow. Per finger: PP / MP / DP lengths
(Santoso 2026), MCP joint centre on the metacarpal-head arch (21 mm spacing derived from breadth 85 mm; middle most
distal, little 13 mm proximal), rest abduction +8 / 0 / -6 / -14 deg, Lister-cascade flexion-plane tilt +2 / +3 / +8 /
+13 deg, AAOS limits, capsule radii 9 / 8 / 7.5 mm with the palmar skin 10 mm from the bone axis, 4 mm tip pad, density
1000. Thumb: CMC (trapezium) at (25, 5, 32) mm from the wrist pivot, MC1 46 / PP 30 / DP 21 mm, open rest pose 60 deg
palmar / 35 deg radial abduction, MCP fixed at 20 deg (a FixedJoint link whose contacts and mass count with the CMC
group; 14 driven groups unchanged), IP driven; the single CMC opposition DoF is the normal of the plane through the MC1
rest direction and an opposition target on the outer surface of a 5 cm object over the palm centre (measured axis
(0.47, -0.76, 0.44) in the palm frame, 62 deg from the finger axes). Palm: 85 x 104 x 28 mm box with the palmar face
10 mm from the metacarpal plane, mass 0.30 kg.

**theta <-> spec mapping.** `MorphologyManager.lengthScale[f]` multiplies the spec phalanx lengths of finger f (thumb:
PP and DP, not MC1); k / zeta / I as in spike 2 (I from the generated link masses, including the thumb PP extra link);
the mask and the drive plumbing are unchanged. Future theta candidates exposed by the spec: thumb CMC position and
opposition target / axis, thumb rest abduction, MCP spacing and arch offsets, rest abduction and cascade tilts, capsule
radii and palmar flesh offset, palm thickness / face offset, phalanx ratios, joint limits (still mirrored in
ArmGraspAgent's limit fields, which remain the drive authority).

**Anthropometrics (bone-derived spike 2 -> spec -> target).** Hand length 187.5 -> 194.1 -> 190 mm; palm width 82.3 ->
85 (spec; 80.5 across the index / pinky capsules) -> 85; hand mass 0.420 -> 0.411 -> 0.40-0.45 kg; index links 33.9 /
28.1 / 18.5 -> 39.2 / 21.3 / 15.3 (+4 pad); middle 26.9 / 34.8 / 21.4 -> 43.6 / 25.8 / 16.2; ring 31.0 / 29.2 / 18.0 ->
41.0 / 24.5 / 16.6; pinky 27.3 / 27.6 / 12.9 -> 31.9 / 17.1 / 14.9; thumb 37.9 / 48.6 -> 46 / 30 / 21. Mesh-follow
deviation between the imported bone pivots and the generated link pivots: index 19, middle 18, ring 6, pinky 42,
thumb 35 mm (cosmetic; the skinned mesh follows the links).

**Arm servo.** The shoulder / elbow PhysX Velocity drives (damping 1e6) cannot hold a static load: an implicit velocity
drive only cancels the velocity gained in one step, so the joint sags at g dt / I. On the giant arm this was invisible; at
human scale the elbow drifted 8 deg/s under 4 N m with a 200 N m limit and set every held object back down. Shoulder
and elbow are now Force drives whose position target integrates the commanded velocity (clamped to the limits and to
5 deg of lead); the agent's velocity-action semantics are unchanged. Arm force limits back to 300 / 200 N m.

**Gates (10 ms, solver 16 / 4, contact offset 3.6 mm, close-until-contact controller: 90 deg/s, preload 12 deg, caps
60 / 70 / 30, thumb 45 / 45, object 80 mm from the palm pivot, lift to 8 cm).** G1 9/9 at 0.2 / 0.6 / 1.5 kg (8-10
contacts, palm contact, 500-step holds), 0/3 at 2.0 and 2.5 kg. G2 at 0.6 kg 3/3 at every scale from 0.25 to 4.0
(peak pulse 34 N = 5.8 mg). G3 pinch (index + thumb): 3/3 at 0.2 / 0.6 / 1.0 kg, 1/3 at 1.5 kg; 3/3 to scale 3.0, 2/3
at 4.0 -> strictly below the envelope on both axes. G4 20 draws x 2000 steps over the k / zeta / I / mask ranges: 20/20
finite at 10 ms (max joint speed 3.9-14.2 kdeg/s, penetration <= 12.5 mm) and 20/20 at 5 ms (6.6-24.0 kdeg/s,
<= 8.3 mm); 10 ms kept. G5 8-env headless probe on the Dynamic_Scene_Train player: 20k decisions in 43.9 s, 504
decisions/s steady state (5k-20k), ratio 1.05 vs the kinematic 481. G6 with every drive at its limit on the 0.6 kg
cylinder (grip 4.6 N m): penetration 22.9 mm, held 10 and 20 N toward and away from the palm, pushed out at 40 N;
one tuning pass at 32 / 16 solver iterations: 21.6 mm, held 10 N, out at 40 N (no improvement: the fist targets drive
the fingertips into the object at the force limits; the controller's preloaded grasp penetrates 10-17 mm in G1 / G2).
Fist check: all fingers reach -70 to -90 deg, no finger-finger crossing; the thumb placed over the flexed middle finger
presses into it 6-8 mm under 0.27 N m (contact under load, not a crossing).

**Separation.** ArmGraspAgent.cs zero diff. MorphologyManager.cs: only the extra-link mass in the subtree inertia.
Harness: trace-to-file logging, arm drive state in the trace, `agent.MaxStep` raised to 20000 for scripted holds,
placement 80 mm.

## Articulated hand, spike 6 (2026-09-22, branch `articulated-hand`): contact penetration tuning — TGS adopted

**Measurement.** The harness samples the deepest hand-link / object overlap (Physics.ComputePenetration over the 14
groups, the thumb proximal phalanx and the palm) every step of a window: the 500-step hold in the G1 0.6 kg preload trial
(reference hand, close-until-contact controller, preload 12 deg) and the squeeze + push phases of the G6 trial (every
drive at its force limit, 10 N push). Rows are single trials unless noted; PGS trial-to-trial noise is about +-2 mm
(3 seeds: 8.7 / 9.0 / 7.0 mm max), so the single-lever rows for object iterations, contact offset and depenetration
velocity are within noise of the baseline. Steps/s = physics steps per wall second in the Editor at timeScale 20.

| setting (one lever at a time) | preload max / p95 (mm) | limits max / p95 (mm) | contacts | grip N m | steps/s |
|---|---|---|---|---|---|
| baseline PGS, hand 16/4, object 6/1, offset 3.6 mm, depen 10 | 8.7 / 8.3 | 18.8 / 17.1 | 9 / 7 | 0.90 / 4.58 | 1336 / 1270 |
| object solver iterations 16/4 | 14.6 / 14.1 | 18.8 / 17.1 | 10 / 7 | 1.04 / 4.58 | 1334 / 1225 |
| object solver iterations 32/8 | 10.3 / 10.3 | 8.8 / 8.2 | 8 / 8 | 1.29 / 2.44 | 1210 / 1105 |
| contact offset 2 mm | 8.7 / 8.1 | 18.7 / 17.0 | 10 / 8 | 1.05 / 4.03 | 1430 / 1352 |
| contact offset 5 mm | 9.8 / 7.9 | 17.5 / 16.0 | 9 / 10 | 0.88 / 4.60 | 1179 / 1144 |
| max depenetration velocity 20 | 14.6 / 14.1 | 18.8 / 17.1 | 10 / 7 | 1.04 / 4.58 | 1364 / 1281 |
| max depenetration velocity 50 | 14.6 / 14.1 | 18.8 / 17.1 | 10 / 7 | 1.04 / 4.58 | 1354 / 1268 |
| **Temporal Gauss-Seidel solver (project setting), everything else baseline** | **2.1 / 2.1** (3 seeds 1.8 / 1.8 / 1.6) | **3.3 / 3.3** | 5 / 7 | 1.07 / 4.48 | 1462 / 1386 |

The PhysX guide explains the result: TGS "minimizes energy introduced when correcting penetrations" and, with N
position iterations, runs N solver substeps, so the stiff finger drives no longer reach their targets inside a single
contact solve (results/011_artic2/contact_solver_survey.md). Object-side iterations do nothing on their own because the
island already runs the articulation's 16 / 4 (PhysX uses the highest count in the island); depenetration velocity
never binds (the forum report that it cannot be raised above 10 after init is consistent with the identical rows).
Lever 5 (finger drive damping) was not needed.

**Adopted:** `ProjectSettings/DynamicsManager.asset` m_SolverType 1 (TGS). It is the only lever that reaches the gate
(<= 5 mm preload, <= 8 mm at the limits) and it is a single project setting; no runtime API exists for it. Everything
else stays at the spike-5 values (hand 16 / 4, contact offset 3.6 mm, depenetration 10 m/s, 10 ms).

**Post-adoption gates (TGS).** G4 20/20 finite (max joint speed 5.7-36.6 kdeg/s, episode max penetration <= 16.7 mm in
the random-target sweep against the kinematic object). G5 8-env headless: 20k decisions in 41.6 s, 544 decisions/s
steady state, ratio 1.13 vs 481 (TGS is not slower here). G1 9/9 at 0.2 / 0.6 / 1.5 kg with 500-step holds, hold-window
penetration <= 1.9 mm; 3/3 at 2.0 and 2.5 kg as well (PGS: 0/3). G2 at 0.6 kg 3/3 at every scale 0.25-4.0, window
penetration <= 3.1 mm: the max survived perturbation scale is still >= 4.0 (5.8 mg), so the earlier strength number was
not a penetration artefact. G6: penetration at the force limits 3.5-5.5 mm (window; 11 mm episode max during the close),
held 10, 20 and 40 N toward and away from the palm, pushed out at 80 N (PGS: out at 40 N).
