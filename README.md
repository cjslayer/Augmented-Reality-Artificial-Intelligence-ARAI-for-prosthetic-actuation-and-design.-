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

**Current reward** (run 008 onward, quality-graded):

- **Shaping** — 15 potential-based terms (14 segment-to-cylinder distances + the palm grasp-point
  distance), each normalized by its episode-initial value (floor 0.05 m) and summed with scale 1/15,
  so the shaping budget is at most ~1.0 per episode regardless of spawn distance.
- **Quality Q ∈ [0, 1]**, paid as Q/50 on each step where the hold criterion is met, capped at 50
  paying steps per episode (so cycling grip/release cannot farm it):
  0.35 saturating contact count (saturates at 8) + 0.30 azimuthal coverage (0 while the largest
  angular gap between contacts is ≥ 180°, ramping to 1) + 0.20 thumb antipodality (thumb azimuth vs
  mean finger azimuth, peak at 180°) + 0.15 palm contact. The palm collider is detected for Q only
  and never counts toward the success gate.
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
| **008** | 2M | Quality-graded reward (above), init from 007, K = 10 fixed | 20/20 held grasps; see below |

Model files live under `results/<run>/` (not tracked); the deployed policy is
`Assets/Models/Prosthetic.onnx` (currently run 008).

## Run 008 results

Training (8 headless environments, 87 min):

| Metric | 007 (last 10 summaries) | 008 start | 008 end |
|---|---|---|---|
| Cumulative reward | 7.23 (old reward) | 2.39 | 2.46 |
| Reward std across episodes | 1.64 | 0.08 | 0.06 |
| Episode length (decisions) | 16.3 | 17.5 | 16.4 |
| Return components: shaping / quality / bonus / penalty | — | 0.90 / 0.50 / 1.00 / −0.02 | 0.90 / 0.57 / 1.00 / −0.02 |
| Quality Q at hold completion | — | 0.51 | 0.59 |
| Palm contact at hold | — | 36% | 88% |
| Contacts at hold | — | 9.0 | 8.5 |
| Coverage gap (deg) | — | 206.6 | 205.5 |
| Thumb antipodality | — | 0.57 | 0.58 |

Under the previous reward, ~86% of the return was travel-distance shaping that scaled with spawn
distance (reward std 1.64). Under the quality-graded reward the spawn dependence is gone (std 0.06),
and the policy adapted mainly by adding palm contact. The coverage term never paid: with this hand
and a 0.13 m diameter cylinder the contacts never span more than half the circumference.

Editor evaluation (20 episodes, capsule target, random spawn and yaw):

| | |
|---|---|
| Held grasps | 20/20, median 80 physics steps |
| Spearman(return, mean Q over paid steps) | 0.74 (Pearson 0.86) |
| Spearman(return, drop-test pass fraction) | 0.61 |
| Drop-test pass fraction | mean 0.13; 4/20 episodes ever caged the cylinder (1 passed 3/3, 2 passed 2/3, 1 passed 1/3) |

The drop test is evaluation-only: at hold completion the policy is frozen, the arm made kinematic,
the cylinder made dynamic with gravity, and physics stepped for 2 s; pass = cylinder centre displaced
less than 0.1 m. Three repeats per episode; in 3 of 20 episodes the repeats disagreed even with
Enhanced Determinism enabled. Return ranks the reward's own quality score well, but that score is
only weakly tied to physical caging for this object: the learned grasp is a pinch plus palm contact
rather than a wrap. Per-episode CSVs are in `results/008/`.

Verification of the reward before training: normalized shaping telescopes to the analytic sum
within 3×10⁻⁵; a scripted hold-45/release cycler earns 0.18 vs 2.17 for a completing grasp; a
one-sided contact stack scores lower Q than the reference grip (0.22 vs 0.28 physically,
0.35–0.38 vs 0.56–0.71 on synthetic contact sets).

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
| `Assets/Models/Prosthetic.onnx` | Deployed policy (run 008) |
| `Config/` | Trainer configurations |
| `tools/deploy_model.cmd` | Copies `results/<run>/Prosthetic.onnx` into the scene asset |
| `MLAGENTS_UPGRADE.md`, `context.md` | Notes on the ML-Agents 4.1 upgrade and the tooling setup |
