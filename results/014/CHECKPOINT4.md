# Run 014 checkpoint 4 (2026-10-02): after the smoke, before training

## G6 with the thumbs exempt in the placement check (`g6_thumbexempt/`)

| hand | 10 N | 20 N | 40 N | 80 N | window penetration | window contacts |
|---|---|---|---|---|---|---|
| 6b | held | held | held | held | 5.3–6.8 mm | 8.7–9.4 |
| 7 | held | held | held | held | 5.4–8.1 mm | 10.3–11.0 |
| 5 | held | held | held | pushed out | 3.9–7.4 mm | 5.7–7.0 |

- **6b and 7 pass**: they hold to 40 N like the 5-digit hand, and also at 80 N.
- **The change is a no-op on the 5-digit hand.** Its placement is unchanged (1.0 mm shift). The 20 / 40 / 80 N rows are identical in every numeric column
  to the previous run. The 10 N row, the first of the sequence, differs at the known first-run level (window 6.00 vs 5.97 mm).
- Criteria decisions recorded in `CRITERIA_014.md` (new lines):
  - (ii) at most 3 mm at task loads is the gate; it passes on all five hands.
  - The penetration × contacts check is retired.
  - (i) applies to G1 / G2 / G3 and passes on all five hands.
  - G4 / G6 self-penetration is reported, and the 013 hand shows it too.

## Player

`Builds/Run014/Prosthetic.exe` rebuilt with:
- composition-share indicators `Comp/Share{3,4,5,6a,6b,7}`;
- per-composition effort `Effort/Digits{3,4,5,6a,6b,7}`;
- the spawn statistics `Spawn/OverlapRejectRate*` and `Spawn/Fallback*`;
- `--episodeLog`.

Build Finished: Success; **Assembly-CSharp sha256 `39af4bebfbec4ea7`** (was 2a53854233afae5f); bank copied (84 files).

Player check without a trainer (`player_check2/`):
- `--topology slots`: layout check passed, created sensors JointTokens[21, 20] + VectorSensor[28] -> OK; the episode log writes.
- no flag: Legacy, JointTokens[16, 13] + 10 ray sensors + VectorSensor[24] -> OK.

## Throughput probe (`Config/run_014_probe.yaml`, run id 014_probe, 8 envs, `--topology slots`)

100k steps in 209.3 s: **478 steps/s** (479 from 10k to 100k). The threshold is 300; run 013 ran 390–410 on the legacy layout. Logs:
`results/014_logs/probe.*`.

## Smoke (`Config/run_014_smoke.yaml`, run id 014_smoke, 500k steps, slot layout; `SMOKE_014.md`)

- **Run.** 500k steps in 1108 s (451 steps/s). No error in the trainer log. No NaN / inf in any of the 122 scalars. Final checkpoint
  Prosthetic-500046. Hold ladder at Hold5 throughout.
- **Composition shares (3565 logged episodes)**: every composition inside its 95 % interval around the design share.

  | composition | 3 | 4 | 5 | 6a | 6b | 7 |
  |---|---|---|---|---|---|---|
  | logged share | 0.105 | 0.144 | 0.442 | 0.075 | 0.083 | 0.151 |
  | design share | 0.10 | 0.15 | 0.45 | 0.075 | 0.075 | 0.15 |

- **Channels.** Success / Gate / Lift by Digits3–7, Success/Digits6a and 6b, Comp/Share*, Effort/Digits* and Spawn/* are all populated.
- **Effort on 3-digit hands.** Finite in all 373 episodes (mean −0.088). The mean squared action per present joint and step is 0.115–0.117 on every
  composition, so the unnormalised effort return scales with the joint count, as decision 8 intends.
- **Gate, lift and hold fire on every composition**: one successful episode per composition with the hold completed (50 / 50 steps at K = 5); table in
  `SMOKE_014.md`.
- **Reach failure falls with digit count.** Episodes ending at MaxStep without a gate:

  | composition | 3 | 4 | 5 | 6a | 6b | 7 |
  |---|---|---|---|---|---|---|
  | reach failure | 0.37 | 0.21 | 0.08 | 0.075 | 0.105 | 0.05 |
  | success at 500k | 0.051 | 0.019 | 0.019 | 0.019 | 0.003 | 0.015 |

- **Spawn rejection by the open-hand overlap filter** (rejected / reach-accepted candidates):

  | composition | 3 | 4 | 5 | 6a | 6b | 7 |
  |---|---|---|---|---|---|---|
  | rate | 0.406 | 0.388 | 0.385 | 0.390 | 0.385 | **0.427** |

  **Flag: the 7-digit hand is 4.2 percentage points above the 5-digit rate.** No composition ever fell back (every episode found a candidate within
  50 attempts). The larger hand rejects more candidate spawns, so its accepted spawn positions are a slightly smaller subset of the disk.

## Notes

- **Episode-log analysis.** An episode ending at MaxStep is logged after ML-Agents has reset the step count, so its `steps` reads 0.
  `tools/eval/smoke_014.py` counts every ended episode. A first version dropped them, which hid the reach failures and biased the shares.
- **Commits** are local; nothing pushed.
