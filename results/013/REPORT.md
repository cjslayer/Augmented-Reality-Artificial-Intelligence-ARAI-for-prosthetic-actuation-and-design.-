# Run 013: randomized object shapes and random stable presentation poses (2026-09-27 to 2026-09-28)

First policy trained on the superquadric object bank (train shapes 1-64) with random stable rest poses, from scratch, 20M steps, then evaluated
against run 012 on held-out shapes. **Not deployed**; the scene model stays run 012.

## Headline

| held-out shapes 65-80, 600 paired episodes per cell | 012 | 013C (13.5M, end of lying-only phase C) | 013D (20M, end of 50/50 phase D) | 013D − 012 [95 % CI] | 013D − 013C [95 % CI] |
|---|---|---|---|---|---|
| canonical (upright) | 0.548 | 0.023 | **0.727** | +0.178 [+0.136, +0.221] | +0.703 [+0.666, +0.740] |
| random stable rest pose | 0.000 | 0.558 | **0.523** | +0.523 [+0.483, +0.563] | −0.035 [−0.076, +0.006] |
| anchor cylinder, canonical, seeds 7001-7300 (n = 300) | 1.000 | 0.000 | 0.853 | | |

- One policy can hold upright and lying grasps together. The fixed 50/50 phase D recovered the upright grasp that the lying-only phase C had
  destroyed (0.02 to 0.73 on held-out shapes, beyond 012's 0.55) while keeping the lying grasp within noise of the specialist (0.52 against
  0.56). 012 has no lying grasp at all.
- The upright gain over 012 is on short and mid-length objects (< 130 mm: 0.56 against 0.12; 130-210 mm: 0.79 against 0.72); on long bars
  the two are equal (0.87 against 0.85). 013D reaches the contact gate on 97 % of upright episodes against 73 % for 012.
- 013D's upright grasp depends on a driven thumb base. On the three thumb-masked probe hands it scores 0.003-0.010 upright (012: 0.44-0.60),
  on the easy hands 0.85-0.90 (012: 0.62-0.69). The thumb-masked − easy gap grows from −0.119 (012) to −0.875 [−0.899, −0.851].
- Lying-object success is GRASP-LIMITED for both 013 models (`PULSE_VS_GRASP.md`). Without the pulses it moves by +0.013 and +0.033, inside
  noise; the ceiling is forming a liftable grasp on a lying object (74-92 of 300 episodes meet the gate and then exhaust the lift budget).
- BO range: all three probes have range by the rule; for 013D it is concentrated in the thumb-base bit (section 6).

## 1. Recipe

- Fresh lineage (no init_path; within-lineage resumes of run id 013 only). Player `Builds/Run013/Prosthetic.exe` (Dynamic_Scene_Train,
  main 7b3f8ca, Assembly-CSharp 040872f1), bank shipped in StreamingAssets/ObjectBank (81 shapes, 131 pose classes), 8 headless envs,
  run-012 PPO hyperparameters, max_steps 20M in every phase file (continuous linear learning rate), checkpoints every 250k (80 kept).
- Object: train shapes 1-64 drawn uniformly per episode (`object/mode` 1), mass log-uniform 0.2-1.5 kg, mu 1.0, per-episode theta
  (thumb-base-masked hands included). Presentation: canonical, or a random stable rest pose, drawn per episode with probability
  `pose/canonicalFraction`; rest poses grouped into symmetry classes (`results/013_prep`, commit 7b3f8ca).
- Phases (`Config/run_013_phase{A,B,C,D}.yaml`; diffs against run_012 in `results/013_prep/YAML_DIFFS_vs_012.txt`, D against C in
  `phaseD_vs_phaseC.diff`):

| phase | steps | presentation | hold K | perturbation | curriculum |
|---|---|---|---|---|---|
| A | 0 to 1.0M | canonical | 5, 10, 20, 50 | 0 | 012 thresholds, min 100 episodes |
| B | 1.0M to 3.0M | canonical fraction 1.0, 0.5, 0.0 | 50 | 0 | threshold 0.9, min 1000 episodes, require_reset |
| C | 3.0M to 13.5M | lying only (fraction 0) | 50 | 0.25, 0.5, 1.0 | 012 thresholds |
| D (amendment) | 13.5M to 20M | fixed 50/50 | 50 | 1.0 | none, every parameter fixed |

## 2. Transitions (`transitions.json`)

