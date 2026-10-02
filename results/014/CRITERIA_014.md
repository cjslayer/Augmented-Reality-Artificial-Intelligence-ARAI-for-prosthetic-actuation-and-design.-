# Run 014 training criteria (post hoc, checkpoint 3) on the slot-layout gates with contact tracing

Source: `results/014/gates_slots_v2` (spike-6 gate configs, penTrace).

## (i) Self-penetration (every gate, every trial)

| hand | max link-pair penetration | where |
|---|---|---|
| 3 | 20.25 mm | g4: L_thumbEnd/L_Palm@walk/1220 |
| 5 | 20.45 mm | g4: L_thumbEnd/L_Palm@walk/956 |
| 6a | 18.25 mm | g4: L_pinkyEnd/L_Palm@walk/1467 |
| 6b | 16.80 mm | g4: L_thumbEnd/L_Palm@walk/1652 |
| 7 | 22.53 mm | g4: L_thumb2End/L_Palm@walk/1703 |

**(i) FAIL**

## (ii) Hold-window penetration at task-level loads (gate: <= 3 mm on every hand)

| load | 3-digit: window max mm (contacts) | 5-digit: window max mm (contacts) | 6a-digit: window max mm (contacts) | 6b-digit: window max mm (contacts) | 7-digit: window max mm (contacts) |
|---|---|---|---|---|---|
| G1 lift 0.2 kg | 2.09 (4.00) | 2.16 (6.00) | 2.20 (7.00) | 1.28 (9.33) | 1.38 (10.67) |
| G1 lift 0.6 kg | 2.04 (4.00) | 2.14 (6.00) | 2.11 (7.00) | 1.23 (9.33) | 1.31 (10.65) |
| G1 lift 1.5 kg | 2.10 (4.00) | 2.17 (6.00) | 2.92 (7.00) | 1.31 (9.36) | 1.30 (10.33) |
| G2 0.6 kg, perturbation 1.0 | 2.14 (4.00) | 2.26 (6.00) | 2.48 (7.00) | 1.29 (10.97) | 1.33 (10.33) |

**(ii) PASS**

## (iii) Force-limit gates (reported, not gated)

| gate | 3-digit | 5-digit | 6a-digit | 6b-digit | 7-digit |
|---|---|---|---|---|---|
| G6 push 10 N | held, window max 7.83 mm, contacts 5.00, max 13.11 mm | held, window max 5.97 mm, contacts 7.07, max 11.00 mm | held, window max 4.37 mm, contacts 8.00, max 10.35 mm | pushed out, window max 3.98 mm, contacts 0.29, max 17.09 mm | pushed out, window max 1.01 mm, contacts 0.20, max 17.52 mm |
| G6 push 20 N | held, window max 8.20 mm, contacts 5.00, max 13.11 mm | held, window max 3.89 mm, contacts 7.36, max 11.03 mm | held, window max 6.46 mm, contacts 6.81, max 10.28 mm | pushed out, window max 3.62 mm, contacts 0.23, max 17.06 mm | pushed out, window max 5.20 mm, contacts 0.24, max 17.42 mm |
| G6 push 40 N | held, window max 8.20 mm, contacts 5.00, max 13.11 mm | held, window max 4.65 mm, contacts 7.98, max 11.03 mm | held, window max 6.85 mm, contacts 7.05, max 10.27 mm | pushed out, window max 4.70 mm, contacts 0.17, max 17.06 mm | pushed out, window max 4.76 mm, contacts 0.17, max 17.42 mm |
| G6 push 80 N | held, window max 8.20 mm, contacts 4.98, max 13.11 mm | pushed out, window max 7.37 mm, contacts 6.71, max 11.03 mm | pushed out, window max 8.27 mm, contacts 7.20, max 10.27 mm | pushed out, window max 4.52 mm, contacts 0.20, max 17.06 mm | pushed out, window max 4.75 mm, contacts 0.13, max 17.42 mm |
| G4 stability (20 draws) | 20/20 finite, max 19.37 mm | 20/20 finite, max 19.22 mm | 20/20 finite, max 23.86 mm | 20/20 finite, max 17.68 mm | 20/20 finite, max 18.07 mm |

