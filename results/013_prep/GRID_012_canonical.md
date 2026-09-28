# Hand × shape grid on run 012, canonical presentation (2026-09-27)

Question: on upright objects, does the thumb-base deficit of the three thumb-masked probe hands (5, 11, 14) change with object shape?
The same grid in randomStable mode (`ZERO_SHOT_v2.md`, Part 4c) sat at the floor for every hand and could not answer this.

Method: run-012 model, fixed theta per hand (probe hands 5, 11, 14 with the thumb-base group masked; 1, 2, 3 easy), all 80 bank shapes ×
5 episodes per hand (stratified `fixedIndex`, seeds 9301-9400 × passIndex 0-3), canonical pose, deterministic head, one job worker,
per-episode reseed, K = 50, perturbation 1.0, mu 1.0, mass log-uniform 0.2-1.5 kg, throwaway pass first (byte-identical to the v2
throwaways, hash 2676025693a9c0e2). Hands are paired on seed, pass and shape. Build: main after b906d26 with the 013-prep ObjectBank
(canonical path unchanged: the throwaway reproduces). Passes: `grid012_canonical/`, log `grid012_canonical.json`; tables by
`tools/eval/grid_tables.py`.


| hand | thumb base | episodes | success [95 % Wilson] | lifted | palm at end | thumb at end | contacts | reach fail | drops (lift phase / before first pulse / during / between / after) | gate→lift median steps |
|---|---|---|---|---|---|---|---|---|---|---|
| 5 | masked | 399 | 0.531 [0.482, 0.580] (n=399) | 0.617 | 0.820 | 0.732 | 3.55 | 0.253 | 54 / 18 / 2 / 9 / 3 | 14.0 |
| 11 | masked | 399 | 0.441 [0.393, 0.490] (n=399) | 0.526 | 0.777 | 0.717 | 3.23 | 0.383 | 37 / 17 / 1 / 12 / 3 | 12.0 |
| 14 | masked | 398 | 0.603 [0.554, 0.650] (n=398) | 0.711 | 0.759 | 0.741 | 3.86 | 0.201 | 37 / 15 / 0 / 23 / 3 | 14 |
| 1 | active | 399 | 0.619 [0.570, 0.665] (n=399) | 0.684 | 0.802 | 0.827 | 4.18 | 0.266 | 21 / 12 / 0 / 9 / 4 | 11 |
| 2 | active | 399 | 0.692 [0.645, 0.735] (n=399) | 0.752 | 0.852 | 0.870 | 4.45 | 0.211 | 16 / 11 / 2 / 7 / 3 | 13.0 |
| 3 | active | 400 | 0.620 [0.572, 0.666] (n=400) | 0.728 | 0.710 | 0.745 | 3.93 | 0.140 | 55 / 26 / 0 / 12 / 3 | 12 |

Paired over 398 episodes common to all six hands (same seed, passIndex and shape): thumb-masked mean 0.524, easy mean 0.643, **thumb-masked − easy -0.119 [-0.146, -0.092]**.

Per hand × length 2·a3 (success; n per cell) and the paired thumb-masked − easy difference within the family:

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | thumb-masked − easy [95 % CI] |
|---|---|---|---|---|---|---|---|
| length 130-210 mm | 0.69 (185) | 0.56 (185) | 0.75 (184) | 0.85 (185) | 0.90 (185) | 0.85 (185) | -0.205 [-0.249, -0.161] (n=184) |
| length 210-280 mm | 0.82 (85) | 0.76 (85) | 0.99 (85) | 0.85 (85) | 0.95 (85) | 0.88 (85) | -0.035 [-0.079, +0.008] (n=85) |
| length < 130 mm | 0.12 (129) | 0.05 (129) | 0.14 (129) | 0.13 (129) | 0.22 (129) | 0.12 (130) | -0.052 [-0.090, -0.013] (n=129) |

Per hand × cross-section 2·max(a1, a2) (success; n per cell) and the paired thumb-masked − easy difference within the family:

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | thumb-masked − easy [95 % CI] |
|---|---|---|---|---|---|---|---|
| cross 50-65 mm | 0.57 (104) | 0.45 (104) | 0.58 (103) | 0.65 (104) | 0.71 (104) | 0.60 (105) | -0.123 [-0.174, -0.072] (n=103) |
| cross 65-80 mm | 0.48 (250) | 0.42 (250) | 0.61 (250) | 0.59 (250) | 0.68 (250) | 0.61 (250) | -0.123 [-0.158, -0.088] (n=250) |
| cross < 50 mm | 0.73 (45) | 0.56 (45) | 0.60 (45) | 0.71 (45) | 0.73 (45) | 0.71 (45) | -0.089 [-0.158, -0.020] (n=45) |

