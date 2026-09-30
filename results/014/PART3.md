# Run 014 Part 3: gates, penetration localization, build (2026-09-30) — stopped before the throughput probe and the smoke

The checkpoint-2 rule ("no training on a hand whose penetration exceeds 2x the 5-digit baseline on any gate") flags the 3-, 6b- and 7-digit hands.
The probe and the smoke are trainer runs on all six compositions, so they were not started. Everything that needs no training is done.

## 1. The 7-digit penetration (11.0 mm): localized and resolved

Per-step trace (harness `penTrace`, G1 lift configuration, 9 trials per hand, `results/014/pen/`):

| hand | worst hand-object penetration | worst link-pair self-penetration |
|---|---|---|
| 5 | 1.70 mm (pinky tip, lift) | none |
| 6b (+ second thumb) | 1.91 mm (pinky base, close) | none |
| 6a (+ extra finger) | 21.1 mm (extra fingertip, close step 49) | 19.5 mm (extra finger middle / distal phalanx into the palm, close) |
| 7 | 30.0 mm (extra fingertip, second thumb tip, close) | 24.7 mm (extra finger into the palm, close) |
| 7, solver iterations 32 / 8 (diagnostic) | 17.3 mm | 18.3 mm (extra finger into the palm) |

- **Solver budget: rejected.** Doubling the iterations does not remove it.
- **Thumb against thumb: rejected.** The second thumb is clean on its own (6b) and never collides with the other thumb.
- **What survives: the extra finger's geometry.** The extrapolated MCP (82 mm along) sat 22 mm inside the rectangular palm box, so the flexing
  finger drove into the palm and the object.

Diagnostic placements on the 6a hand:

| placement | object max | self max |
|---|---|---|
| MCP at the pinky's along 91 mm | 2.03 mm | 0 |
| + the pinky's abduction / tilt | 1.96 mm | 0 |
| no palm widening | 1.67 mm | 0 |
| MCP 91 mm + no widening | 1.93 mm | 0 |

**Adopted: extra-finger MCP along = 91 mm (the pinky's).** It is one value in `HandSpec.extraFinger`. Across −52.5 mm, rest abduction −22°,
tilt 18° and the 21 mm palm widening are unchanged. After the change (no overrides):

| hand | lifts | object max | self | window max |
|---|---|---|---|---|
| 6a | 9/9 | 2.03 mm | 0 | 2.71 mm |
| 7 | 9/9 | 1.55 mm | 0 | 1.38 mm |

## 2. Gates by hand (`GATES_014.md`; `gates_slots/`, slot layout, spike-6 configs)

- **5-digit slot hand.** 1.00x the HEAD baseline on every gate.
- **Every hand, every file.** No NaN; G4 20/20 finite.
- **Lift and perturbation.** Every hand passes G1 (to 2.5 kg) and G2 (to perturbation 4.0).
- **Pinch.** The 3-digit hand fails 1 of 12 pinch trials at perturbation 2.0–4.0.
- **G6 push.** The 6b and 7-digit hands are pushed out already at 10 N. The 3-, 5- and 6a-digit hands hold to 40 N, and the 3-digit hand also at
  80 N. This is a feasibility result, not a penetration one.

**Flags under the 2x rule** (both reproduced on a repeat run, `gates_slots_repeat/`):

| hand | gate | this hand | 5-digit baseline | which link |
|---|---|---|---|---|
| 3 | G6 push 20 N, hold window | 8.20 / 7.84 mm (repeat) | 3.89 mm; the baseline's other push windows are 7.38 / 4.65 / 7.37 mm (10 / 40 / 80 N) | index tip, fixed-target close at the force limits |
| 6b | G3 pinch, perturbation 0.25–1.5, episode max | 1.92 / 1.85 mm | 0.75 mm | primary thumb proximal / distal phalanx during the lift |
| 7 | G3 pinch 1.0 kg, episode max | 2.47 / 2.59 mm | 1.19 mm | primary thumb proximal phalanx or index tip during the lift |
| 7 | G3 pinch, perturbation 0.25–1.5, episode max | 2.56 / 2.56 mm | 0.75 mm | primary thumb proximal phalanx during the lift |

None involves self-penetration.

- **The pinch flags are ratio artefacts.** The absolute values (1.9–2.6 mm) are the size of every hand's G1 maximum (1.4–2.6 mm). The 5-digit pinch
  baseline is unusually low (0.75–1.19 mm).
- **The 3-digit flag is about the baseline, not the hand.** Its G6 window is 7.8–8.2 mm, in line with the baseline's 10 N and 80 N windows. It sits at
  the spike-6 adopted TGS limit of ≤ 8 mm window penetration at the force limits.

## 3. Harness fixes and notes

- **G4 row fix.** A stability trial ended early by the agent (its grasp gate fired during the random walk, then a drop or budget ended the episode) lost
  its row. `WriteStabilityRow` now writes it with the agent's end reason. Legacy G4 now has 20 rows; the trial-3 row ends in a drop.
- **G4 is not reproducible run to run.** Each trial's θ is drawn at the agent's episode start, before the harness seeds the random walk, so draws
  differ between runs. Cross-hand G4 comparisons are therefore over the 20 draws, not per row.
- **G3 ranking note.** Added to `MLAGENTS_UPGRADE.md` (new lines): under TGS the pinch is not weaker than the power grasp up to 2.5 kg / perturbation
  4.0. G3 is a pass / fail feasibility gate from run 014 on.
- **Placement fixture.** With the widened palm (6a, 7) the palm box centre moves 10.5 mm ulnar, and the harness places the object relative to that
  centre.

## 4. Build

`Builds/Run014/Prosthetic.exe` (Dynamic_Scene_Train, default layout untouched): Build Finished: Success; Assembly-CSharp sha256 `2a53854233afae5f`; bank
copied (84 files). Player check without a trainer (`player_check/`):

- `--topology slots`: layout check passed (Slots); created sensors JointTokens[21, 20] + VectorSensor[28] -> OK; bank ready 81 shapes / 131 pose classes.
- no flag: Legacy; JointTokens[16, 13] + 10 ray sensors + VectorSensor[24] -> OK.

Configs written, not run: `Config/run_014_probe.yaml` (100k), `Config/run_014_smoke.yaml` (500k): the phase-A recipe in the slot layout, env_args
`--topology slots`, ladder min_lesson_length 1000, lift budget 300, anchor share 0.10.
