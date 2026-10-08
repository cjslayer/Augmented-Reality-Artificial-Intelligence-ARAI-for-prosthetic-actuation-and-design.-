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