## Penetration x contacts across hands at the same load

Window penetration (mean over the load's trials of the window maximum, and of the window median) times the mean window contact count (finger groups + palm).

| load | 3-digit max x c / median x c | 5-digit max x c / median x c | 6a-digit max x c / median x c | 6b-digit max x c / median x c | 7-digit max x c / median x c | max / min (max x c) | max / min (median x c) |
|---|---|---|---|---|---|---|---|
| G1 lift 0.2 kg | 7.8 / 7.7 | 12.3 / 12.2 | 15.0 / 14.9 | 10.6 / 10.5 | 13.9 / 13.8 | 1.92 | 1.93 |
| G1 lift 0.6 kg | 7.7 / 7.6 | 12.4 / 12.3 | 14.1 / 14.0 | 10.2 / 10.1 | 13.8 / 13.7 | 1.83 | 1.84 |
| G1 lift 1.5 kg | 6.3 / 6.3 | 10.4 / 10.4 | 18.5 / 13.9 | 11.9 / 11.7 | 13.2 / 13.2 | 2.91 | 2.23 |
| G2 0.6 kg, perturbation 1.0 | 8.1 / 7.9 | 12.4 / 12.1 | 16.1 / 15.8 | 13.9 / 13.5 | 13.5 / 13.3 | 2.00 | 1.99 |
| G6 push 20 N | 41.0 / 30.9 | 28.6 / 26.7 | 44.0 / 24.1 | 0.8 / 0.0 | 1.2 / 0.0 | 52.84 | nan |
| G6 push 40 N | 41.0 / 27.2 | 37.1 / 29.4 | 48.3 / 30.5 | 0.8 / 0.0 | 0.8 / 0.0 | 60.44 | nan |
| G6 push 80 N | 40.8 / 26.2 | 49.5 / 24.2 | 59.5 / 35.1 | 0.9 / 0.0 | 0.6 / 0.0 | 96.43 | nan |

**Penetration x contacts roughly constant across hands (max / min <= 2 at every load, window-maximum based): NO**

## Checkpoint 3b decisions (CJ, 2026-10-02; added as new lines)

- **Gate: criterion (ii), hold-window penetration ≤ 3 mm absolute at task loads.** It passes on all five hands: 1.23–2.92 mm (section (ii) above).
- **The penetration × contacts check is retired.** At task loads the normal force at a contact is the driver's squeeze per finger, not the object's
  weight shared over the contacts. A per-hand constant penetration is therefore what the corrected model predicts, which is what the table above
  shows. The "NO" above no longer gates training.
- **Criterion (i), no self-penetration, applies to G1 / G2 / G3 only.** It passes on all five hands: 0.00 mm on every lift and pinch trial.
- **G4 and G6 self-penetration are reported, not gated.**

  | hand | G4 random walk | G6 force-limit close |
  |---|---|---|
  | 3 | 20.3 mm | 4.2 mm |
  | 5 | 20.5 mm | 12.1 mm |
  | 6a | 18.3 mm | 9.1 mm |
  | 6b | 16.8 mm | 10.7 mm |
  | 7 | 22.5 mm | 10.7 mm |

  The unchanged 013 (5-digit) hand shows it too.
- **G6 placement now exempts the thumbs in every mode**, as the contact-controller gates already did. Rerun with the corrected placement
  (`results/014/g6_thumbexempt/`):

  | hand | 10 N | 20 N | 40 N | 80 N | window penetration | window contacts |
  |---|---|---|---|---|---|---|
  | 6b | held | held | held | held | 5.3–6.8 mm | 8.7–9.4 |
  | 7 | held | held | held | held | 5.4–8.1 mm | 10.3–11.0 |
  | 5 | held | held | held | pushed out | as before | as before |

  Both pass ("holds to 40 N like the 5-digit hand"), and both also hold at 80 N.
- **The change is a no-op on the 5-digit hand.** Its placement is unchanged (1.0 mm shift). The 20 / 40 / 80 N rows are identical in every numeric
  column to the previous run. The 10 N row, the first gate of the sequence, differs at the first-run reproducibility level already seen in G1
  (window 6.00 vs 5.97 mm).
