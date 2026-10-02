# Run 014 Part 3 continuation (checkpoint-3 instructions, 2026-10-01): stopped before the throughput probe and the smoke

Two of the checkpoint-3 stop conditions fired:
1. Penetration × contacts is not roughly constant across hands at the same load.
2. The 6b hand is still pushed out at 10 N with the second thumb held at its rest pose.

No trainer was started. Sources:
- `CRITERIA_014.md`: the post hoc criteria and the penetration × contacts table.
- `GATES_014.md`: new-lines sections on contact counts and G4 seeding.
- `gates_slots_v2/`: all gates on 3 / 5 / 6a / 6b / 7 with the penetration and contact trace.
- `pushout/`: slip dumps, close-ups and placement traces.

## Criteria (post hoc)

**(i) No self-penetration on any hand: FAIL on every hand, including the 5-digit (013) hand.**

| hand | G4 random walk (link-pair maximum) | G6 force-limit close (link-pair maximum) |
|---|---|---|
| 3 | 20.3 mm (thumb tip / palm) | 4.2 mm |
| 5 | 20.5 mm (thumb tip / palm) | 12.1 mm (middle tip / thumb proximal phalanx, push-out at 80 N) |
| 6a | 18.3 mm (pinky tip / palm) | 9.1 mm (pinky base / pinky tip at the close) |
| 6b | 16.8 mm (thumb tip / palm) | 10.7 mm (pinky tip / second-thumb proximal phalanx, close step 3) |
| 7 | 22.5 mm (second-thumb tip / palm) | 10.7 mm (pinky base / second-thumb tip, close step 2) |

The lift and pinch gates (G1, G2, G3) have no self-penetration on any hand. Self-penetration occurs only where joints are driven to arbitrary targets
(G4) or at their force limits toward fixed targets (G6). It is the same order on the unchanged 5-digit hand, so it is a property of the shared contact
model under those drives, not of the new digits.

**(ii) Hold-window penetration ≤ 3 mm at task-level loads: PASS.**

| load | 3 | 5 | 6a | 6b | 7 |
|---|---|---|---|---|---|
| G1 0.2 kg | 2.09 | 2.16 | 2.20 | 1.28 | 1.38 |
| G1 0.6 kg | 2.04 | 2.14 | 2.11 | 1.23 | 1.31 |
| G1 1.5 kg | 2.10 | 2.17 | 2.92 | 1.31 | 1.30 |
| G2 0.6 kg, perturbation 1.0 | 2.14 | 2.26 | 2.48 | 1.29 | 1.33 |

Values are mm.

**(iii) Force-limit gates (reported, not gated)** are tabled in `CRITERIA_014.md`, with window contacts. G4 is 20/20 finite on every hand, with
maxima of 17.7–23.9 mm.

## Stop 1: penetration × contacts is not constant

At the same load, window penetration stays about 2.1 mm on the 3, 5 and 6a hands while contacts go from 4 to 7 (finger groups + palm). On the 6b and
7 hands it is about 1.3 mm with 9.3–10.7 contacts.

| load | penetration × contacts, 3 → 7 digits | max / min |
|---|---|---|
| G1 0.2 kg | 7.8, 12.3, 15.0, 10.6, 13.9 | 1.92 |
| G1 0.6 kg | 7.7, 12.4, 14.1, 10.2, 13.8 | 1.83 |
| G1 1.5 kg | 6.3, 10.4, 18.5, 11.9, 13.2 | 2.91 |
| G2 0.6 kg, perturbation 1.0 | 8.1, 12.4, 16.1, 13.9, 13.5 | 2.00 |

The window-median product gives the same picture (max / min 1.84–2.23). Penetration does not fall as 1 / contacts. It is roughly per-hand constant
across loads (0.2 to 1.5 kg change it by under 0.1 mm on most hands) and depends on the hand's geometry. The force-per-contact model does not
describe it.

The G6 rows are not comparable for 6b and 7 (see stop 2): the object is never inside their grasp, with about 0.2 window contacts.

## Stop 2: second-thumb push-out (G6)

**Masked test.** The 6b hand with the second thumb masked (thumb2Base, thumb2End held stiffly at the neutral pose, `maskGroups`) is pushed out at
10 N, as without the mask (`gates_slots_v2/h6b_thumb2masked`). Rule: the rest pose blocks the finger wrap, so stop and report the geometry.

**Contact geometry at push-out (6b, masked; `pushout/h6b_masked/`):**
- Slip at step 352, in the push-in phase.
- Every finger group is at its full-flexion target with no contact: the fingers closed in front of the object, not around it.
- The only link near the object is the second-thumb tip (separation 3 mm).
- The object is at (84, 87, 55) mm palm frame and still rests on the pedestal.

**Why (placement trace, `pushout/place3_*`):**
- G6 runs without the contact controller, so its placement overlap check includes the thumb groups.
- The object is normally placed at 35 mm along the palm normal (all hands, every gate).
- On the 6b and 7 hands the second thumb's open rest pose (60° palmar abduction, mirrored to the ulnar side) occupies that spot.
- The harness pushes the object out until it clears: 60 mm, the cap, to 94 mm out. That is beyond finger reach.
- The 3, 5 and 6a hands place at 35 mm (1.0–1.5 mm shift) and hold to 40 N (3-digit to 80 N).
- In G1 (contact controller on, thumbs exempt) the 6b and 7 hands place normally and lift 9/9 up to 2.5 kg.

**The design question.** The second thumb's open rest pose stands in the space in front of the palm that the fingers wrap around. The primary thumb's
rest pose is the same, but on the radial side, clear of the placement. In training the reset does not place the object into the hand (below), so the
policy would have to reach past the second thumb.

Options:
1. A different rest posture for the second thumb (less palmar abduction, or more radial).
2. Keep it, and treat G6 on the 6b / 7 hands as not applicable with this placement.
3. Exempt thumbs in the G6 placement, as the contact-controller gates already do.

## Object placement in the training reset: palm-independent (confirmed)

`ArmGraspAgent.SpawnCylinder` (ArmGraspAgent.cs, `SpawnCylinder`):
- The object rests on the pedestal at the scene's world `spawnCenter` plus a uniform disk offset (radius 0.09 m, `spawn_radius`).
- Candidates are accepted when their distance from the shoulder is within `reachRange` (0.40–0.54 m).
- A candidate overlapping any arm or finger collider of the open hand at reset is rejected (up to 50 attempts).

Nothing refers to the palm's width or centre; the hand's geometry enters only through that rejection. The harness's palm-relative placement is a test
fixture only.

## What else is ready

- `Builds/Run014` (2a53854233afae5f) predates this continuation's agent stats changes: composition-share indicators `Comp/Share{3,4,5,6a,6b,7}`,
  per-composition effort `Effort/Digits*`, and `--episodeLog`. Those are compiled in the Editor and need a rebuild before the probe.
- Probe and smoke configs: `Config/run_014_probe.yaml`, `Config/run_014_smoke.yaml`.
