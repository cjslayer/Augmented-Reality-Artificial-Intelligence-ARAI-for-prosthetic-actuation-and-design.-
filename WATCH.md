# Watching run 011 in the Editor (branch `articulated-hand`)

Scene model = run 012 (12M). Held-out on fresh seeds 6001-6100 (K=50, perturb 1.0): success 0.89/0.83/0.79 at mu 0.6/1.0/1.5; paired vs 011b on 5001-5100: +0.45/+0.41/+0.48. 011b remains tagged `011b-baseline`.
(`Dynamic_Scene`, InferenceOnly; `Dynamic_Scene_Train` stays Default with no model for training builds.)

Three Editor-only modes, all compiled out of player builds. The training scene is `Assets/Scenes/Dynamic_Scene_Train.unity`
(the agent's behavior stays Default; the modes override it at Play time). Nothing here changes the reward, observations
or actions, and none of it touches the headless run.

## A. Checkpoint theater — watch the REAL run (`Assets/Scripts/Diagnostics/CheckpointTheater.cs`)

**OFF by default** (it costs about 35 % of the headless run's throughput while the Editor is in Play mode). The run that
is training now is `011b` (the resume of 011 from 2.0M with the K-derived budget), so use `"runId":"011b"` below;
`results/011b/Prosthetic/` holds its checkpoints and `results/011b/run_logs/training_status.json` its lessons. Turn it on
by creating the file and pressing Play; turn it off by stopping Play (delete the file to keep it off next time).

1. Create `Temp/theater.json` in the project folder:
   ```json
   {"runId":"011","refreshMinutes":10,"behavior":"Prosthetic"}
   ```
2. Press Play in `Dynamic_Scene_Train`. The theater switches the agent to InferenceOnly, turns per-episode theta
   resampling on, imports the newest `results/011/Prosthetic/Prosthetic-<steps>.onnx` into `Assets/Models/Watch/` and hands
   it to the agent with `Agent.SetModel` (a runtime hot-swap: Play mode is not left). Every `refreshMinutes` it looks for a
   newer checkpoint and swaps again; F5 forces a check. The panel at the top right shows the loaded checkpoint step, the
   current curriculum lesson (read from `results/011/run_logs/training_status.json`, and applied to the perturbation scale
   so the pulses match the trainer's lesson) and the time to the next refresh.
3. Guard: a model whose inputs do not match the scene (vector observation 24, JointTokens buffer 16 x 13) is refused and
   logged; the previous model stays loaded. Checkpoints appear every 250k steps (`checkpoint_interval`), so the first
   swap happens once the run has passed 250k.
4. Delete `Temp/theater.json` to return the scene to normal. The imported `.onnx` copies under `Assets/Models/Watch/` can be
   deleted at any time (gitignored is recommended; they are not the deployed model).

## B. Live demo — train from scratch, visibly, in the Editor

Separate run id, never merged with 011, throughput ~1 environment at Editor frame rate (about one tenth of the 8-env
headless run), so it is for showing the loop, not for producing a policy.

```
C:\Users\chris\ml-agents\venv\Scripts\mlagents-learn.exe Config\run_011.yaml --run-id 011_demo --force --results-dir results
```
then press Play in `Dynamic_Scene_Train` within ~60 s. No `--env`: the Editor is the only environment (it connects on
the Editor port 5004, so it does not collide with the headless run's players on 5005+). Stop with Play off, then Ctrl-C in
the trainer window. Curves land in `results/011_demo` (TensorBoard: `tensorboard --logdir results`).

## C. Physics view — see the physically real hand (`Assets/Scripts/Diagnostics/PhysicsViewOverlay.cs`)

Press **F2** in Play mode (or create an empty file `Temp/physview` to start with it on). Cyan = the articulation's real
colliders (finger and thumb capsules, palm box, forearm and bicep capsules), yellow = the object's collider. The skinned
mesh follows the links with up to 42 mm of lag on the pinky (the mesh was authored on the old bone layout), so the cyan
shapes are what actually touches the object. Works together with A and with the morphology overlay.

## What the morphology overlay shows (`MorphologyWatchOverlay`, always on in Play mode)

Per active agent: episode number and step, successes so far, `randomize ON/OFF`, the five finger length scales of this
episode, active groups / 14 and the masked groups, mean finger stiffness k (N m/rad) with the derived omega, palm contact
and per-fingertip contact flags, contact count / 14.

## What to point out to an advisor

- **Phase machine**: reach (fingers open, object kinematic on the pedestal) -> contact gate (6 finger segments on 2
  fingers plus the thumb: +0.1, object released) -> lift (object bottom >= 3 cm above the pedestal) -> hold 50 decisions
  (5 s) with three random wrench pulses of up to 1.5 x the object's weight -> +1. A drop (11 cm from the grasp point, 60
  deg tilt or below the pedestal) or a budget miss ends the episode with -0.2.
- **theta changes every episode**: the overlay's length scales, active groups and stiffness change each reset; the
  physics skeleton is regenerated from the anthropometric spec with those scales (F2 shows it).
- **Contacts**: the overlay's fingertip flags and count; the gate counts finger segments only (palm and forearm are shown
  but do not count).
- **Perturbation pulses**: during the hold the object is kicked three times; the curriculum lesson in the theater panel
  is the pulse scale the trainer is currently asking for (0 -> 0.25 -> 0.5 -> 0.75 -> 1.0 of 1.5 x weight).
- **Drop vs success**: success ends with the +1 (successes counter increments); a drop ends the episode immediately.
- **Object mass**: log-uniform in 0.2-1.5 kg per episode, in the observations.
