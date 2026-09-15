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
