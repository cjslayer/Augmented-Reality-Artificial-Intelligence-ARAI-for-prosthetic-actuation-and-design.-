# ARAI — Augmented Reality Artificial Intelligence for prosthetic actuation and design

A Unity / ML-Agents simulation in which a reinforcement-learning policy drives a five-axis arm and a
14-joint prosthetic hand to reach for and grasp a cylinder. The repository holds the Unity project,
the trainer configuration, the deployed policy, and the evaluation tooling used for the paper.

## Environment

| | |
|---|---|
| Unity | 6000.3.18f1 (URP, OpenXR) |
| ML-Agents | `com.unity.ml-agents` 4.1.0 in Unity; Python trainer built from the `develop` branch (`mlagents 1.2.0.dev0`, PyTorch 2.8 CPU) |
| Scene | `Assets/Scenes/Dynamic_Scene.unity` |
| Agent | `Assets/Scripts/ArmGraspAgent.cs` on the `ArmAnimation` object (behavior `Prosthetic`) |
| Observations / actions | 55 continuous observations; 19 continuous actions (14 finger joint groups + shoulder flexion, shoulder abduction, elbow flexion, wrist flexion, wrist pronation) |
| Target | Capsule collider, radius 0.0647 m, height 0.776 m (visual mesh matched); spawned in a 0.25 m disk 1.1–1.5 m from the shoulder with random yaw |
| Decision period | 5 physics steps (0.02 s each); MaxStep 5000 |

Joint motion is kinematic with a penetration clamp: every commanded rotation is checked with
`Physics.ComputePenetration` against the target and bisected back to the contact angle, so the hand
never interpenetrates the object (measured max penetration 0.48 mm).

## Task and reward

**Success criterion** (unchanged since run 004): at least 6 finger segments touching the cylinder,
at least 2 distinct non-thumb fingers, the thumb touching, held for 10 consecutive decisions
(50 physics steps). Success pays +1 and ends the episode.

**Current reward** (quality-graded; introduced in run 008, quality term revised in run 009):

- **Shaping** — 15 potential-based terms (14 segment-to-cylinder distances + the palm grasp-point
  distance), each normalized by its episode-initial value (floor 0.05 m) and summed with scale 1/15,
  so the shaping budget is at most ~1.0 per episode regardless of spawn distance.
- **Quality Q ∈ [0, 1]**, paid as Q/50 on each step where the hold criterion is met, capped at 50
  paying steps per episode (so cycling grip/release cannot farm it). Since run 009:
  0.30 wedge posture (vertical spread of the contact heights, palm low / fingertips high:
  clamp((spread − 0.42 m) / 0.03 m), paid only while the palm touches and multiplied by an
  opposition gate clamp(antipodality / 0.5); a one-sided contact set can relieve all depenetration
  by lateral translation, so spread without opposition earns nothing)
  + 0.25 saturating contact count (saturates at 8)
  + 0.20 palm contact
  + 0.15 azimuthal coverage (credit ramps from a 240° largest gap to full at 180°, the reachable range for this hand and object)
  + 0.10 thumb antipodality (thumb azimuth vs mean finger azimuth, peak at 180°).
  Run 008 used 0.35 contacts / 0.30 coverage from 180° / 0.20 antipodality / 0.15 palm. The palm collider is
  detected for Q only and never counts toward the success gate.
- **Bonus** +1 on success; **time penalty** −1/MaxStep per step.

Maximum return ≈ 3.0. Per-episode statistics (`Grasp/*`, `Return/*`) are sent to the ML-Agents
StatsRecorder and, optionally, to a CSV (`statsCsvPath`).

## Run history

| Run | Steps | Change | Result |
|---|---|---|---|
| 004 | 2M | Contact/success termination, static target | First grasps; 20/20 Editor successes |
| 005 | 2M | Random target position and yaw, 5-axis arm reach | Reaches ≥6 contacts but never holds |
| 006 | 6M | Hold-length curriculum K = 2→10, init from 005 | 19/20 held grasps, box-collider failure at yaw 162° |
| 007 | 6M | Box collider → capsule (yaw-invariant target), init from 006 | 41/41 held grasps, episodes ~16 decisions |
| 008 | 2M | Quality-graded reward (normalized shaping + budgeted hold Q), init from 007, K = 10 fixed | 20/20 held grasps; drop-test pass 0.49 at μ = 1.0 |
| **009** | 2M | Q revised from the drop-test analysis: opposition-gated wedge posture term, coverage recalibrated; init from 008 | drop-test pass 0.86 at μ = 1.0 (held-out seeds); see below |

