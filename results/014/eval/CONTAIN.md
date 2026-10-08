# Run 014 eval, pre-check: is the 5-digit hand inside the spawn envelope? (2026-10-06) — FAILS; eval not started

**Question (CJ).** Does the 5-digit hand, at every θ draw, lie inside the envelope (7 digits at length scale 1.2, widened palm)? If so, every placement the
envelope accepts is also accepted by 013E's own-hand filter, and 013E can be evaluated with the envelope forced so that all rows pair.

**Method.**
- Harness mode eval with spawnCheck: 300 seeded eval resets (seeds 12401–12700, held-out shapes, randomized θ, spawnStream). No policy step.
- At each reset, `ArmGraspAgent.HandInsideEnvelope(2 mm)` takes a 2 mm grid inside every collider of the own-hand filter (forearm, palm, present
  finger-group links) and tests each point against the 23 envelope colliders posed on the current palm. A point counts as inside within 0.05 mm.
- Two passes:
  - (a) legacy layout with the 013E model and the envelope forced (`forceSpawnEnvelope`): 013E's own colliders;
  - (b) slot layout, digits 5, with the 014 model.

| pass | resets | grid points | points outside | resets with any point outside | max outside > 1 mm | > 2 mm | > 5 mm | largest |
|---|---|---|---|---|---|---|---|---|
| (a) legacy, 013E | 300 | 21,939,045 | 31,134 | **300** | 84 | 4 | 1 | 12.86 mm (L_Forearm, seed 12626) |
| (b) slots, 5-digit | 300 | 21,932,856 | 31,155 | **300** | 67 | 2 | 1 | 14.34 mm |

Per-reset maximum excursion in (a): median 0.91 mm, 90th percentile 1.12 mm. The links that stick out by the most vary: middle phalanges
(middle / ring / index / pinky), thumbEnd, finger ends. The large cases are thumbEnd (3.2 mm, 3.1 mm), middleEnd (2.3 mm) and the forearm
(12.9 mm). The forearm copy in the envelope is identical in shape, so a forearm excursion means the forearm–palm relation (wrist angle) at
that reset differs from the one captured when the envelope was built.

**Containment does not hold.** Per CJ's rule the eval is stopped here; nothing else was run (no pairing pass, no eval block).

**Practical impact on the checked placements:** in 1,200 5-digit slot-layout resets with both filters evaluated on the same candidates
(spawncheck 2026-10-04: 900 held-out + 300 anchor; plus pass (b)), the envelope never chose a candidate that the own-hand filter rejects (0 / 1200). The
violations are small next to the candidates' clearance in these draws, but that is an empirical result, not the containment guarantee.

**Likely causes (not yet tested).**
1. The envelope is captured once at Initialize from a static rebuild. At an episode reset the joints are not exactly at that pose: the wrist and
   finger joints differ by small angles, which moves a link by about 1 mm, more at the forearm. A posed-per-link envelope (each copy following
   its own link) or a safety margin would close this.
2. Shorter fingers with flexed rest angles put their middle and end phalanges where a longer finger's capsules do not reach (thumbEnd, middle
   phalanges).

Files: `contain_legacy013E.csv`, `contain_slots5_014.csv`. Columns: seed, ordinal, composition, objIndex, pose, tested, envelopeRejects, handRejects,
handFirst, chosen, fallback, x, y, z, points, outside, maxOutMm, worst collider.

## CJ decision (2026-10-08) and backlog

- The pairing needs **decision agreement on the rows actually used**, not geometric containment. With a frozen-pose envelope the containment
  test fails by construction. Before any policy pass, every row of the paired cells (5-digit upright 300, lying 300, anchor 7001–7300) is checked
  with both filters on that row's candidates: the envelope and 013E's own-hand filter in the legacy layout. Requirement: every placement the
  envelope accepts is also accepted by 013E's own filter. Disagreeing rows, if any, are excluded from the 014 − 013E paired comparison only.
- Seeds 12401–12700 were used here for resets only (no policy outcome observed) and stay valid for the eval.
- **Backlog (not fixed now):** the envelope is captured once, in a static pose. Reset poses differ by about 1 mm (forearm up to 12.9 mm), so the
  training filter's guarantee is approximate. For future runs: pose the envelope per reset (each copy following its link) or add a measured margin.

## Decision agreement on the paired rows (2026-10-08; `pair_*.csv`, spawnCheck, resets only)

Legacy layout, 013E, envelope forced, with both filters evaluated on every candidate of every row; spawnStream on (seeds 12401–12700
held-out, 7001–7300 anchor).

| cell | rows | envelope accepted, own filter rejected (disagreements) | fallbacks | placement identical to 014 slot 5-digit | own filter picks the same attempt (matched rows) |
|---|---|---|---|---|---|
| 5-digit upright | 300 | **0** | 0 | 300 / 300 | 299 / 300 |
| 5-digit lying | 300 | **0** | 0 | 300 / 300 | 245 / 300 |
| anchor | 300 | **0** | 0 | 300 / 300 | 295 / 300 |

- **0 disagreements**, so the forced-envelope pairing uses all 300 rows per cell as the headline.
- Spawn positions and objects are also identical across the six slot compositions (300 / 300 in both poses), so composition comparisons pair by row.
- Matched rows (013E on its own filter places identically) are 299 / 245 / 295. These feed the sensitivity line.
