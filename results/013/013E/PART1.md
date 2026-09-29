# Run 013E (2026-09-29): anchor share + 300-step lift budget, 20M -> 24M within lineage 013

**Gate result: FAIL on the anchor criterion (0.883 < 0.95); upright and lying both pass. 013D stays the BO substrate.**
013E is better than 013D on every held-out measure, but the gate requires all three criteria.

| criterion | 013D | 013E | 013E − 013D [95 % CI] | threshold | pass |
|---|---|---|---|---|---|
| anchor cylinder, canonical, seeds 7001-7300 (n = 300) | 0.853 | 0.883 [0.842, 0.915] | | ≥ 0.95 | **no** |
| upright (canonical), held-out 65-80, 600 paired | 0.727 | 0.755 | +0.028 [+0.003, +0.053] | ≥ −0.03, CI lower ≥ −0.06 | yes |
| lying (random stable), held-out 65-80, 600 paired | 0.523 | 0.567 | +0.043 [+0.004, +0.082] | ≥ −0.03, CI lower ≥ −0.06 | yes |

Full tables: `GATE.md` (held-out by length and rest-pose class, end reasons and drop phases, anchor, masked stratum).

## 1a. Lift-budget check (`LIFT_BUDGET.md`, tools/eval/lift_budget_check.py)

469 of 616 gated-never-lifted random-stable episodes (76.1 %) ended by the lift-budget timeout (147 by a drop), over the 013C / 013D Part 3
held-out passes and the two perturbation-0 pulse passes. Rule ≥ 50 % → lift budget 200 → 300 steps. Derivation (ArmGraspAgent.cs:421):
TaskBudgetSteps = liftBudget + HoldStepsNeeded + margin = 200 + 500 + 100 = 800 at K = 50; 300 + 500 + 100 = 900 in 013E training.

## 1b / 1c. Code (no reward, observation or action change)

- `Assets/Scripts/Objects/ObjectBank.cs`: `object/anchorShare` (trainUniform only; default 0 draws nothing extra, so the random stream is
  unchanged): the anchor with probability share, else uniform over 1-64.
- `Assets/Scripts/ArmGraspAgent.cs:93-94, :420, :657`: the single lift-budget read site now reads the per-episode `m_LiftBudgetEp` =
  env param `task/liftBudgetSteps`, else the serialized field (200). The Editor harness sets the field to 200 (ArticulatedGates.cs:261) and
  cannot set the env param, so **every eval pass here (013E included) runs the 200-step lift budget and the 800-step task budget of Part 3**.
  013E's held-out numbers are therefore measured on the old, stricter lift budget, and its lying gain is not due to a longer eval budget.
- Also in ObjectBank.cs, for Part 2 (Editor only, default unchanged): the harness key `objectIndexStride` (Temp/gates.json) maps fixedIndex
  train indices 1-64 to 1 + ((i − 1) s + s − 1) mod 64, so stride 2 gives the even shapes 2-64. The player never reads it.

## 1d. Build and regression