Model files live under `results/<run>/` (not tracked); the deployed policy is
`Assets/Models/Prosthetic.onnx` (currently run 009).

## Evaluation: drop test as a pass-vs-μ curve

Grasps are evaluated physically, not by the reward. At hold completion the policy is frozen, the
arm Rigidbody is made kinematic, the platform collider is disabled, the cylinder is made dynamic with
gravity (its FreezeAll constraints cleared), and physics is stepped for 2 s at the fixed 0.02 s step;
pass = cylinder centre displaced less than 0.1 m. Three repeats per episode from the identical
pre-drop state, Enhanced Determinism on. Contact friction is applied through a runtime material on
the cylinder (Maximum combine, verified to change the resolved pair friction) and swept over
μ = 0.6 / 1.0 / 1.5; **μ = 1.0 is the primary coefficient**, taken as representative of silicone
prosthetic fingertip surfaces (literature citation pending), with 0.6 and 1.5 as the robustness sweep.
The curve is reported for every policy on the same seed list.

### Runs 008 and 009 on held-out seeds 2001–2100 (100 episodes each, all reached the hold)

Mean per-episode pass fraction with bootstrap 95% CIs; the last column is the paired per-seed
difference.

| μ | 008 | 009 | paired 009 − 008 |
|---|---|---|---|
| 0.6 | 0.213 [0.147, 0.287] | 0.603 [0.517, 0.683] | +0.390 [+0.297, +0.483] |
| **1.0** | 0.487 [0.387, 0.577] | **0.857 [0.797, 0.910]** | +0.370 [+0.280, +0.460] |
| 1.5 | 0.777 [0.700, 0.847] | 0.980 [0.960, 0.997] | +0.203 [+0.127, +0.283] |

At μ = 1.0, 009 passes at least one repeat in 94% of episodes and all three in 75% (008: 52% and
45%).

**Failure mode.** Every failure, for both policies and at every μ, is an axial slide: the cylinder
slips straight down its own axis out of the hand (median axial share of the first 0.3 s of motion
0.997; lateral escapes 0 of 300 repeats in the 008 baseline study). Of the 300 repeats at μ = 1.0,
008 shows 146 pass / 131 axial / 23 slow-axial; 009 shows 257 pass / 4 axial / 38 slow-axial /
1 lateral. Raising friction alone on identical 008 grasps moved the pass fraction from 0.13 to
0.53 to 0.83 (μ 0.6 / 1.0 / 1.5, seeds 1001–1100), so the residual failures are friction-capacity
failures, not caging failures.

**Per spawn band (μ = 1.0, mean pass fraction).** The far band was a zero-pass regime for 008; 009
reaches it.

| Shoulder-to-cylinder distance | 008 | 009 |
|---|---|---|
| [1.00, 1.25) m, n = 33 | 0.919 | 0.949 |
| [1.25, 1.40) m, n = 51 | 0.359 | 0.863 |
| [1.40, 1.60) m, n = 16 | 0.000 | 0.646 |

**Mechanism (spread → force → survival).** Contact impulses logged during the first steps after
release (`ContactPoint.impulse`, normal component; the tangential component is not reported by this
Unity version) show that at release every grasp has friction capacity above the 9.8 N weight
(008 failers 22 N, passers 28 N). What separates them is retention: in failing grasps the summed
normal force decays from 36 N to 1.4 N within 0.3 s while the fingers stay in geometric contact, in
passing grasps it settles near 37 N, carried mainly by a load-bearing thumb (19 N vs 6 N). Among
palm-present grasps the vertical spread of the contact heights (palm low, fingertips high) predicts
the solver's normal force at release (Spearman 0.91) and at 0.1 s (0.70), and the drop outcome at
μ = 1.0 (AUC 0.91): the palm-low / fingertips-high posture is the configuration in which the
depenetration forces cannot be relieved by translating the cylinder, so the squeeze persists and
friction can carry the weight. This is the basis of the wedge term in Q. On the held-out seeds 009
raised the summed normal force at release from 38 N to 54 N and at 0.1 s from 16 N to 35 N, with
vertical spread 0.413 → 0.451 m (wedge credit 0.46 → 0.97), palm contact 89% → 98%, antipodality
0.58 → 0.65, coverage gap 206° → 197°, contacts 8.45 → 7.35. Within 009 the spread-to-force
Spearman is 0.48 [0.29, 0.64], weaker than 008's 0.89 because 009's spread sits at saturation; the
force and the pass rate rose together, so the term was not gamed. Palm-less grasps fell from 11/100
to 2/100 (both 009 palm-less grasps pass at μ = 1.0). Within 009, Spearman(mean Q, pass fraction) is
0.07: Q is saturated at 0.83 and no longer ranks episodes.