| switch | lesson entry | stopped at (logged) | resumed from | checkpoint sha256 |
|---|---|---|---|---|
| A to B | Hold50 at 820k | 1.08M | Prosthetic-999996 | 3943949f |
| B to C | Canon0 at 2.95M | 3.21M | Prosthetic-2999954 | fd7aa7ac |
| C to D | none (amendment) | 13.50M | Prosthetic-13499981 (= 013C, onnx 05b010c2) | 279651fb |
| end | - | graceful exit at 20000063 | 013D = Prosthetic-20000063 (onnx 96ac32e6) | d98044e9 |

Every hard stop left no process behind; the status file was written by hand with the next phase's lessons at 0 (MLAGENTS_UPGRADE.md,
run-011b procedure) and checkpoint.pt made byte-identical to the resume checkpoint. The lesson ladders cascaded: hold 20 to 50 in 30k steps,
perturbation 0.5 to 1.0 in 30k steps (about 650 episodes; see `results/013_prep/CURRICULUM_NOTE.md`, now in MLAGENTS_UPGRADE.md).
Throughput 390-410 steps/s sustained (500k windows spanning a switch or an Editor check read 275-381); no NaN at any row.

## 3. Training curves (`monitor.csv`, 50k-step window means of the sampled-head training stats)

| step | phase | K | canonical fraction | perturb | reward | Success/Canonical | Success/Lying | gate | lift | palm | steps/s (500k) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0.50M | A | 5 | 1.0 | 0.0 | 0.452 | 0.007 | - | 0.977 | 0.084 | 0.160 | 419 |
| 1.01M | B | 50 | 1.0 | 0.0 | 0.814 | 0.204 | - | 0.998 | 0.491 | 0.364 | 335 |
| 2.00M | B | 50 | 0.5 | 0.0 | 0.698 | 0.482 | 0.011 | 0.981 | 0.386 | 0.422 | 396 |
| 3.00M | C | 50 | 0.0 | 0.25 | 0.490 | - | 0.011 | 0.976 | 0.059 | 0.463 | 275 |
| 4.00M | C | 50 | 0.0 | 0.25 | 0.530 | - | 0.014 | 0.985 | 0.205 | 0.445 | 403 |
| 5.00M | C | 50 | 0.0 | 0.25 | 0.581 | - | 0.004 | 0.989 | 0.299 | 0.440 | 402 |
| 6.00M | C | 50 | 0.0 | 0.25 | 0.653 | - | 0.016 | 0.995 | 0.381 | 0.465 | 391 |
| 7.00M | C | 50 | 0.0 | 0.25 | 0.662 | - | 0.027 | 0.992 | 0.437 | 0.451 | 397 |
| 8.00M | C | 50 | 0.0 | 1.0 | 0.853 | - | 0.224 | 0.996 | 0.516 | 0.458 | 406 |
| 9.00M | C | 50 | 0.0 | 1.0 | 0.918 | - | 0.319 | 0.991 | 0.561 | 0.497 | 408 |
| 10.00M | C | 50 | 0.0 | 1.0 | 0.957 | - | 0.324 | 0.997 | 0.531 | 0.490 | 403 |
| 11.00M | C | 50 | 0.0 | 1.0 | 0.990 | - | 0.375 | 0.995 | 0.565 | 0.513 | 407 |
| 12.00M | C | 50 | 0.0 | 1.0 | 1.056 | - | 0.411 | 0.997 | 0.607 | 0.539 | 407 |
| 13.00M | C | 50 | 0.0 | 1.0 | 1.018 | - | 0.385 | 0.993 | 0.566 | 0.553 | 396 |
| 14.00M | D | 50 | 0.5 | 1.0 | 0.789 | 0.016 | 0.437 | 0.984 | 0.381 | 0.410 | 394 |
| 15.00M | D | 50 | 0.5 | 1.0 | 0.862 | 0.049 | 0.426 | 0.993 | 0.406 | 0.485 | 402 |
| 16.00M | D | 50 | 0.5 | 1.0 | 0.924 | 0.180 | 0.434 | 0.992 | 0.497 | 0.492 | 381 |
| 17.00M | D | 50 | 0.5 | 1.0 | 1.007 | 0.388 | 0.366 | 0.993 | 0.583 | 0.546 | 399 |
| 18.00M | D | 50 | 0.5 | 1.0 | 1.124 | 0.512 | 0.403 | 0.999 | 0.646 | 0.548 | 409 |
| 19.00M | D | 50 | 0.5 | 1.0 | 1.150 | 0.577 | 0.388 | 0.993 | 0.661 | 0.599 | 407 |
| 20.00M | D | 50 | 0.5 | 1.0 | 1.152 | 0.559 | 0.390 | 0.997 | 0.638 | 0.587 | 394 |