Builds/Run013 rebuilt (Build Finished: Success; Assembly-CSharp 010485484b6719e6, was 040872f16f7e253c; player log "bank ready: 81 shapes,
131 pose classes"). The objectIndexStride edit came after the build and is Editor-only. 012 regression on the rebuilt project (Editor):
throwaway hash 2676025693a9c0e2 (= the byte-stable reference), anchor canonical 7001-7300 **1.000 [0.987, 1.000]** (hash 4eb037f5),
`regression/`.

## 1e. Config (`Config/run_013_phaseE.yaml`, diff against phase D in `phaseE_vs_phaseD.diff`)

Diff: max_steps 20000000 → 24000000; env params + `object/anchorShare: 0.1`, + `task/liftBudgetSteps: 300.0`; unchanged: canonicalFraction
0.5, hold 50, perturb 1.0, object/mode 1 (trainUniform), morph/randomize 1; no curriculum block. **LR seam**: the linear schedule
lr = 3.0e-4 × (1 − step / max_steps) was 0 at 20M under max_steps 20M; under 24M it restarts at 3.0e-4 / 6 = 5.0e-5 at 20.0M and decays to
0 at 24M.

Resume (`transition_E.json`): from Prosthetic-20000063 (= 013D, pt sha d98044e9), `--resume` of run id 013. checkpoint.pt (written at 013D's
graceful exit) differs from Prosthetic-20000063.pt in bytes but is equal in all 342 tensors and values, so it was left as is. The status file
was the graceful-exit one (5 parameters at lesson 0); the two new fixed parameters are absent and default to lesson 0
(EnvironmentParameterManager.__init__). Log: "task/liftBudgetSteps ... value=300.0", first step 20010000.

## 1f. Training (`monitor.csv`, 50k-window means of the sampled-head training stats)

| step | Success/Canonical | Success/Lying | gate | lift | palm | steps/s (500k) | NaN |
|---|---|---|---|---|---|---|---|
| 20.0M (013D end) | 0.559 | 0.390 | 0.997 | 0.638 | | 394 | 0 |
| 20.5M | 0.620 | 0.420 | 0.995 | 0.677 | 0.607 | ~400 (window spans the D-to-E gap: logged 6) | 0 |
| 21.0M | 0.610 | 0.480 | 0.993 | 0.711 | 0.616 | 395 | 0 |
| 21.5M | 0.633 | 0.444 | 0.995 | 0.703 | 0.600 | 390 | 0 |
| 22.0M | 0.642 | 0.477 | 0.992 | 0.714 | 0.615 | 401 | 0 |
| 22.5M | 0.665 | 0.450 | 0.998 | 0.710 | 0.628 | 399 | 0 |
| 23.0M | 0.694 | 0.519 | 0.995 | 0.743 | 0.623 | 397 | 0 |
| 23.5M | 0.646 | 0.465 | 0.994 | 0.715 | 0.625 | 397 | 0 |
| 24.0M | 0.723 | 0.495 | 0.997 | 0.759 | 0.633 | 395 | 0 |

Graceful exit at 24000002 (10,099 s). 013E = Prosthetic-24000002: `results/013/013E/Prosthetic.onnx` sha256 8a3097a9243fb900 (pt d3ab63e1).
The trainer's end-of-run copy to results/013/Prosthetic.onnx was reverted to the 013D bytes (96ac32e6). No Editor ran during training.

## 1g. Evaluation (Editor relaunched after training, idle machine, throwaway first; deterministic head, one worker, K = 50, perturbation 1.0)

- Held-out 65-80, seeds 10001-10300 × passIndex 0-1, both presentations: canonical 0.730 / 0.780, random stable 0.560 / 0.573 (p0 / p1),
  paired per row with 013D's Part 3 passes (table above). Upright gains are on short and mid-length objects; lying gains mostly on tilted
  rest poses (+0.132 [+0.020, +0.243], n = 76). Lift-phase drops fell (canonical 115 → 91, lying 59 → 45); liftBudget ends on lying
  146 → 137 at the eval budget of 200.
- Anchor 0.883 [0.842, 0.915] (013D 0.853, 012 1.000): 33 drops, 2 reach failures.
- Masked stratum (canonical, held-out × 5, seeds 9301-9380, paired with new 013D passes): thumb-masked hands 5 / 11 / 14 pooled 0.013
  vs 0.004 (+0.008 [−0.008, +0.025]), still collapsed; easy hands 1 / 2 / 3 pooled 0.904 vs 0.825 (+0.079 [+0.036, +0.122]).

## 1h. Substrate

The gate fails on the anchor only, so **013D (Prosthetic-20000063, onnx 96ac32e6) is the BO substrate** for Parts 2-3. The stop rule
(both upright and lying below −0.06) is not met; both moved up.
