# Run 014 phases C / D: lying-focused continuation of 014_r2 (brief 2026-10-08)

This continues run id 014_r2 within its lineage: it resumes from the 24M checkpoint into phase C (lying only), then phase D (50/50 restore).
Every phase uses fixed environment parameters and has no curriculum block.

## Step 0: preflight (2026-10-08)

**Git and processes.**
- HEAD is 2fadab9. The only working-tree changes are the standing Editor-noise files (ProjectSettings, UserSettings, the OpenXR asset, Assets/_Recovery, myo/), which stay uncommitted under the standing rule.
- No trainer, player, Python or Editor process is running. The only Unity processes are the Unity CLI's MCP server and identity service.

**Protected model hashes (sha256):**

| file | sha256 |
|---|---|
| results/014/Prosthetic.onnx (014_r2, 24M) | 164d42736cd7d0d5f80067fe1b1a8c8c28ddd54f5424267dfbe98f0e75bf24cd |
| results/013/Prosthetic.onnx (013 = 013D) | 96ac32e6564fcdbb6b4ddd1dd2a6c4feaf7c93529cd54ebebf8cef7c7b9d0eeb |
| results/013/013D/Prosthetic.onnx | 96ac32e6564fcdbb6b4ddd1dd2a6c4feaf7c93529cd54ebebf8cef7c7b9d0eeb |
| results/013/013E/Prosthetic.onnx | 8a3097a9243fb90010dd82eeaf5b12aae8f63e1a6216d0f0e56ef0e861bc4716 |

**Resume checkpoint.**
- `--resume` loads `results/014_r2/Prosthetic/checkpoint.pt` (`DEFAULT_CHECKPOINT_NAME` in mlagents/trainers/model_saver/torch_model_saver.py).
- Its `global_step` is 24000187.
- It is not byte-identical to Prosthetic-24000187.pt (sha 70b2573a… vs 3cbc282b…), but all 170 entries (policy, critic, optimizer states, global step) are tensor-equal. The bytes differ only in serialization.
- Backed up as `results/014_r2/Prosthetic/checkpoint_Bend_24000187.pt` (sha 70b2573a…, identical to checkpoint.pt).
- Earlier backups still present: checkpoint_A1end_1999977.pt and checkpoint_A2end_2249961.pt.

**013 phase C / D parameters (Config/run_013_phaseC.yaml, run_013_phaseD.yaml):**

| | 013 C (3.0M → 13.5M, 10.5M steps) | 013 D (13.5M → 20M, 6.5M steps) | 014 C (this brief) | 014 D (this brief) |
|---|---|---|---|---|
| pose/canonicalFraction | 0.0 | 0.5 | 0.0 | 0.5 |
| perturb/scale | ladder 0.25 → 0.5 → 1.0 (reward lessons, threshold 0.9, min 100 episodes; cascaded) | 1.0 fixed | **1.0 fixed** (no ladder) | 1.0 |
| hold/decisions (K) | 50 | 50 | 50 | 50 |
| task/liftBudgetSteps | absent: field default 200 | absent: 200 | **300** | 300 |
| object/anchorShare | absent: 0 (cylinder never drawn) | absent: 0 | **0.10** | 0.10 |
| digit composition | 5-digit legacy hand only | same | **six compositions, design shares** (3 .10, 4 .15, 5 .45, 6a .075, 6b .075, 7 .15) | same |
| max_steps | 20M | 20M | **36M** | 36M |
| checkpoint_interval | 250k | 250k | 500k | 500k |
| curriculum block | yes (perturbation) | none | none | none |

013E then ran 20M → 24M as D plus anchorShare 0.10 and lift budget 300. 013E's total is 24M steps, of which 10.5M were lying-only.