Phase C taught the lying grasp only after the perturbation ladder completed (Success/Lying 0.01-0.03 from 3M to 7M, 0.22 at 8M, 0.41 at
12M) and erased the upright grasp (interim check at 12.25M: 0.026 against 012's 0.615 on the same 192 rows, `interim/INTERIM.md`). Phase D
brought Success/Canonical from 0.02 back to 0.56-0.58 in 5M steps while Success/Lying stayed at 0.37-0.45; the amendment's interference
flag (lying below 0.25 for 1M while canonical rises) never fired.

## 4. Evaluation (Part 3)

All passes: Editor harness, deterministic head, one job worker, per-episode reseed, K = 50, perturbation 1.0, mu 1.0, mass as 012, idle
machine (after training). Held-out: shapes 65-80 stratified, seeds 10001-10300 × passIndex 0-1 (600 episodes per model and presentation,
37-38 per shape), paired per episode across the three models (same seed, pass, shape, theta, mass, spawn, yaw and rest pose). Full tables
with every family: `eval/PART3_TABLES.md`; passes under `eval/<model>/`.

### Held-out shapes 65-80, canonical: 600 episodes paired across the three models

| subset | n | 012 | 013C | 013D | 013D − 012 | 013D − 013C | 013C − 012 |
|---|---|---|---|---|---|---|---|
| all | 600 | 0.548 [0.508, 0.588] | 0.023 [0.014, 0.039] | 0.727 [0.690, 0.761] | +0.178 [+0.136, +0.221] | +0.703 [+0.666, +0.740] | -0.525 [-0.568, -0.482] |
| length 130-210 mm | 337 | 0.718 [0.668, 0.763] | 0.006 [0.002, 0.021] | 0.789 [0.743, 0.829] | +0.071 [+0.020, +0.123] | +0.783 [+0.739, +0.827] | -0.712 [-0.761, -0.664] |
| length 210-280 mm | 75 | 0.853 [0.756, 0.916] | 0.000 [0.000, 0.049] | 0.867 [0.772, 0.926] | +0.013 [-0.081, +0.108] | +0.867 [+0.790, +0.944] | -0.853 [-0.933, -0.773] |
| length < 130 mm | 188 | 0.122 [0.083, 0.177] | 0.064 [0.037, 0.108] | 0.559 [0.487, 0.628] | +0.436 [+0.355, +0.517] | +0.495 [+0.420, +0.569] | -0.059 [-0.116, -0.001] |
| cross 50-65 mm | 226 | 0.518 [0.453, 0.582] | 0.013 [0.005, 0.038] | 0.690 [0.627, 0.747] | +0.173 [+0.104, +0.241] | +0.677 [+0.616, +0.738] | -0.504 [-0.572, -0.437] |
| cross 65-80 mm | 374 | 0.567 [0.516, 0.616] | 0.029 [0.017, 0.052] | 0.749 [0.702, 0.790] | +0.182 [+0.127, +0.236] | +0.719 [+0.673, +0.766] | -0.537 [-0.592, -0.483] |
| e1 0.3-0.7 | 186 | 0.522 [0.450, 0.592] | 0.059 [0.033, 0.103] | 0.753 [0.686, 0.809] | +0.231 [+0.156, +0.306] | +0.694 [+0.624, +0.763] | -0.462 [-0.547, -0.378] |
| e1 0.7-1.1 | 187 | 0.770 [0.705, 0.825] | 0.005 [0.001, 0.030] | 0.791 [0.728, 0.844] | +0.021 [-0.045, +0.088] | +0.786 [+0.727, +0.845] | -0.765 [-0.826, -0.704] |
| e1 1.1-1.5 | 227 | 0.388 [0.327, 0.452] | 0.009 [0.002, 0.032] | 0.652 [0.588, 0.711] | +0.264 [+0.191, +0.338] | +0.643 [+0.581, +0.705] | -0.379 [-0.443, -0.315] |
| e2 0.3-0.7 | 226 | 0.566 [0.501, 0.629] | 0.009 [0.002, 0.032] | 0.743 [0.683, 0.796] | +0.177 [+0.109, +0.245] | +0.735 [+0.677, +0.792] | -0.558 [-0.623, -0.492] |
| e2 0.7-1.1 | 150 | 0.293 [0.226, 0.371] | 0.067 [0.037, 0.118] | 0.627 [0.547, 0.700] | +0.333 [+0.240, +0.427] | +0.560 [+0.476, +0.644] | -0.227 [-0.314, -0.140] |
| e2 1.1-1.5 | 224 | 0.701 [0.638, 0.757] | 0.009 [0.002, 0.032] | 0.777 [0.718, 0.826] | +0.076 [+0.013, +0.139] | +0.768 [+0.713, +0.823] | -0.692 [-0.752, -0.632] |
| aspect 1.5-3 | 412 | 0.461 [0.414, 0.509] | 0.012 [0.005, 0.028] | 0.677 [0.631, 0.721] | +0.216 [+0.163, +0.269] | +0.665 [+0.619, +0.711] | -0.449 [-0.498, -0.400] |
| aspect < 1.5 | 37 | 0.054 [0.015, 0.177] | 0.243 [0.134, 0.401] | 0.703 [0.542, 0.825] | +0.649 [+0.495, +0.802] | +0.459 [+0.267, +0.652] | +0.189 [+0.042, +0.336] |
| aspect >= 3 | 151 | 0.907 [0.850, 0.944] | 0.000 [0.000, 0.025] | 0.868 [0.804, 0.913] | -0.040 [-0.100, +0.021] | +0.868 [+0.813, +0.922] | -0.907 [-0.954, -0.861] |
| volume low (cuts 171, 236 cm³) | 189 | 0.222 [0.169, 0.287] | 0.016 [0.005, 0.046] | 0.571 [0.500, 0.640] | +0.349 [+0.268, +0.430] | +0.556 [+0.485, +0.626] | -0.206 [-0.268, -0.145] |
| volume mid (cuts 171, 236 cm³) | 186 | 0.640 [0.569, 0.705] | 0.011 [0.003, 0.038] | 0.758 [0.692, 0.814] | +0.118 [+0.044, +0.192] | +0.747 [+0.685, +0.810] | -0.629 [-0.698, -0.560] |
| volume high (cuts 171, 236 cm³) | 225 | 0.747 [0.686, 0.799] | 0.040 [0.021, 0.074] | 0.831 [0.777, 0.874] | +0.084 [+0.022, +0.147] | +0.791 [+0.735, +0.847] | -0.707 [-0.776, -0.638] |

| model | reach-failure rate | gate | lifted | palm at end | thumb at end | contacts | drops (lift phase / before first pulse / during / between / after) |
|---|---|---|---|---|---|---|---|
| 012 | 0.268 | 0.732 | 0.650 | 0.813 | 0.802 | 3.73 | 53 / 35 / 0 / 18 / 4 |
| 013C | 0.208 | 0.792 | 0.090 | 0.275 | 0.678 | 4.32 | 399 / 22 / 1 / 9 / 2 |
| 013D | 0.028 | 0.972 | 0.763 | 0.672 | 0.862 | 5.28 | 115 / 10 / 0 / 7 / 3 |

Contacts and palm flag by length family:

| length | 012 contacts / palm | 013C contacts / palm | 013D contacts / palm |
|---|---|---|---|
| length 130-210 mm | 4.19 / 0.86 | 4.58 / 0.24 | 5.40 / 0.72 |
| length 210-280 mm | 5.37 / 0.59 | 3.89 / 0.12 | 6.00 / 0.72 |
| length < 130 mm | 2.26 / 0.82 | 4.02 / 0.40 | 4.77 / 0.57 |

### Held-out shapes 65-80, randomStable: 599 episodes paired across the three models

| subset | n | 012 | 013C | 013D | 013D − 012 | 013D − 013C | 013C − 012 |
|---|---|---|---|---|---|---|---|
| all | 599 | 0.000 [0.000, 0.006] | 0.558 [0.518, 0.597] | 0.523 [0.483, 0.562] | +0.523 [+0.483, +0.563] | -0.035 [-0.076, +0.006] | +0.558 [+0.518, +0.597] |
| length 130-210 mm | 336 | 0.000 [0.000, 0.011] | 0.598 [0.545, 0.649] | 0.574 [0.521, 0.626] | +0.574 [+0.522, +0.627] | -0.024 [-0.078, +0.031] | +0.598 [+0.546, +0.651] |
| length 210-280 mm | 75 | 0.000 [0.000, 0.049] | 0.453 [0.346, 0.566] | 0.453 [0.346, 0.566] | +0.453 [+0.341, +0.566] | +0.000 [-0.098, +0.098] | +0.453 [+0.341, +0.566] |
| length < 130 mm | 188 | 0.000 [0.000, 0.020] | 0.527 [0.455, 0.597] | 0.457 [0.388, 0.529] | +0.457 [+0.386, +0.529] | -0.069 [-0.147, +0.009] | +0.527 [+0.455, +0.598] |
| cross 50-65 mm | 226 | 0.000 [0.000, 0.017] | 0.549 [0.484, 0.612] | 0.496 [0.431, 0.560] | +0.496 [+0.430, +0.561] | -0.053 [-0.123, +0.017] | +0.549 [+0.484, +0.614] |
| cross 65-80 mm | 373 | 0.000 [-0.000, 0.010] | 0.563 [0.512, 0.612] | 0.539 [0.488, 0.589] | +0.539 [+0.488, +0.589] | -0.024 [-0.075, +0.026] | +0.563 [+0.513, +0.613] |
| e1 0.3-0.7 | 185 | 0.000 [0.000, 0.020] | 0.562 [0.490, 0.632] | 0.524 [0.453, 0.595] | +0.524 [+0.452, +0.596] | -0.038 [-0.109, +0.033] | +0.562 [+0.491, +0.634] |
| e1 0.7-1.1 | 187 | 0.000 [0.000, 0.020] | 0.578 [0.506, 0.646] | 0.551 [0.479, 0.620] | +0.551 [+0.480, +0.622] | -0.027 [-0.101, +0.048] | +0.578 [+0.507, +0.648] |
| e1 1.1-1.5 | 227 | 0.000 [0.000, 0.017] | 0.537 [0.472, 0.601] | 0.498 [0.433, 0.562] | +0.498 [+0.433, +0.563] | -0.040 [-0.108, +0.029] | +0.537 [+0.473, +0.602] |
| e2 0.3-0.7 | 226 | 0.000 [0.000, 0.017] | 0.566 [0.501, 0.629] | 0.504 [0.440, 0.569] | +0.504 [+0.439, +0.570] | -0.062 [-0.130, +0.006] | +0.566 [+0.502, +0.631] |
| e2 0.7-1.1 | 150 | 0.000 [0.000, 0.025] | 0.540 [0.460, 0.618] | 0.520 [0.441, 0.598] | +0.520 [+0.440, +0.600] | -0.020 [-0.102, +0.062] | +0.540 [+0.460, +0.620] |
| e2 1.1-1.5 | 223 | 0.000 [0.000, 0.017] | 0.561 [0.495, 0.624] | 0.543 [0.477, 0.607] | +0.543 [+0.477, +0.608] | -0.018 [-0.085, +0.049] | +0.561 [+0.495, +0.626] |
| aspect 1.5-3 | 411 | 0.000 [-0.000, 0.009] | 0.555 [0.506, 0.602] | 0.513 [0.465, 0.561] | +0.513 [+0.465, +0.562] | -0.041 [-0.092, +0.010] | +0.555 [+0.507, +0.603] |
| aspect < 1.5 | 37 | 0.000 [0.000, 0.094] | 0.703 [0.542, 0.825] | 0.703 [0.542, 0.825] | +0.703 [+0.555, +0.850] | +0.000 [-0.168, +0.168] | +0.703 [+0.555, +0.850] |
| aspect >= 3 | 151 | 0.000 [0.000, 0.025] | 0.530 [0.450, 0.608] | 0.503 [0.424, 0.582] | +0.503 [+0.424, +0.583] | -0.026 [-0.102, +0.049] | +0.530 [+0.450, +0.609] |
| volume low (cuts 171, 236 cm³) | 189 | 0.000 [0.000, 0.020] | 0.519 [0.448, 0.589] | 0.450 [0.381, 0.521] | +0.450 [+0.379, +0.521] | -0.069 [-0.146, +0.009] | +0.519 [+0.447, +0.590] |
| volume mid (cuts 171, 236 cm³) | 186 | 0.000 [0.000, 0.020] | 0.581 [0.509, 0.649] | 0.554 [0.482, 0.623] | +0.554 [+0.482, +0.625] | -0.027 [-0.102, +0.048] | +0.581 [+0.510, +0.652] |
| volume high (cuts 171, 236 cm³) | 224 | 0.000 [0.000, 0.017] | 0.571 [0.506, 0.634] | 0.558 [0.493, 0.622] | +0.558 [+0.493, +0.623] | -0.013 [-0.076, +0.049] | +0.571 [+0.507, +0.636] |
| pose class upright | 10 | 0.000 [-0.000, 0.278] | 0.300 [0.108, 0.603] | 0.500 [0.237, 0.763] | +0.500 [+0.190, +0.810] | +0.200 [-0.172, +0.572] | +0.300 [+0.016, +0.584] |
| pose class lying | 513 | 0.000 [0.000, 0.007] | 0.565 [0.522, 0.608] | 0.538 [0.495, 0.581] | +0.538 [+0.495, +0.581] | -0.027 [-0.071, +0.017] | +0.565 [+0.522, +0.608] |
| pose class tilted rest | 76 | 0.000 [0.000, 0.048] | 0.539 [0.428, 0.647] | 0.421 [0.316, 0.533] | +0.421 [+0.310, +0.532] | -0.118 [-0.234, -0.003] | +0.539 [+0.427, +0.652] |

| model | reach-failure rate | gate | lifted | palm at end | thumb at end | contacts | drops (lift phase / before first pulse / during / between / after) |
|---|---|---|---|---|---|---|---|
| 012 | 0.923 | 0.077 | 0.003 | 0.718 | 0.027 | 0.59 | 42 / 2 / 0 / 0 / 0 |
| 013C | 0.035 | 0.965 | 0.626 | 0.576 | 0.942 | 4.81 | 47 / 16 / 2 / 17 / 3 |
| 013D | 0.040 | 0.962 | 0.624 | 0.646 | 0.942 | 4.97 | 59 / 14 / 5 / 29 / 7 |

Contacts and palm flag by length family:

| length | 012 contacts / palm | 013C contacts / palm | 013D contacts / palm |
|---|---|---|---|
| length 130-210 mm | 0.49 / 0.72 | 5.00 / 0.60 | 5.12 / 0.67 |
| length 210-280 mm | 0.93 / 0.69 | 4.85 / 0.59 | 5.09 / 0.65 |
| length < 130 mm | 0.64 / 0.72 | 4.46 / 0.53 | 4.66 / 0.60 |

### 013D on train shapes 1-64, seeds 10001-10300 × passIndex 0-2

| subset | canonical | random-stable |
|---|---|---|
| all (n=900 / 900) | 0.739 [0.709, 0.767] | 0.513 [0.481, 0.546] |
| length < 130 mm | 0.580 [0.523, 0.635] | 0.532 [0.475, 0.588] |
| length 130-210 mm | 0.803 [0.760, 0.839] | 0.499 [0.450, 0.548] |
| length 210-280 mm | 0.843 [0.788, 0.886] | 0.514 [0.447, 0.581] |

Per-shape canonical success over 64 shapes: min 0.20, median 0.79, max 1.00; shapes ≤ 0.70: 18.
Per-shape random-stable success over 64 shapes: min 0.13, median 0.57, max 0.86; shapes ≤ 0.70: 58.

### Anchor regression (canonical cylinder, seeds 7001-7300)

| model | success [95 % Wilson] | lifted | palm | ends |
|---|---|---|---|---|
| 012 (reference, eval_n300_v2 / 2026-09-27) | 1.000 [0.987, 1.000] | 1.000 | 0.967 | 300 success |
| 013C | 0.000 [0.000, 0.013] | 0.090 | 0.200 | {'drop': 205, 'maxStep': 95} |
| 013D | 0.853 [0.809, 0.889] | 0.870 | 0.870 | {'drop': 43, 'maxStep': 1, 'success': 256} |

013D on the cylinder (bank index 0, which training never drew: trainUniform covers 1-64) fails by 43 drops, 38 in the lift phase before
any pulse. At the drop the thumb touches the object in 25 % of cases (100 % at success) with 2.7 finger groups in contact (6.5 at
success); mass does not separate failures (0.68 kg) from successes (0.65 kg).

## 5. Hand × shape grids on 013D (six probe hands, 80 shapes × 5 episodes, seeds 9301-9400 × passIndex 0-3, paired across hands)

Canonical (`eval/GRID_013D_canonical.md`), paired against 012's canonical grid (`results/013_prep/GRID_012_canonical.md`):

| hand | thumb base | episodes | success [95 % Wilson] | lifted | palm at end | thumb at end | contacts | reach fail | drops (lift phase / before first pulse / during / between / after) | gate→lift median steps |
|---|---|---|---|---|---|---|---|---|---|---|
| 5 | masked | 400 | 0.010 [0.004, 0.025] (n=400) | 0.015 | 0.115 | 0.385 | 3.75 | 0.025 | 376 / 1 / 0 / 0 / 0 | 151.0 |
| 11 | masked | 400 | 0.003 [0.000, 0.014] (n=400) | 0.007 | 0.075 | 0.475 | 3.76 | 0.052 | 359 / 2 / 0 / 0 / 0 | 44 |
| 14 | masked | 400 | 0.007 [0.003, 0.022] (n=400) | 0.025 | 0.035 | 0.268 | 2.66 | 0.040 | 364 / 6 / 0 / 0 / 1 | 41.5 |
| 1 | active | 400 | 0.845 [0.806, 0.877] (n=400) | 0.935 | 0.812 | 0.953 | 6.39 | 0.005 | 12 / 15 / 0 / 13 / 6 | 20.0 |
| 2 | active | 400 | 0.897 [0.864, 0.924] (n=400) | 0.917 | 0.740 | 1.000 | 6.09 | 0.007 | 15 / 4 / 1 / 1 / 2 | 20 |
| 3 | active | 400 | 0.902 [0.869, 0.928] (n=400) | 0.955 | 0.762 | 0.968 | 5.58 | 0.003 | 14 / 9 / 1 / 6 / 4 | 21.0 |

Paired over 400 episodes common to all six hands (same seed, passIndex and shape): thumb-masked mean 0.007, easy mean 0.882, **thumb-masked − easy -0.875 [-0.899, -0.851]**.


### Paired per hand: 013D − 012 (same seed, passIndex, shape and theta)

| hand | pairs | 013D | 012 | difference [95 % CI] |
|---|---|---|---|---|
| 5 | 399 | 0.010 | 0.531 | -0.521 [-0.572, -0.471] |
| 11 | 399 | 0.003 | 0.441 | -0.439 [-0.487, -0.390] |
| 14 | 398 | 0.008 | 0.603 | -0.595 [-0.644, -0.547] |
| 1 | 399 | 0.845 | 0.619 | +0.226 [+0.179, +0.273] |
| 2 | 399 | 0.897 | 0.692 | +0.206 [+0.162, +0.249] |
| 3 | 400 | 0.902 | 0.620 | +0.282 [+0.236, +0.329] |

Random stable (`eval/GRID_013D_randomStable.md`), paired against 013D canonical on the same rows:

| hand | thumb base | episodes | success [95 % Wilson] | lifted | palm at end | thumb at end | contacts | reach fail | drops (lift phase / before first pulse / during / between / after) | gate→lift median steps |
|---|---|---|---|---|---|---|---|---|---|---|
| 5 | masked | 400 | 0.138 [0.107, 0.175] (n=400) | 0.212 | 0.300 | 0.895 | 4.09 | 0.035 | 59 / 10 / 0 / 12 / 2 | 105 |
| 11 | masked | 400 | 0.205 [0.168, 0.247] (n=400) | 0.268 | 0.250 | 0.953 | 4.60 | 0.033 | 48 / 6 / 2 / 9 / 3 | 107 |
| 14 | masked | 400 | 0.180 [0.145, 0.221] (n=400) | 0.265 | 0.237 | 0.932 | 4.45 | 0.070 | 65 / 9 / 2 / 13 / 5 | 97.5 |
| 1 | active | 400 | 0.480 [0.431, 0.529] (n=400) | 0.542 | 0.605 | 0.938 | 5.36 | 0.035 | 23 / 9 / 1 / 8 / 5 | 61 |
| 2 | active | 400 | 0.625 [0.577, 0.671] (n=400) | 0.695 | 0.637 | 0.988 | 5.48 | 0.022 | 32 / 5 / 8 / 13 / 0 | 58.0 |
| 3 | active | 400 | 0.652 [0.605, 0.698] (n=400) | 0.772 | 0.598 | 0.958 | 4.98 | 0.028 | 16 / 21 / 1 / 14 / 7 | 51 |

Paired over 400 episodes common to all six hands (same seed, passIndex and shape): thumb-masked mean 0.174, easy mean 0.586, **thumb-masked − easy -0.412 [-0.448, -0.375]**.


### Paired per hand: 013D rs − 013D canonical (same seed, passIndex, shape and theta)

| hand | pairs | 013D rs | 013D canonical | difference [95 % CI] |
|---|---|---|---|---|
| 5 | 400 | 0.138 | 0.010 | +0.128 [+0.093, +0.162] |
| 11 | 400 | 0.205 | 0.003 | +0.203 [+0.163, +0.242] |
| 14 | 400 | 0.180 | 0.007 | +0.172 [+0.134, +0.211] |
| 1 | 400 | 0.480 | 0.845 | -0.365 [-0.424, -0.306] |
| 2 | 400 | 0.625 | 0.897 | -0.273 [-0.325, -0.220] |
| 3 | 400 | 0.652 | 0.902 | -0.250 [-0.304, -0.196] |

Reading: 013D learned an upright grasp that needs a driven thumb base. The thumb-masked hands meet the gate (reach failures 2.5-5.2 %) and
then drop the object in the lift phase in 359-376 of 400 episodes, with the thumb on the object in 27-48 % and the palm in 4-12 % of
episodes (easy hands: 95-100 % and 74-81 %). 012's upright grasp, which wedges the object against the palm, tolerated a passive thumb far
better (0.44-0.60). On rest poses the thumb-masked hands keep 0.14-0.21 against 0.48-0.65. The masked-thumb deficit is policy-specific and
grew about sevenfold between 012 and 013D.

## 6. BO range probes (20 probe theta, held-out shapes, seeds 10001-10100, n = 100 each, perturbation 1.0)

Rule: the objective has range if the mean over the 20 theta is in [0.45, 0.80] and the SD across theta is ≥ 0.10.

| model / presentation | n theta | mean | min | max | SD | worst 4 theta | range? |
|---|---|---|---|---|---|---|---|
| 013D random-stable | 20 | 0.522 | 0.14 | 0.77 | 0.173 | 5 (0.14), 14 (0.20), 11 (0.23), 19 (0.37) | **yes** |
| 013D canonical | 20 | 0.714 | 0.00 | 0.96 | 0.328 | 5 (0.00), 11 (0.00), 14 (0.01), 10 (0.47) | **yes** |
| 012 canonical | 20 | 0.538 | 0.23 | 0.68 | 0.115 | 10 (0.23), 11 (0.36), 18 (0.41), 5 (0.45) | **yes** |

Without the three thumb-masked probe hands (5, 11, 14): 013D random stable 0.581 (SD 0.104, min 0.37), 013D canonical 0.839 (SD 0.127,
min 0.47), 012 canonical 0.557 (SD 0.113, min 0.23); the thumb-masked hands average 0.190, 0.003 and 0.433. Verdict: the objective has range
in all three settings by the rule. For 013D canonical the range is mostly one mask bit, so a campaign would first rediscover "drive the thumb
base". Beyond it, 013D random stable and 012 canonical keep an SD of 0.10-0.11 across the remaining hands, the graded range that the
Part B probe on the cylinder lacked (SD 0.02-0.05).

## 7. Interference: lying specialist (013C) against the mixture (013D)

| presentation (held-out, 600 paired) | 013C | 013D | 013D − 013C [95 % CI] |
|---|---|---|---|
| canonical | 0.023 | 0.727 | +0.703 [+0.666, +0.740] |
| random stable | 0.558 | 0.523 | −0.035 [−0.076, +0.006] |

The mixture recovered upright without losing lying: an upright gain of +0.70 against a lying cost of −0.035 whose CI includes zero. The
largest lying cost by family is on tilted rest poses, −0.118 [−0.234, −0.003] (n = 76). The 16.5M interim check (`interim/INTERIM_16p5M.md`)
had upright at 0.52 and lying at 0.54 on its 192 rows, so the last 3.5M steps of phase D raised the upright grasp further at no measurable
lying cost.

Pulse vs grasp (`PULSE_VS_GRASP.md`): random stable, held-out, perturbation 0 against 1.0 on the same 300 rows.

| model | success, perturbation 0 | success, perturbation 1.0 | paired difference [95 % CI] | verdict |
|---|---|---|---|---|
| 013C | 0.550 | 0.537 | +0.013 [-0.039, +0.066] | **GRASP-LIMITED** |
| 013D | 0.530 | 0.497 | +0.033 [-0.019, +0.085] | **GRASP-LIMITED** |

## 8. Instrument notes

- Load sensitivity: passes run while the trainer was running are not byte-reproducible. A 012 throwaway during training diverged from the
  byte-stable reference at its second episode; on the idle machine after training it reproduced 2676025693a9c0e2 exactly. The interim
  checks ran under load (paired by construction, not byte-reproducible); every Part 3 pass ran on the idle machine.
- Even idle, byte identity is not guaranteed: a second 012 throwaway immediately after the first diverged at episode 2 (hash 1a1dd5d8).
  The cross-episode channel is timing-dependent state (candidate: interpolated transforms read by the observations while frame boundaries
  fall at different physics steps; not isolated). Comparisons here are paired by seed, shape and theta, not by byte identity, which adds
  realisation noise but no bias.
- Three passes finished while the runner's play-state poll read "stopped" before the CSV footer was visible; they were registered from their
  complete CSVs (DONE footer) and flagged in the logs.

## 9. Deployment recommendation (not deployed)

Do not replace the scene model with 013D now. On the bank it is the better policy (upright +0.18 and lying +0.52 over 012 on held-out
shapes), but on the deployed task it is worse: the upright cylinder drops from 1.000 to 0.853, and hands without a driven thumb base lose the
upright grasp entirely (0.00-0.01 against 0.44-0.60), a regression a prosthesis user with a passive thumb would feel directly. Before
reconsidering: (1) add the anchor cylinder to the training draw (index 0 was never trained on); (2) keep a canonical share in any continued
training, since phase C showed that a lying-only curriculum erases the upright grasp; (3) re-measure the thumb-masked hands after either a
thumb-base-active constraint on the morphology distribution or targeted training on them; (4) re-run the 012 instrument set (seeds 7001-7300
at mu 0.6, 1.0 and 1.5) on the candidate.