Per hand × e1 (success; n per cell) and the paired thumb-masked − easy difference within the family:

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | thumb-masked − easy [95 % CI] |
|---|---|---|---|---|---|---|---|
| e1 0.3-0.7 | 0.52 (165) | 0.42 (165) | 0.60 (164) | 0.62 (165) | 0.70 (165) | 0.61 (165) | -0.128 [-0.172, -0.084] (n=164) |
| e1 0.7-1.1 | 0.62 (105) | 0.53 (105) | 0.69 (105) | 0.64 (105) | 0.74 (105) | 0.73 (105) | -0.092 [-0.141, -0.043] (n=105) |
| e1 1.1-1.5 | 0.48 (129) | 0.39 (129) | 0.53 (129) | 0.60 (129) | 0.64 (129) | 0.55 (130) | -0.129 [-0.175, -0.083] (n=129) |

Per hand × e2 (success; n per cell) and the paired thumb-masked − easy difference within the family:

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | thumb-masked − easy [95 % CI] |
|---|---|---|---|---|---|---|---|
| e2 0.3-0.7 | 0.53 (135) | 0.44 (135) | 0.60 (134) | 0.61 (135) | 0.71 (135) | 0.59 (135) | -0.114 [-0.160, -0.069] (n=134) |
| e2 0.7-1.1 | 0.43 (124) | 0.35 (124) | 0.48 (124) | 0.46 (124) | 0.51 (124) | 0.46 (125) | -0.056 [-0.095, -0.018] (n=124) |
| e2 1.1-1.5 | 0.63 (140) | 0.51 (140) | 0.71 (140) | 0.76 (140) | 0.84 (140) | 0.79 (140) | -0.179 [-0.229, -0.129] (n=140) |

Per hand × aspect a3 / max(a1, a2) (success; n per cell) and the paired thumb-masked − easy difference within the family:

| family | hand 5 | hand 11 | hand 14 | hand 1 | hand 2 | hand 3 | thumb-masked − easy [95 % CI] |
|---|---|---|---|---|---|---|---|
| aspect 1.5-3 | 0.44 (224) | 0.34 (224) | 0.52 (223) | 0.59 (224) | 0.66 (224) | 0.57 (225) | -0.173 [-0.212, -0.135] (n=223) |
| aspect < 1.5 | 0.07 (45) | 0.04 (45) | 0.07 (45) | 0.04 (45) | 0.09 (45) | 0.04 (45) | +0.000 [-0.058, +0.058] (n=45) |
| aspect >= 3 | 0.85 (130) | 0.75 (130) | 0.93 (130) | 0.87 (130) | 0.95 (130) | 0.90 (130) | -0.067 [-0.106, -0.027] (n=130) |

## Reading

The thumb-base deficit is real on upright bank objects and larger than on the cylinder: paired over 398 episodes the thumb-masked hands
succeed 0.524 against 0.643 for the easy hands, −0.119 [−0.146, −0.092] (on the anchor cylinder at the same perturbation level the gap
was a few points: 0.91-0.98 against 1.00, `results/bo/partB/probe.csv`). It depends on the object in a way that follows what the thumb has
to do. On long bars (210-280 mm, aspect ≥ 3) the fingers can clamp the object against the palm along its length and the missing thumb
drive matters little: −0.035 [−0.079, +0.008] and −0.067, with hand 14 at 0.99 on the long bars. On short pucks (< 130 mm, aspect < 1.5)
nobody succeeds (0.04-0.22), so there is nothing to lose. The gap opens on mid-length objects (130-210 mm: −0.205 [−0.249, −0.161];
aspect 1.5-3: −0.173), where the fingers alone cannot span enough of the object to wrap it and a driven thumb has to oppose them
across the cross-section, and on the rounded-diamond cross-sections (e2 1.1-1.5: −0.179), whose edges need opposition rather than a
palm press. The mechanism in the logged columns is the one of `HARD_HANDS.md`: at the end of an episode the thumb touches the object in
72-74 % of the thumb-masked episodes against 75-87 % for the easy hands, they hold fewer segments (3.2-3.9 against 3.9-4.5 finger groups),
and they reach the gate less often (reach-failure rate 0.20-0.38 against 0.14-0.27). Hand 11 is the weakest (0.44), consistent with its
two further masked groups (ring MCP and pinky DIP besides the thumb base). One easy hand, 3, loses as many objects in the lift phase as
hand 5 (55 and 54 lift-phase drops), so the easy-hand mean is not uniform either. For a morphology objective on the bank, the thumb-base
bit therefore has a shape-dependent effect of 0.04 to 0.21, concentrated on mid-length objects: range that did not exist on the cylinder.