Where 014 differs from 013:
- perturbation is fixed at 1.0 instead of the ladder (the brief's choice: 22M steps already at 1.0, and the pulse test shows pulses are not the limiter);
- lift budget is 300 and anchor share 0.10, as in 013E and 014 B;
- 014 has six compositions;
- the learning rate is lower (next section).

**Learning rate at the resume.**
- ML-Agents' linear schedule is `(v0 − vmin) · (1 − step / max_steps) + vmin` (`ModelUtils.polynomial_decay`, power 1; mlagents/trainers/ppo/optimizer_torch.py).
- The minimums are lr 1e-10, epsilon 0.1 and beta 1e-5. Beta and epsilon follow learning_rate_schedule (settings.py), so they are linear too.
- At step 24000187 with max_steps 36M, 1 − step/max_steps = 0.33333:

| | initial | at 24.0M (C start) | at 32M (latest C end) | at 36M |
|---|---|---|---|---|
| learning rate | 3.0e-4 | **1.0000e-4 (33.3 % of initial)** | 3.33e-5 | 1e-10 |
| epsilon | 0.2 | 0.1333 | 0.1111 | 0.1 |
| beta | 0.005 | 1.673e-3 | 5.66e-4 | 1e-5 |

- 33.3 % is above the brief's 10 % floor, so the run continues.
- For comparison, 013 C ran at lr 2.55e-4 → 9.75e-5 (steps 3.0M → 13.5M of max 20M), so 014 C starts at about 40 % of 013 C's starting LR.
- Changing max_steps is the only hyperparameter change allowed.

## Step 1: phase C / D configs and smoke (2026-10-08)

**Configs.**
- `Config/run_014_phaseC.yaml` = `run_014_phaseB.yaml` with:
  - pose/canonicalFraction 0.0;
  - max_steps 36000000;
  - checkpoint_interval 500000;
  - keep_checkpoints 200 (the run folder holds 50 checkpoints, and C + D add at most 24);
  - the episode-log directory changed to `results/014_r2_episodes/C`, so no window crosses a phase switch, as in B.
- Everything else matches B: perturbation 1.0, K 50, lift budget 300, anchorShare 0.10, design digit shares (player default). There is no curriculum block.
- `Config/run_014_phaseD.yaml` = `run_014_phaseB.yaml` with max_steps 36000000, checkpoint_interval 500000, keep_checkpoints 200 and episode directory `results/014_r2_episodes/D`. All env params are identical to B.

**Smoke** (run id `014_smokeC`, `--initialize-from=014_r2`, so 014_r2 was not resumed). It ran on the exact phase C yaml. Launched 14:21:45; reached 100k at 14:25:38 (224 s); then hard-stopped, and 0 trainer, player or Python processes remained.
- The yaml writes its episode log to `results/014_r2_episodes/C`. The smoke's 8 files were moved to `results/014_smokeC_episodes/` afterwards, so the C directory was empty when C launched.
- The smoke folder `results/014_smokeC` is left in place. Logs: `results/014_logs/smokeC.*`.
- The trainer reports every parameter as fixed: perturb/scale 1.0, canonicalFraction 0.0, hold 50, lift budget 300, anchorShare 0.1, morph 1.0, object/mode 1.0.
- Every one of the 10 summaries has `Task/PerturbScale` = 1.0.
- The layout check passed in all 8 players (Slots: vector 28, tokens 21 × 20, actions 24).
- The smoke LR was 3.0e-4, since `--initialize-from` starts at step 0. The resume will start at 1.0e-4.

| composition | episodes | gate | lift | hold | success | end drop | end liftBudget | end maxStep |
|---|---|---|---|---|---|---|---|---|
| 3 | 149 | 0.799 | 0.275 | 0.221 | 0.020 | 0.356 | 0.409 | 0.208 |
| 4 | 223 | 0.987 | 0.377 | 0.350 | 0.103 | 0.390 | 0.484 | 0.018 |
| 5 | 660 | 0.995 | 0.518 | 0.480 | 0.118 | 0.483 | 0.383 | 0.005 |
| 6a | 110 | 0.991 | 0.436 | 0.427 | 0.109 | 0.445 | 0.436 | 0.009 |
| 6b | 111 | 0.973 | 0.495 | 0.468 | 0.099 | 0.559 | 0.306 | 0.027 |
| 7 | 227 | 0.982 | 0.436 | 0.423 | 0.084 | 0.471 | 0.419 | 0.018 |
| all | 1480 | 0.970 | 0.452 | 0.421 | 0.099 | 0.457 | 0.405 | 0.031 |

**Smoke result:**
- 1480 / 1480 episodes are randomStable (lying) at K 50, and 10.0 % drew the cylinder.
- All six compositions were drawn, and gate, lift and hold fire on all six.
- The pooled numbers match B's last-1M lying stats (lift 0.435, success 0.099).

**Check tool.** `results/014/CD/cd_check.py` (new; it reads via `tools/eval/phase_monitor_014`) implements the brief's C and D criteria and appends to `C_CHECKS.md` / `D_CHECKS.md`. The C stop rule's "two consecutive falls" uses B's last-1M lying lift (0.435) as the reference before the first C window.

## Step 2: phase C launch (2026-10-08 14:26:20)

- **Command:** `mlagents-learn Config/run_014_phaseC.yaml --run-id=014_r2 --results-dir=results --resume`, with the Run014 player (`--topology slots` via env_args) and 8 envs.
- **Logs:** `results/014_logs/r2_C.*`. Episodes go to `results/014_r2_episodes/C`, which was empty at launch.
- **checkpoint.pt** at launch: sha 70b2573a…, the same as the backup `checkpoint_Bend_24000187.pt`.
- **Trainer:** "Resuming training from step 24000187". Every parameter is fixed: canonicalFraction 0.0, perturb 1.0, hold 50, lift budget 300, anchorShare 0.1.
- **Layout check:** passed in 8 / 8 players.
- **Schedules** (TensorBoard), in line with step 0:

  | | first summary (24.03M) | 24.19M |
  |---|---|---|
  | learning rate | 9.983e-5 | 9.846e-5 |
  | epsilon | 0.1333 | 0.1328 |
  | beta | 1.670e-3 | 1.648e-3 |

- `Task/PerturbScale` is 1.0 throughout. No non-finite scalar.
- **Throughput:** 24.01M → 24.20M in 435 s = **437 steps/s**. C+8M (32M) would therefore be about 5.1 h after launch.

## Phase C: report marks and switch (2026-10-08)

All checks are in `C_CHECKS.md`; there was one check every 500k from C+0.5M to C+7.0M. Throughput held at 437–439 steps/s. There were no NaN values and no crash.

| mark | lying success, trailing 500k | lying lift, last 1M | lying success, last 1M | end drop | end liftBudget | end maxStep |
|---|---|---|---|---|---|---|
| C+1M (25.0M) | 0.160 | 0.468 | 0.133 | 0.432 | 0.402 | 0.028 |
| C+2M (26.0M) | 0.268 | 0.501 | 0.247 | 0.348 | 0.371 | 0.027 |
| C+4M (28.0M) | 0.366 | 0.551 | 0.359 | 0.289 | 0.322 | 0.024 |
| C+6M (30.0M) | 0.387 | 0.576 | 0.387 | 0.289 | 0.297 | 0.023 |
| **C+7M (31.0M), switch** | **0.402** (n 6869) | 0.581 | 0.398 | 0.281 | 0.294 | 0.022 |

**Pooled lying success per 500k window:**

| window end | 24.5M | 25.0M | 25.5M | 26.0M | 26.5M | 27.0M | 27.5M |
|---|---|---|---|---|---|---|---|
| success | 0.106 | 0.159 | 0.227 | 0.267 | 0.303 | 0.331 | 0.352 |

| window end | 28.0M | 28.5M | 29.0M | 29.5M | 30.0M | 30.5M | 31.0M |
|---|---|---|---|---|---|---|---|
| success | 0.367 | 0.357 | 0.378 | 0.385 | 0.388 | 0.393 | **0.402** |

**Pooled lying lift per 1M window:**

| window end | 25M | 26M | 27M | 28M | 29M | 30M | 31M |
|---|---|---|---|---|---|---|---|
| lift | 0.467 | 0.502 | 0.538 | 0.550 | 0.558 | 0.575 | 0.581 |

Lift never fell, so the stop rule was never near firing.

**Lying at the switch, last 1M (30.0M–31.0M):**

| composition | episodes | gate | lift | hold | success | end drop | end liftBudget | end maxStep |
|---|---|---|---|---|---|---|---|---|
| 3 | 1371 | 0.881 | 0.458 | 0.430 | 0.306 | 0.301 | 0.268 | 0.124 |
| 4 | 2009 | 0.980 | 0.557 | 0.541 | 0.401 | 0.274 | 0.301 | 0.021 |
| 5 | 6047 | 0.992 | 0.612 | 0.592 | 0.434 | 0.269 | 0.284 | 0.008 |
| 6a | 1025 | 0.987 | 0.583 | 0.559 | 0.411 | 0.258 | 0.312 | 0.014 |
| 6b | 979 | 0.995 | 0.607 | 0.581 | 0.341 | 0.356 | 0.292 | 0.005 |
| 7 | 2065 | 0.990 | 0.579 | 0.552 | 0.369 | 0.283 | 0.328 | 0.010 |
| all | 13496 | 0.979 | 0.580 | 0.558 | 0.398 | 0.281 | 0.294 | 0.022 |

**Switch decision.**
- At the C+7.0M check (log step 31,000,000, 18:53:28), the trailing 500k pooled lying success was **0.402 ≥ 0.40** (n 6869). The window is 30.5M–31.0M, all of it after C began.
- C ran 7.0M steps (24.0M → 31.0M), against 013 C's 10.5M.

**Stopping C.**
- Hard stop at 18:53:43. The trainer, the 8 players and the 8 env-worker Python processes (venv command line, base-interpreter executable path) were killed. 0 processes remained.
- `checkpoint.pt` = Prosthetic-30999947 (step 30999947, written 18:53:16, all tensors equal). It is backed up as **`checkpoint_Cend_30999947.pt`** (sha 412c30fa…). The C-end onnx is Prosthetic-30999947.onnx (abe7b9cc…).
- The status file was not rewritten by the hard stop. Every parameter is at lesson 0, so that is harmless here.

## Step 3: phase D launch (2026-10-08 18:54:26)

- **Command:** `mlagents-learn Config/run_014_phaseD.yaml --run-id=014_r2 --results-dir=results --resume`.
- **Logs:** `results/014_logs/r2_D.*`. Episodes go to `results/014_r2_episodes/D`, which was empty at launch.
- **Trainer:** "Resuming training from step 30999947". Every parameter is fixed: canonicalFraction 0.5, perturb 1.0, hold 50, lift budget 300, anchorShare 0.1.
- **Layout check:** passed in 8 / 8 players.
- **LR at the D start:** 3e-4 × (1 − 30999947 / 36e6) = 4.17e-5, decaying to 0 at 36M.
- **D+6M would be 37.0M, beyond max_steps.** So D ends at the end criterion or at 36M (D+5.0M), whichever comes first.
- **Report marks:** D+0.5M (31.5M), D+1M (32M), D+2M (33M), then every 1M (34M, 35M, 36M). The recovery rule applies at D+3M (34M).