Training for 009 (2M steps from 008, 86 min): reward 2.55 → 2.71, Q at hold 0.70 → 0.87, wedge
0.45 → 0.94, entropy 1.01 → 0.97, success 1.00 throughout. Per-episode CSVs: `results/009/validation/`
(held-out evaluation, both policies), `results/008/eval100/` and `results/008/controls/` (the 008
failure analysis, friction sweep, spawn pins and force logs), `results/009_checks/` (exploit checks).

Reward checks before training 009: normalized shaping telescopes to the analytic sum within
4×10⁻⁵; a scripted hold-45/release cycler earns 0.11 vs 1.98–2.09 for a completing grasp; a
one-sided stack scores Q 0.156 vs 0.195 for the scripted reference grip; a cylinder resting on
passive curled fingers scores 0.063 without meeting the gate; on synthetic contact sets a
gate-passing one-sided straddle scores 0.446 against 0.861 for a reference wrap (0.696 before the
opposition gate), and 100% of real drop-test passers retain full wedge credit under the gate.

Seeds 1001–1100 (analysis) and 2001–2100 (this comparison) have both been used for model
decisions; final paper numbers will need a further held-out seed set.

## Run 010: morphology-conditioned policy (2026-09-16)

Run 010 trains a fresh lineage with a per-episode morphology vector theta (per-finger link-length scales 0.8-1.2,
per-joint spring omega/zeta/inertia, a 14-bit finger actuation mask), impedance actuation with an exact discrete
integrator, and morphology-conditioned observations (22-float vector + a 16 x 13 per-joint token BufferSensor with
attention). 6M steps, hold-decision curriculum 2 -> 10. Held-out evaluation on seeds 3001-3100 with random theta:
grasp success 0.90, drop-pass given hold 0.59 / 0.73 / 0.81 at mu 0.6 / 1.0 / 1.5, no theta bin collapsing (details
and theta-binned tables in `results/010/validation/THETA_REPORT.md`, methods in `MLAGENTS_UPGRADE.md`). The grasp-point
reference used for reach shaping was moved to the robust centre of a forced-close placement grid (x scaled by the
finger-length ratio); runs 004-009 trained with shaping aimed at a marginal point. The deployed model remains run 009.

## Training

The Python trainer is a source build (`C:\Users\chris\ml-agents`, Python 3.10 venv). Train against
the headless player rather than the Editor (about 3.4× faster); the player must be built with the
agent's Behavior Type set to **Default** (the committed scene uses InferenceOnly for the demo).

```
mlagents-learn Config/run_008.yaml --run-id=008 --env Builds/Prosthetic/Prosthetic.exe --num-envs 8 --no-graphics
```

`Config/trainer_config.yaml` holds the shared PPO hyperparameters and the hold-length curriculum;
per-run files such as `Config/run_008.yaml` set `init_path` and `max_steps`. GPU training was
benchmarked slower than CPU for this network (2×128 MLP, vector observations).

Deploy a trained model into the scene with:

```
tools\deploy_model.cmd <run-id>
```

## Repository layout

| Path | Contents |
|---|---|
| `Assets/Scripts/ArmGraspAgent.cs` | Agent: joints, penetration clamp, reward, quality score, episode stats |
| `Assets/Scenes/Dynamic_Scene.unity` | Training / demo scene |
| `Assets/Models/Prosthetic.onnx` | Deployed policy (run 009) |
| `Config/` | Trainer configurations |
| `tools/deploy_model.cmd` | Copies `results/<run>/Prosthetic.onnx` into the scene asset |
| `MLAGENTS_UPGRADE.md`, `context.md` | Notes on the ML-Agents 4.1 upgrade and the tooling setup |
