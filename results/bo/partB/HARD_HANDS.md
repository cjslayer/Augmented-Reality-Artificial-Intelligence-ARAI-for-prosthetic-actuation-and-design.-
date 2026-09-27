# The four hard probe hands: what makes them hard (2026-09-27)

Question: the Part B probe (`PARTB.md`) found the run-012 policy at 0.95-0.99 mean success over 20 training-distribution
hands at every stressor level, with all the across-hand spread coming from probe hands 5, 10, 14 and 18. This file asks what
those hands are, how they fail, whether one theta dimension explains the deficit, and whether the hard hands are structurally
hard or unlucky.

Data: the 120 probe passes in `probe_passes/` (read only; 20 hands × 6 levels × 100 episodes on seeds 4001-4100, deterministic
head, one job worker, mu 1.0, K = 50) and **ten new Editor passes** in `sens_passes/` (one throwaway on seeds 6091-6100, one
baseline reproduction, six single-dimension moves and two combined moves; same instrument, same seed block, perturbation 2.0
with the 0.4-3.0 kg mass range). Rows follow `tools/eval/summarize_pass.py` (no warm-up abort occurred; reach failures count as
failures). Theta parameterization and training distribution: `tools/bo/theta_space.py` (67 dimensions; k on a log scale).
Nothing under `Assets/` or `tools/` was changed.

**Headline.** Probe hands 5 and 14 (and 11, the fifth-worst) are the three hands in the probe whose **thumb base (CMC) group is
masked**, so the thumb is driven only at its IP joint. Re-activating that one group at the hardest level lifts hand 5 from 0.76
to 0.97 and hand 14 from 0.57 to 0.89; moving the next two largest-|z| dimensions changes nothing beyond pass noise (one of
them, hand 5's pinky-MCP inertia, hurts), and moving all three together gives no more than the thumb alone (hand 14: 0.88) or
less (hand 5: 0.84). Hands 10 and 18 are mild (0.97 mean over the six levels) and their
failures are heavy-object, single-pass clusters. The one theta axis with a measurable effect on this task is therefore the
thumb-base actuation bit; no continuous dimension shows an effect above the ~0.1 resolution of a 100-episode pass.

## 1a. theta profiles of the hard hands

z-scores on the 67-dim unit-box parameterization of tools/bo/theta_space.py (k on a log scale, mask bits 0/1), against (A) the 20 probe hands and (B) 2000 draws from `sample_training_distribution(2000, seed=1)`. A mask bit that is 0 (group masked) has z = -2.0 against the training distribution by construction (P(active) = 0.8), so masked groups always appear; they are listed separately.

### Hand 5

| dimension | value | z vs 20 probe hands (A) | z vs training distribution (B) |
|---|---|---|---|
| mask_thumbBase | MASKED | -2.32 | -2.27 |
| mask_indexBase | MASKED | -1.69 | -2.02 |
| inertia_pinkyBase | 0.516 | -1.46 | -1.71 |
| zeta_middleMiddle | 0.305 | -1.75 | -1.70 |
| k_middleBase | 5.849 N m/rad | +1.35 | +1.70 |
| inertia_wristFlex | 1.957 | +1.63 | +1.68 |
| k_indexBase | 0.526 N m/rad | -1.47 | -1.66 |
| inertia_thumbBase | 1.943 | +1.03 | +1.59 |
| inertia_ringMiddle | 0.575 | -1.06 | -1.50 |
| inertia_middleMiddle | 1.841 | +2.05 | +1.41 |
| zeta_pinkyEnd | 0.897 | +1.77 | +1.25 |

Masked groups: indexBase, thumbBase. Largest-|z| continuous dimensions (B): inertia_pinkyBase (-1.71), zeta_middleMiddle (-1.70), k_middleBase (+1.70)

### Hand 10

| dimension | value | z vs 20 probe hands (A) | z vs training distribution (B) |
|---|---|---|---|
| mask_indexBase | MASKED | -1.69 | -2.02 |
| mask_middleBase | MASKED | -1.69 | -1.97 |
| mask_pinkyBase | MASKED | -1.49 | -1.97 |
| mask_ringEnd | MASKED | -1.69 | -1.96 |
| inertia_ringMiddle | 1.996 | +1.69 | +1.75 |
| zeta_middleBase | 0.304 | -1.64 | -1.69 |
| inertia_pinkyBase | 1.985 | +1.67 | +1.68 |
| inertia_thumbEnd | 1.966 | +1.34 | +1.63 |
| inertia_middleBase | 0.552 | -1.23 | -1.61 |
| k_ringBase | 5.439 N m/rad | +1.24 | +1.60 |
| zeta_indexMiddle | 0.970 | +1.47 | +1.55 |
| len_pinky | 1.183 | +1.64 | +1.55 |
| zeta_pinkyEnd | 0.365 | -1.81 | -1.39 |

Masked groups: indexBase, middleBase, ringEnd, pinkyBase. Largest-|z| continuous dimensions (B): inertia_ringMiddle (+1.75), zeta_middleBase (-1.69), inertia_pinkyBase (+1.68)

### Hand 14

| dimension | value | z vs 20 probe hands (A) | z vs training distribution (B) |
|---|---|---|---|
| mask_thumbBase | MASKED | -2.32 | -2.27 |
| mask_middleEnd | MASKED | -1.33 | -2.09 |
| k_indexEnd | 0.113 N m/rad | -1.29 | -1.57 |
| inertia_ringMiddle | 0.548 | -1.12 | -1.56 |
| len_index | 0.824 | -1.36 | -1.56 |
| zeta_wristPron | 0.335 | -1.47 | -1.53 |
| k_ringEnd | 0.118 N m/rad | -1.53 | -1.47 |
| zeta_pinkyMiddle | 0.934 | +1.54 | +1.45 |
| zeta_pinkyEnd | 0.377 | -1.72 | -1.33 |
| inertia_thumbBase | 0.759 | -1.63 | -1.12 |
| zeta_pinkyBase | 0.455 | -1.55 | -1.00 |
| zeta_indexBase | 0.845 | +1.66 | +0.97 |

Masked groups: middleEnd, thumbBase. Largest-|z| continuous dimensions (B): k_indexEnd (-1.57), inertia_ringMiddle (-1.56), len_index (-1.56)

### Hand 18

| dimension | value | z vs 20 probe hands (A) | z vs training distribution (B) |
|---|---|---|---|
| mask_pinkyBase | MASKED | -1.49 | -1.97 |
| mask_ringBase | MASKED | -2.32 | -1.93 |
| mask_pinkyEnd | MASKED | -1.95 | -1.92 |
| k_indexEnd | 0.100 N m/rad | -1.44 | -1.76 |
| inertia_wristPron | 0.503 | -1.56 | -1.71 |
| inertia_indexBase | 1.984 | +1.72 | +1.68 |
| zeta_thumbEnd | 0.981 | +1.45 | +1.65 |
| zeta_indexBase | 0.322 | -1.23 | -1.63 |
| zeta_middleEnd | 0.971 | +1.44 | +1.60 |
| k_ringMiddle | 0.333 N m/rad | -1.40 | -1.60 |
| inertia_ringMiddle | 1.906 | +1.51 | +1.55 |
| k_middleEnd | 0.882 N m/rad | +1.55 | +1.53 |
| zeta_indexEnd | 0.339 | -1.55 | -1.52 |
| zeta_pinkyEnd | 0.939 | +2.05 | +1.45 |
| inertia_indexEnd | 1.864 | +1.90 | +1.41 |
| zeta_indexMiddle | 0.418 | -1.90 | -1.17 |
| inertia_middleMiddle | 1.700 | +1.64 | +1.08 |

Masked groups: ringBase, pinkyBase, pinkyEnd. Largest-|z| continuous dimensions (B): k_indexEnd (-1.76), inertia_wristPron (-1.71), inertia_indexBase (+1.68)

### Dimensions with |z| >= 1.5 in at least 3 of the 4 hard hands

- reference A: zeta_pinkyEnd (4/4)
- reference B: inertia_ringMiddle (4/4)

### Derived summaries: hard hands against the 16 easy hands and the training distribution

| summary | hand 5 | hand 10 | hand 14 | hand 18 | easy-hand min | easy-hand median | easy-hand max | training 5th-95th pct |
|---|---|---|---|---|---|---|---|---|
| active groups | 12 | 10 | 12 | 11 | 10 | 11 | 13 | 9-13 |
| active finger groups (non-thumb) | 11 | 8 | 11 | 9 | 8 | 9 | 11 | 7-12 |
| total finger length (4 fingers, mm) | 298.4 | 310.5 | 273.5 | 321.9 | 279.5 | 308.1 | 331.4 | 277.7-337.6 |
| thumb chain length (mm) | 113.5 | 82.5 | 99.5 | 100.6 | 78.0 | 93.9 | 114.8 | 79.6-114.4 |
| thumb / mean finger length | 1.521 | 1.062 | 1.456 | 1.250 | 0.947 | 1.224 | 1.506 | 1.007-1.539 |
| hand span (palm + index + tip, mm) | 183.1 | 192.3 | 174.0 | 195.1 | 172.7 | 189.5 | 201.5 | 173.8-200.9 |
| mean finger length scale | 0.970 | 1.021 | 0.886 | 1.050 | 0.911 | 1.003 | 1.075 | 0.905-1.097 |
| mean k MCP (base) (N m/rad) | 2.856 | 2.174 | 3.746 | 1.323 | 1.429 | 2.117 | 3.860 | 1.072-3.532 |
| geo-mean k MCP (base) (N m/rad) | 1.756 | 1.587 | 3.707 | 1.266 | 1.169 | 1.744 | 3.682 | 0.965-3.144 |
| mean k PIP (middle) (N m/rad) | 0.612 | 1.175 | 0.780 | 0.581 | 0.511 | 1.232 | 1.769 | 0.602-1.836 |
| geo-mean k PIP (middle) (N m/rad) | 0.566 | 1.133 | 0.726 | 0.524 | 0.458 | 0.993 | 1.447 | 0.551-1.623 |
| mean k DIP (end) (N m/rad) | 0.415 | 0.412 | 0.318 | 0.355 | 0.192 | 0.362 | 0.683 | 0.203-0.609 |
| geo-mean k DIP (end) (N m/rad) | 0.358 | 0.389 | 0.245 | 0.254 | 0.180 | 0.300 | 0.597 | 0.184-0.541 |
| mean k thumb base (N m/rad) | 3.284 | 12.5 | 7.286 | 11.2 | 1.145 | 3.846 | 13.0 | 1.131-13.1 |
| geo-mean k thumb base (N m/rad) | 3.284 | 12.5 | 7.286 | 11.2 | 1.145 | 3.846 | 13.0 | 1.131-13.1 |
| mean k thumb end (N m/rad) | 1.246 | 0.687 | 0.426 | 2.498 | 0.311 | 0.940 | 2.843 | 0.331-2.662 |
| geo-mean k thumb end (N m/rad) | 1.246 | 0.687 | 0.426 | 2.498 | 0.311 | 0.940 | 2.843 | 0.331-2.662 |
| mean k wrist (N m/rad) | 4.023 | 3.818 | 1.938 | 2.044 | 1.559 | 4.391 | 7.379 | 1.508-7.069 |
| geo-mean k wrist (N m/rad) | 3.185 | 3.247 | 1.844 | 1.957 | 1.518 | 3.613 | 7.134 | 1.464-6.826 |
| mean k active finger groups (N m/rad) | 1.354 | 2.470 | 1.603 | 1.842 | 1.169 | 1.584 | 2.589 | 0.922-2.353 |
| mean zeta fingers | 0.587 | 0.558 | 0.610 | 0.606 | 0.555 | 0.651 | 0.763 | 0.564-0.739 |
| mean zeta wrist | 0.425 | 0.813 | 0.557 | 0.725 | 0.396 | 0.686 | 0.957 | 0.400-0.893 |
| mean inertia scale fingers | 1.330 | 1.234 | 1.103 | 1.421 | 1.008 | 1.262 | 1.369 | 1.057-1.438 |
| mean inertia scale wrist | 1.384 | 0.963 | 1.402 | 0.943 | 0.673 | 1.252 | 1.703 | 0.728-1.755 |
| wrist k flex / pron (N m/rad) | 6.48 / 1.57 | 1.81 / 5.83 | 2.53 / 1.34 | 2.63 / 1.45 | 1.02 / 1.36 | - | 9.26 / 9.90 | - |

Full theta of the hard hands (raw units):

- hand 5: len index 0.945, middle 0.898, ring 1.082, pinky 0.955, thumb 1.170; mask 01111111111101; k (N m/rad) indexBase 0.53, indexMiddle 0.45, indexEnd 0.82, middleBase 5.85, middleMiddle 1.08, middleEnd 0.26, ringBase 4.34, ringMiddle 0.46, ringEnd 0.20, pinkyBase 0.71, pinkyMiddle 0.45, pinkyEnd 0.37, thumbBase 3.28, thumbEnd 1.25, wristFlex 6.48, wristPron 1.57; zeta 0.61, 0.86, 0.93, 0.62, 0.30, 0.64, 0.39, 0.38, 0.43, 0.70, 0.64, 0.90, 0.43, 0.39, 0.39, 0.46; inertia 1.31, 1.58, 1.54, 1.18, 1.84, 0.91, 1.76, 0.58, 1.77, 0.52, 1.45, 0.64, 1.94, 1.61, 1.96, 0.81
- hand 10: len index 1.066, middle 0.978, ring 0.856, pinky 1.183, thumb 0.850; mask 01101111001111; k (N m/rad) indexBase 1.48, indexMiddle 1.41, indexEnd 0.48, middleBase 0.86, middleMiddle 1.35, middleEnd 0.61, ringBase 5.44, ringMiddle 1.26, ringEnd 0.28, pinkyBase 0.92, pinkyMiddle 0.69, pinkyEnd 0.28, thumbBase 12.51, thumbEnd 0.69, wristFlex 1.81, wristPron 5.83; zeta 0.39, 0.97, 0.60, 0.30, 0.59, 0.42, 0.42, 0.67, 0.56, 0.79, 0.35, 0.36, 0.61, 0.76, 0.95, 0.67; inertia 1.40, 1.02, 0.73, 0.55, 1.08, 1.11, 0.87, 2.00, 1.72, 1.98, 1.08, 0.80, 0.96, 1.97, 1.20, 0.72
- hand 14: len index 0.824, middle 0.997, ring 0.851, pinky 0.873, thumb 1.026; mask 11111011111101; k (N m/rad) indexBase 4.14, indexMiddle 0.47, indexEnd 0.11, middleBase 3.15, middleMiddle 0.52, middleEnd 0.57, ringBase 3.28, ringMiddle 1.07, ringEnd 0.12, pinkyBase 4.42, pinkyMiddle 1.06, pinkyEnd 0.47, thumbBase 7.29, thumbEnd 0.43, wristFlex 2.53, wristPron 1.34; zeta 0.84, 0.76, 0.55, 0.50, 0.77, 0.64, 0.70, 0.53, 0.35, 0.45, 0.93, 0.38, 0.43, 0.71, 0.78, 0.33; inertia 1.00, 1.79, 0.83, 1.03, 0.73, 1.08, 1.29, 0.55, 1.84, 0.99, 0.79, 1.80, 0.76, 0.97, 1.45, 1.36
- hand 18: len index 1.102, middle 0.968, ring 1.064, pinky 1.066, thumb 1.037; mask 11111101101011; k (N m/rad) indexBase 0.98, indexMiddle 0.45, indexEnd 0.10, middleBase 2.03, middleMiddle 1.07, middleEnd 0.88, ringBase 1.05, ringMiddle 0.33, ringEnd 0.25, pinkyBase 1.23, pinkyMiddle 0.47, pinkyEnd 0.18, thumbBase 11.20, thumbEnd 2.50, wristFlex 2.63, wristPron 1.45; zeta 0.32, 0.42, 0.34, 0.47, 0.59, 0.97, 0.44, 0.55, 0.40, 0.50, 0.72, 0.94, 0.85, 0.98, 0.90, 0.55; inertia 1.98, 0.98, 1.86, 0.89, 1.70, 1.12, 1.36, 1.91, 1.35, 1.21, 1.41, 1.25, 1.27, 1.59, 1.38, 0.50

### Reading 1a

- The rule "dimension with |z| >= 1.5 in >= 3 of 4 hard hands" returns `inertia_ringMiddle` against the training distribution
  (hands 5 and 14 low, 10 and 18 high: **opposite signs**, so not a common factor) and `zeta_pinkyEnd` against the 20-hand sample
  (5 and 18 high, 10 and 14 low: again opposite). No continuous dimension is shared with a common sign.
- The feature the two hardest hands share is the **masked thumb base** (group 12, `mask_thumbBase`, z = -2.27). In the training
  distribution 16.2 % of hands have it masked (`sample_training_distribution(2000, 1)`; the constraint of `MorphologyManager.Sample`
  only requires one of the two thumb groups to be active). In the probe, exactly three of the 20 hands have it masked (5, 11,
  14), and they are exactly the three worst hands by six-level mean success (table below); the chance of that under no effect is
  1 in 1,140.
- Hands 10 and 18 have the thumb base active. Hand 10 has four masked groups (index, middle and pinky MCP, ring DIP: only 8 active
  finger groups, the minimum in the probe alongside hand 11) and the slowest reach of all 20 hands; hand 18 has three masked
  ulnar-side groups (ring MCP, pinky MCP, pinky DIP) and the softest index DIP spring (0.10 N m/rad, the class minimum).
- A secondary candidate the 1c budget did not test: wrist pronation stiffness. `k_wristPron` is the individual dimension most
  correlated with six-level success (Spearman +0.69, `SECONDARY_OBJECTIVES.md`); hands 14, 18, 16 and 5 have the four softest
  pronation springs (1.34-1.57 N m/rad against a 1-10 range) and are four of the six worst hands. It is confounded with the thumb
  mask in this sample and stays a hypothesis.

### Masks of all 20 probe hands, ranked by six-level mean success

| hand | mask (groups 0-13) | masked groups | thumb base | wrist pronation k (N m/rad) | wrist flexion k (N m/rad) | mean success over the 6 levels |
|---|---|---|---|---|---|---|
| 14 (hard) | 11111011111101 | middleEnd, thumbBase | **masked** | 1.34 | 2.53 | 0.807 |
| 5 (hard) | 01111111111101 | indexBase, thumbBase | **masked** | 1.57 | 6.48 | 0.868 |
| 11 | 11111101111001 | ringBase, pinkyEnd, thumbBase | **masked** | 3.06 | 8.41 | 0.940 |
| 18 (hard) | 11111101101011 | ringBase, pinkyBase, pinkyEnd | active | 1.45 | 2.63 | 0.967 |
| 10 (hard) | 01101111001111 | indexBase, middleBase, ringEnd, pinkyBase | active | 5.83 | 1.81 | 0.972 |
| 16 | 11111110010111 | ringMiddle, ringEnd, pinkyMiddle | active | 1.36 | 2.95 | 0.977 |
| 0 | 11101011111111 | middleBase, middleEnd | active | 1.72 | 8.69 | 0.990 |
| 8 | 11111101011110 | ringBase, ringEnd, thumbEnd | active | 1.91 | 1.20 | 0.995 |
| 1 | 01111010111111 | indexBase, middleEnd, ringMiddle | active | 6.13 | 1.09 | 0.997 |
| 17 | 11111011100011 | middleEnd, pinkyBase, pinkyMiddle, pinkyEnd | active | 4.28 | 6.79 | 0.997 |
| 7 | 01111011101011 | indexBase, middleEnd, pinkyBase, pinkyEnd | active | 2.76 | 8.77 | 0.998 |
| 12 | 11100111110111 | middleBase, middleMiddle, pinkyMiddle | active | 4.19 | 1.02 | 0.998 |
| 19 | 11110111001110 | middleMiddle, ringEnd, pinkyBase, thumbEnd | active | 9.90 | 1.83 | 0.998 |
| 2 | 11110011111111 | middleMiddle, middleEnd | active | 2.06 | 2.77 | 1.000 |
| 3 | 11111111110111 | pinkyMiddle | active | 5.50 | 9.26 | 1.000 |
| 4 | 01101011111111 | indexBase, middleBase, middleEnd | active | 7.22 | 3.84 | 1.000 |
| 6 | 10111111111111 | indexMiddle | active | 4.46 | 2.86 | 1.000 |
| 9 | 10011110101111 | indexMiddle, indexEnd, ringMiddle, pinkyBase | active | 7.06 | 1.58 | 1.000 |
| 13 | 11101111010111 | middleBase, ringEnd, pinkyMiddle | active | 7.03 | 1.90 | 1.000 |
| 15 | 11010110111111 | indexEnd, middleMiddle, ringMiddle | active | 7.36 | 1.24 | 1.000 |

## 1b. Failure anatomy of the hard hands at the four hardest levels

Levels: perturb 2.0, mass 0.2-1.5 kg; perturb 3.0, mass 0.2-1.5 kg; perturb 1.0, mass 0.40-3.00 kg; perturb 2.0, mass 0.40-3.00 kg. Drop phase uses the logged pulse schedule (hold-step indices of the three pulses, each 5 steps long) and the hold step at which the episode ended; `lift phase` = the object was not in the hold state when it dropped (holdSteps 0: dropped before the first lift, or after the lift criterion was lost, which resets the hold counter and clears the schedule). `Reach failure` = MaxStep interruption with zero task steps (the policy never met the contact gate), counted as a failure.

### Hand 5

| level | n | success | ends | reach fail | drop phase | median hold step at drop | pulse active at drop |
|---|---|---|---|---|---|---|---|
| perturb 2.0, mass 0.2-1.5 kg | 100 | 0.89 | {'success': 89, 'drop': 6, 'reach failure (maxStep, 0 steps)': 5} | 0.05 | {'lift phase (no hold in progress)': 3, 'before first pulse': 3} | 8 | 0.00 |
| perturb 3.0, mass 0.2-1.5 kg | 100 | 0.85 | {'success': 85, 'drop': 13, 'reach failure (maxStep, 0 steps)': 2} | 0.02 | {'lift phase (no hold in progress)': 6, 'before first pulse': 4, 'between pulses': 1, 'after last pulse': 1, 'during pulse 2': 1} | 14 | 0.08 |
| perturb 1.0, mass 0.40-3.00 kg | 100 | 0.76 | {'success': 76, 'drop': 17, 'reach failure (maxStep, 0 steps)': 7} | 0.07 | {'between pulses': 2, 'before first pulse': 7, 'lift phase (no hold in progress)': 6, 'after last pulse': 2} | 24 | 0.00 |
| perturb 2.0, mass 0.40-3.00 kg | 100 | 0.76 | {'success': 76, 'drop': 20, 'reach failure (maxStep, 0 steps)': 4} | 0.04 | {'between pulses': 4, 'lift phase (no hold in progress)': 7, 'before first pulse': 8, 'after last pulse': 1} | 10 | 0.00 |
| **pooled 4 levels** | 400 | 0.815 | {'success': 326, 'drop': 56, 'reach failure (maxStep, 0 steps)': 18} | 0.045 | {'lift phase (no hold in progress)': 22, 'before first pulse': 22, 'between pulses': 7, 'after last pulse': 4, 'during pulse 2': 1} | 14 | 0.02 |

Pooled over the four levels, successes vs failures (drops in parentheses where different):

| quantity | successes | failures (drops) |
|---|---|---|
| n | 326 | 74 (56) |
| contacts at end | 4.11 | 1.51 (1.45) |
| distinct fingers at end | 2.77 | 0.81 (0.86) |
| palm touching at end | 0.81 | 0.26 (0.20) |
| thumb touching at end | 1 | 0.46 (0.36) |
| mass, mean / median (kg) | 0.94 / 0.74 | 1.19 / 0.98 |
| gripTorqueMean | 0.574 | 0.273 (0.277) |
| maxPenMm | 3.41 | 4.01 (3.87) |
| retShaping | 0.850 | 0.583 |
| median steps to gate (reach) | 84 | 98 |
| median steps gate -> lift | 10 | 1 |
| failures that reached the lift | - | 0.51 |
| failures by mass tercile (low / mid / high; cuts 0.56, 1.12 kg) | - | [20, 24, 30] of [130, 136, 134] |

Failure count by mass tercile per level (terciles within the level's 100 draws):

| level | mass tercile cuts (kg) | failures low / mid / high |
|---|---|---|
| perturb 2.0, mass 0.2-1.5 kg | 0.42, 0.73 | 3 / 3 / 5 |
| perturb 3.0, mass 0.2-1.5 kg | 0.42, 0.73 | 3 / 6 / 6 |
| perturb 1.0, mass 0.40-3.00 kg | 0.83, 1.46 | 7 / 6 / 11 |
| perturb 2.0, mass 0.40-3.00 kg | 0.83, 1.46 | 6 / 8 / 10 |

Seeds failing at >= 3 of the 4 levels: [4026, 4033, 4094] (note: the mass draw depends on the level's mass range, so a seed is the same spawn / theta-independent stream but not the same mass across mass ranges).

### Hand 10

| level | n | success | ends | reach fail | drop phase | median hold step at drop | pulse active at drop |
|---|---|---|---|---|---|---|---|
| perturb 2.0, mass 0.2-1.5 kg | 100 | 1.00 | {'success': 100} | 0.00 | {} | - | - |
| perturb 3.0, mass 0.2-1.5 kg | 100 | 1.00 | {'success': 100} | 0.00 | {} | - | - |
| perturb 1.0, mass 0.40-3.00 kg | 100 | 0.83 | {'success': 83, 'drop': 7, 'reach failure (maxStep, 0 steps)': 10} | 0.10 | {'before first pulse': 5, 'between pulses': 1, 'lift phase (no hold in progress)': 1} | 15 | 0.00 |
| perturb 2.0, mass 0.40-3.00 kg | 100 | 1.00 | {'success': 100} | 0.00 | {} | - | - |
| **pooled 4 levels** | 400 | 0.958 | {'success': 383, 'drop': 7, 'reach failure (maxStep, 0 steps)': 10} | 0.025 | {'before first pulse': 5, 'between pulses': 1, 'lift phase (no hold in progress)': 1} | 15 | 0.00 |

Pooled over the four levels, successes vs failures (drops in parentheses where different):

| quantity | successes | failures (drops) |
|---|---|---|
| n | 383 | 17 (7) |
| contacts at end | 4.59 | 2.18 (2.86) |
| distinct fingers at end | 1.30 | 0.88 (1) |
| palm touching at end | 1 | 0.65 (0.43) |
| thumb touching at end | 1 | 0.53 (0.57) |
| mass, mean / median (kg) | 0.97 / 0.77 | 1.37 / 1.37 |
| gripTorqueMean | 4.257 | 1.792 (2.189) |
| maxPenMm | 2.75 | 5.23 (5.11) |
| retShaping | 0.863 | 0.585 |
| median steps to gate (reach) | 100 | 160 |
| median steps gate -> lift | 11 | 1 |
| failures that reached the lift | - | 0.41 |
| failures by mass tercile (low / mid / high; cuts 0.56, 1.12 kg) | - | [2, 4, 11] of [130, 136, 134] |

Failure count by mass tercile per level (terciles within the level's 100 draws):

| level | mass tercile cuts (kg) | failures low / mid / high |
|---|---|---|
| perturb 2.0, mass 0.2-1.5 kg | 0.42, 0.73 | 0 / 0 / 0 |
| perturb 3.0, mass 0.2-1.5 kg | 0.42, 0.73 | 0 / 0 / 0 |
| perturb 1.0, mass 0.40-3.00 kg | 0.83, 1.46 | 4 / 6 / 7 |
| perturb 2.0, mass 0.40-3.00 kg | 0.83, 1.46 | 0 / 0 / 0 |

Seeds failing at >= 3 of the 4 levels: none (note: the mass draw depends on the level's mass range, so a seed is the same spawn / theta-independent stream but not the same mass across mass ranges).

### Hand 14

| level | n | success | ends | reach fail | drop phase | median hold step at drop | pulse active at drop |
|---|---|---|---|---|---|---|---|
| perturb 2.0, mass 0.2-1.5 kg | 100 | 0.85 | {'success': 85, 'drop': 8, 'reach failure (maxStep, 0 steps)': 5, 'liftBudget': 2} | 0.05 | {'before first pulse': 4, 'lift phase (no hold in progress)': 2, 'after last pulse': 1, 'between pulses': 1} | 14 | 0.00 |
| perturb 3.0, mass 0.2-1.5 kg | 100 | 0.87 | {'success': 87, 'liftBudget': 1, 'reach failure (maxStep, 0 steps)': 2, 'drop': 10} | 0.02 | {'before first pulse': 2, 'between pulses': 5, 'lift phase (no hold in progress)': 2, 'after last pulse': 1} | 72 | 0.00 |
| perturb 1.0, mass 0.40-3.00 kg | 100 | 0.66 | {'success': 66, 'drop': 21, 'reach failure (maxStep, 0 steps)': 9, 'taskBudget': 1, 'liftBudget': 3} | 0.09 | {'between pulses': 4, 'lift phase (no hold in progress)': 11, 'before first pulse': 5, 'during pulse 1': 1} | 0 | 0.05 |
| perturb 2.0, mass 0.40-3.00 kg | 100 | 0.57 | {'success': 57, 'liftBudget': 3, 'drop': 29, 'taskBudget': 1, 'reach failure (maxStep, 0 steps)': 10} | 0.10 | {'before first pulse': 10, 'between pulses': 6, 'during pulse 1': 1, 'lift phase (no hold in progress)': 11, 'after last pulse': 1} | 12 | 0.03 |
| **pooled 4 levels** | 400 | 0.738 | {'success': 295, 'drop': 68, 'reach failure (maxStep, 0 steps)': 26, 'liftBudget': 9, 'taskBudget': 2} | 0.065 | {'before first pulse': 21, 'lift phase (no hold in progress)': 26, 'after last pulse': 3, 'between pulses': 16, 'during pulse 1': 2} | 18 | 0.03 |

Pooled over the four levels, successes vs failures (drops in parentheses where different):

| quantity | successes | failures (drops) |
|---|---|---|
| n | 295 | 105 (68) |
| contacts at end | 4.91 | 2.76 (2.72) |
| distinct fingers at end | 3.42 | 1.66 (1.75) |
| palm touching at end | 0.71 | 0.43 (0.22) |
| thumb touching at end | 1 | 0.71 (0.63) |
| mass, mean / median (kg) | 0.83 / 0.68 | 1.41 / 1.28 |
| gripTorqueMean | 0.390 | 0.297 (0.289) |
| maxPenMm | 3.11 | 3.59 (3.65) |
| retShaping | 0.853 | 0.659 |
| median steps to gate (reach) | 86 | 93 |
| median steps gate -> lift | 13 | 16 |
| failures that reached the lift | - | 0.47 |
| failures by mass tercile (low / mid / high; cuts 0.56, 1.12 kg) | - | [16, 29, 60] of [130, 136, 134] |

Failure count by mass tercile per level (terciles within the level's 100 draws):

| level | mass tercile cuts (kg) | failures low / mid / high |
|---|---|---|
| perturb 2.0, mass 0.2-1.5 kg | 0.42, 0.73 | 5 / 3 / 7 |
| perturb 3.0, mass 0.2-1.5 kg | 0.42, 0.73 | 0 / 4 / 9 |
| perturb 1.0, mass 0.40-3.00 kg | 0.83, 1.46 | 7 / 13 / 14 |
| perturb 2.0, mass 0.40-3.00 kg | 0.83, 1.46 | 6 / 11 / 26 |

Seeds failing at >= 3 of the 4 levels: [4002, 4003, 4019, 4025, 4033, 4067, 4076, 4092, 4093] (note: the mass draw depends on the level's mass range, so a seed is the same spawn / theta-independent stream but not the same mass across mass ranges).

### Hand 18

| level | n | success | ends | reach fail | drop phase | median hold step at drop | pulse active at drop |
|---|---|---|---|---|---|---|---|
| perturb 2.0, mass 0.2-1.5 kg | 100 | 0.99 | {'success': 99, 'drop': 1} | 0.00 | {'between pulses': 1} | 77 | 0.00 |
| perturb 3.0, mass 0.2-1.5 kg | 100 | 0.99 | {'success': 99, 'reach failure (maxStep, 0 steps)': 1} | 0.01 | {} | - | - |
| perturb 1.0, mass 0.40-3.00 kg | 100 | 0.99 | {'success': 99, 'drop': 1} | 0.00 | {'before first pulse': 1} | 37 | 0.00 |
| perturb 2.0, mass 0.40-3.00 kg | 100 | 0.86 | {'success': 86, 'reach failure (maxStep, 0 steps)': 5, 'drop': 9} | 0.05 | {'between pulses': 3, 'after last pulse': 2, 'during pulse 1': 1, 'before first pulse': 2, 'lift phase (no hold in progress)': 1} | 74 | 0.11 |
| **pooled 4 levels** | 400 | 0.958 | {'success': 383, 'drop': 11, 'reach failure (maxStep, 0 steps)': 6} | 0.015 | {'between pulses': 4, 'before first pulse': 3, 'after last pulse': 2, 'during pulse 1': 1, 'lift phase (no hold in progress)': 1} | 74 | 0.09 |

Pooled over the four levels, successes vs failures (drops in parentheses where different):

| quantity | successes | failures (drops) |
|---|---|---|
| n | 383 | 17 (11) |
| contacts at end | 5.75 | 4.53 (5.64) |
| distinct fingers at end | 2.16 | 1.88 (2.18) |
| palm touching at end | 0.99 | 0.94 (0.91) |
| thumb touching at end | 1 | 0.88 (0.91) |
| mass, mean / median (kg) | 0.96 / 0.77 | 1.43 / 1.00 |
| gripTorqueMean | 3.952 | 2.475 (2.489) |
| maxPenMm | 3.79 | 4.81 (4.67) |
| retShaping | 0.876 | 0.754 |
| median steps to gate (reach) | 78 | 88 |
| median steps gate -> lift | 11 | 2 |
| failures that reached the lift | - | 0.65 |
| failures by mass tercile (low / mid / high; cuts 0.56, 1.12 kg) | - | [3, 6, 8] of [130, 136, 134] |

Failure count by mass tercile per level (terciles within the level's 100 draws):

| level | mass tercile cuts (kg) | failures low / mid / high |
|---|---|---|
| perturb 2.0, mass 0.2-1.5 kg | 0.42, 0.73 | 0 / 1 / 0 |
| perturb 3.0, mass 0.2-1.5 kg | 0.42, 0.73 | 0 / 0 / 1 |
| perturb 1.0, mass 0.40-3.00 kg | 0.83, 1.46 | 0 / 0 / 1 |
| perturb 2.0, mass 0.40-3.00 kg | 0.83, 1.46 | 5 / 3 / 6 |

Seeds failing at >= 3 of the 4 levels: none (note: the mass draw depends on the level's mass range, so a seed is the same spawn / theta-independent stream but not the same mass across mass ranges).

### Reference: the 16 easy hands pooled at the same four levels

n 6400, success 0.991, ends {'success': 6343, 'drop': 51, 'reach failure (maxStep, 0 steps)': 6}, reach failures 0.0009, drop phases {'before first pulse': 19, 'between pulses': 11, 'lift phase (no hold in progress)': 15, 'after last pulse': 3, 'during pulse 3': 1, 'during pulse 1': 2}, median hold step at drop 19.
Successes: contacts 5.79, palm 0.96, thumb 1.00, mass 0.98, gripTorqueMean 2.588, median gate step 77, gate->lift 10. Failures: contacts 2.60, palm 0.40, thumb 0.53, mass 1.37, gripTorqueMean 0.925, by mass tercile [15, 10, 32] of [2080, 2176, 2144].

Lowest level per hard hand: hand 5: perturb 1.0, mass 0.40-3.00 kg (0.76); hand 10: perturb 1.0, mass 0.40-3.00 kg (0.83); hand 14: perturb 2.0, mass 0.40-3.00 kg (0.57); hand 18: perturb 2.0, mass 0.40-3.00 kg (0.86)

### Success by object mass at the doubled-mass levels (perturb 1.0 and 2.0, 0.4-3.0 kg, pooled)

| hand | 0.4-0.8 kg | 0.8-1.2 kg | 1.2-1.6 kg | 1.6-2.0 kg | 2.0-2.5 kg | 2.5-3.0 kg |
|---|---|---|---|---|---|---|
| hand 5 | 0.80 (n=60) | 0.76 (n=46) | 0.81 (n=32) | 0.75 (n=24) | 0.75 (n=24) | 0.50 (n=14) |
| hand 10 | 0.93 (n=60) | 0.93 (n=46) | 0.88 (n=32) | 0.83 (n=24) | 0.96 (n=24) | 0.93 (n=14) |
| hand 14 | 0.80 (n=60) | 0.70 (n=46) | 0.59 (n=32) | 0.38 (n=24) | 0.46 (n=24) | 0.29 (n=14) |
| hand 18 | 0.92 (n=60) | 0.93 (n=46) | 0.97 (n=32) | 1.00 (n=24) | 0.88 (n=24) | 0.79 (n=14) |
| 16 easy hands | 0.99 (n=960) | 0.99 (n=736) | 0.99 (n=512) | 0.99 (n=384) | 0.98 (n=384) | 0.97 (n=224) |

### Success by object mass at the default-mass levels (perturb 2.0 and 3.0, 0.2-1.5 kg, pooled)

| hand | 0.20-0.40 kg | 0.40-0.60 kg | 0.60-0.80 kg | 0.80-1.00 kg | 1.00-1.25 kg | 1.25-1.51 kg |
|---|---|---|---|---|---|---|
| hand 5 | 0.92 (n=60) | 0.85 (n=46) | 0.84 (n=32) | 0.88 (n=24) | 0.79 (n=24) | 0.93 (n=14) |
| hand 10 | 1.00 (n=60) | 1.00 (n=46) | 1.00 (n=32) | 1.00 (n=24) | 1.00 (n=24) | 1.00 (n=14) |
| hand 14 | 0.92 (n=60) | 0.85 (n=46) | 0.97 (n=32) | 0.75 (n=24) | 0.83 (n=24) | 0.64 (n=14) |
| hand 18 | 1.00 (n=60) | 0.98 (n=46) | 1.00 (n=32) | 0.96 (n=24) | 1.00 (n=24) | 1.00 (n=14) |
| 16 easy hands | 0.99 (n=960) | 1.00 (n=736) | 1.00 (n=512) | 0.99 (n=384) | 0.99 (n=384) | 0.98 (n=224) |

### Mechanical reading, one paragraph per hand (hypotheses with the supporting numbers)

**Hand 14 (thumb base masked, middle DIP masked; shortest index 0.824; soft distal joints; 0.57 at the hardest level).** A
structural pinch failure with a mass dependence. Over the four hardest levels 105 of 400 episodes fail: 68 drops, 26 reach
failures, 11 budget ends. The drops are early: median hold step 18 of 500, 21 before the first pulse and 26 with no hold in
progress, only 2 during a pulse, so the perturbation pulses are not what kills it; the object leaves the hand as soon as it
carries weight. The failures are the heavy tail: 60 of 105 in the top mass tercile (> 1.12 kg), and at the doubled-mass
levels success falls from 0.80 at 0.4-0.8 kg to 0.29 at 2.5-3.0 kg while the 16 easy hands stay at 0.97-0.99 across the
same bins. At the final step of a drop the hand holds 2.7 contacts, palm 0.22, thumb 0.63 (successes: 4.9, 0.71, 1.00; easy
hands' successes: 5.8, 0.96, 1.00), and its mean grip torque is 0.39 N m against 2.6 for the easy hands. Reading: without a
driven CMC the thumb cannot be brought into opposition, so the grasp is a finger-side pinch against a passive thumb whose
friction capacity is set by the finger springs (distal k 0.11-0.12 N m/rad here) and does not scale with the load; above
about 1.5 kg the cylinder slides out under gravity shortly after lift. The 26 reach failures and 11 budget ends are the same
deficit at the gate: the gate needs thumb contact, and a passive thumb only meets the object for some spawn geometries.

**Hand 5 (thumb base masked, index MCP masked; long thumb 1.17; 0.76 at two levels).** The same passive-thumb pinch, but the
failure is geometric rather than a load limit. 74 of 400 fail: 56 drops and 18 reach failures. Drops split evenly between
"no hold in progress" (22) and "before the first pulse" (22), median hold step 14, one drop during a pulse. Unlike hand 14
the failures are not concentrated in the heavy tail (20 / 24 / 30 by tercile), and success versus mass is flat at 0.75-0.81
up to 2.5 kg before falling to 0.50 above it. At a drop the hand holds 1.45 contacts, palm 0.20, thumb 0.36, and the failed
episodes reach the lift criterion one step after the gate (median) against ten for successes: the gate is met by a marginal
six-segment set (middle, ring, pinky plus a passive thumb touch, with the index MCP also passive) that does not cage the
cylinder, and on release it slips before a hold forms. The three seeds that fail at three or more levels (4026, 4033, 4094)
point to spawn geometries the passive thumb cannot reach rather than to mass.

**Hand 10 (index, middle and pinky MCP and ring DIP masked; stiff thumb base 12.5 N m/rad; 0.83 at one level).** Not a
structural deficit at the levels tried: 100 / 100 at perturbation 2.0 and 3.0 with the default masses and 100 / 100 at
perturbation 2.0 with the doubled masses, and 83 / 100 at perturbation 1.0 with the doubled masses, on the same seeds. The
17 failures of that single pass are 10 reach failures and 7 drops, heavy (median 1.37 kg, 11 of 17 in the top tercile), with a
reach time of 160 steps against 100 for the successes; the pass-level noise check in `SECONDARY_OBJECTIVES.md` shows this
hand's reach-phase means moving between passes by less than episode noise predicts, so the cluster is an episode-chaining
event on one pass (the PhysX scene persists across episodes; a pass at a different perturbation scale differs only through
that chaining, and it produced no failures). What is real about hand 10 is the slow closure: with three passive MCPs its
reach time is the slowest of the 20 hands (105-110 steps against 74-85 for the easy hands), which is the mechanism by which a
heavy object plus an unlucky episode sequence turns into MaxStep reach failures.

**Hand 18 (ring MCP, pinky MCP, pinky DIP masked; index DIP 0.10 N m/rad; 0.86 at one level).** A genuine but mild strength
failure under load. 17 of 400 fail, 14 of them in one pass (perturbation 2.0, doubled masses): 9 drops and 5 reach failures.
Its drops are the late ones: median hold step 74, four between pulses, two after the last pulse, one during a pulse, and the
highest pulse-active-at-drop share of the four hands (0.11). At the drop the hand still holds 5.6 contacts, palm 0.91, thumb
0.91: a full power grasp that lets go, at 1.43 kg mean mass (successes 0.96 kg), with a grip torque of 2.5 N m against 4.0
for its own successes. Reading: two passive ulnar MCPs and the softest distal index spring leave less holding torque on the
ulnar side, which shows only at the heavy tail under 2 x pulses; the same seeds pass at perturbation 1.0 with the same
masses (99 / 100). Unlucky, in the sense of the brief: a mass-tail interaction, not a deficit visible at the training
distribution's own levels.

## 1c. Local sensitivity: one dimension at a time (ten Editor passes)

Design. The two hardest hands are 14 (0.57 at perturbation 2.0, 0.4-3.0 kg) and 5 (0.76 at both doubled-mass levels; the
perturbation-2.0 level was taken as the harder of the tie, and it is the level of hand 14, so every pass below shares one
level and one seed block). For each hand the three dimensions with the largest |z| against the training distribution (1a,
reference B) were moved one at a time to the training-distribution median, holding the other 66 fixed: for a mask bit the
median is "active"; for a continuous dimension it is the median of 2,000 draws in the unit box (the geometric mean of the
class range for k, 1.25 for an inertia scale). A fourth pass per hand moves all three together, and one pass repeats the hand
14 baseline unchanged, to measure how well the probe pass reproduces in a fresh Editor session. Seeds 4001-4100, n = 100,
deterministic head, one job worker, throwaway pass first; CSVs in `sens_passes/`. Ten passes were used of the cap of twelve
(one throwaway + nine measured); no pass failed.

Easy-hand success at perturb 2.0, mass 0.4-3.0 kg: min 0.93, median 1.00, max 1.00 (16 hands).

| hand | dimension moved | value before -> after | success before (probe pass) | success after [95 % Wilson] | change | reach failures before -> after | palm before -> after | contacts before -> after | after inside the easy-hand range (>= 0.93)? |
|---|---|---|---|---|---|---|---|---|---|
| 14 | none (baseline reproduction) | (same theta, new Editor session) | 0.57 [0.47, 0.66] | 0.64 [0.54, 0.73] | +0.07 | 0.10 -> 0.07 | 0.51 -> 0.53 | 3.99 -> 4.22 | no |
| 14 | mask_thumbBase | masked -> active | 0.57 [0.47, 0.66] | 0.89 [0.81, 0.94] | +0.32 | 0.10 -> 0.01 | 0.51 -> 0.98 | 3.99 -> 6.47 | no |
| 14 | mask_middleEnd | masked -> active | 0.57 [0.47, 0.66] | 0.54 [0.44, 0.63] | -0.03 | 0.10 -> 0.14 | 0.51 -> 0.65 | 3.99 -> 3.87 | no |
| 14 | k_indexEnd | 0.113 -> 0.315 | 0.57 [0.47, 0.66] | 0.55 [0.45, 0.64] | -0.02 | 0.10 -> 0.17 | 0.51 -> 0.55 | 3.99 -> 3.66 | no |
| 14 | all three | all three moves together | 0.57 [0.47, 0.66] | 0.88 [0.80, 0.93] | +0.31 | 0.10 -> 0.00 | 0.51 -> 0.96 | 3.99 -> 6.55 | no |
| 5 | mask_thumbBase | masked -> active | 0.76 [0.67, 0.83] | 0.97 [0.92, 0.99] | +0.21 | 0.04 -> 0.00 | 0.59 -> 1.00 | 3.41 -> 5.62 | yes |
| 5 | mask_indexBase | masked -> active | 0.76 [0.67, 0.83] | 0.85 [0.77, 0.91] | +0.09 | 0.04 -> 0.02 | 0.59 -> 0.72 | 3.41 -> 4.63 | no |
| 5 | inertia_pinkyBase | 0.516 -> 1.265 | 0.76 [0.67, 0.83] | 0.65 [0.55, 0.74] | -0.11 | 0.04 -> 0.07 | 0.59 -> 0.63 | 3.41 -> 3.26 | no |
| 5 | all three | all three moves together | 0.76 [0.67, 0.83] | 0.84 [0.76, 0.90] | +0.08 | 0.04 -> 0.05 | 0.59 -> 0.90 | 3.41 -> 5.23 | no |

Pass hashes (data rows): sens_h14_baseline_repro f906342985a71ad4, sens_h14_mask_thumbBase cc9c582a51068774, sens_h14_mask_middleEnd 296353a19de6d6e0, sens_h14_k_indexEnd e33adc25a8be58d8, sens_h14_all3 210ba41de37adfd6, sens_h05_mask_thumbBase 91a5b28f01e50ff9, sens_h05_mask_indexBase d5afb74a8ce33726, sens_h05_inertia_pinkyBase eb0ec1f2bae0240a, sens_h05_all3 045b6d11c2fd4f4a
Editor passes used: 10 (one throwaway + 9 measured), wall 466 s


**Reproduction across Editor sessions.** The baseline re-run matched the probe pass on every mass draw (100 / 100: the
per-episode reseed works) and on the first two episodes, then diverged from episode 3 (both drops, at hold step 44 vs 396);
98 of 100 rows differ in some column and 31 in the success flag, netting +0.07. Passes are byte-reproducible within a
session (the probe's cross-checks, `PARTB.md`) but this hand's episodes are chaotic enough that a fresh session gives a
different realisation of the same distribution. Every "change" column above should therefore be read against a
session-to-session band of about ±0.07 for a hand in this regime; the thumb-base moves (+0.21, +0.32) are three to four
times that band, the other moves are inside it.

**Does a single dimension explain the deficit?** Yes for the thumb base. Hand 5 with the thumb base active scores 0.97
[0.92, 0.99], inside the easy-hand range at this level (0.93-1.00), with palm contact 0.59 → 1.00 and 3.4 → 5.6 contacts at
the end. Hand 14 with the thumb base active scores 0.89 [0.81, 0.94], just below the easy-hand minimum, with palm contact
0.51 → 0.98 and 4.0 → 6.5 contacts; its residual is consistent with what remains of the hand (the shortest index of the 20,
0.824, and distal springs at the class floor). Moving all three dimensions together gives 0.88 for hand 14, no more than
the thumb alone, so the combination adds nothing; the middle-DIP mask and the index-DIP stiffness are inside the noise band
(-0.03, -0.02). For hand 5 the index-MCP mask gives +0.09 (borderline) and the pinky-MCP inertia scale -0.11; the combined
move scores 0.84, below the thumb-only 0.97, so the inertia move hurts in both passes where it appears (-0.11 alone, -0.13
in combination) and is plausibly a real small effect of the wrong sign for "move to the median": hand 5's unusually light
pinky MCP link (inertia scale 0.52) was helping it, not hurting it. The deficit is one dimension, not a combination; the
other dimensions the |z| rule picked are either inert or, once, mildly protective.

## 1d. Verdict

- **Which theta axes matter on this task.** One discrete axis: the thumb-base (CMC) actuation bit. Effect size at the
  hardest level: +0.21 (hand 5) and +0.32 (hand 14) success, with palm contact rising from about 0.5 to about 1.0 and
  end-contacts by 2.2-2.5 groups, against a session noise band of about 0.07. It is the strongest single-dimension
  correlate of six-level success across the 20 hands (Spearman +0.63) after wrist-pronation stiffness (+0.69), which this
  budget did not test and which is confounded with the mask in the sample. No continuous dimension shows an effect above
  the resolution of a 100-episode pass in the moves tried (three moves, -0.11 to +0.09).
- **Hard or unlucky.** Hands 5 and 14 are hard: a structural deficit (a passive thumb CMC turns the power grasp into a
  finger-side pinch against an undriven thumb; hand 14's version fails with load, hand 5's fails geometrically at the gate).
  Hands 10 and 18 are unlucky: 0.97 mean success over six levels, with failures confined to one pass each and to the heavy
  tail of the doubled mass range (hand 10's cluster is an episode-chaining event on one pass; hand 18's is a genuine but
  mild strength limit under 2 x pulses at 1.4 kg).
- **What this means for a morphology objective on the current task.** Over the training distribution the objective is close
  to a step function of one mask bit with 16 % prevalence, plus pass noise. A campaign would rediscover "keep the thumb
  base driven" and then find nothing else at the ±0.1 resolution. Whether thumb-base-masked hands belong in the training
  distribution at all (the `MorphologyManager.Sample` constraint only requires one active thumb group) is a design decision
  for the next run rather than something BO should spend evaluations on.
- **Open.** The wrist-pronation hypothesis (one pass: hand 14 with `k_wristPron` moved 1.34 → 3.16 N m/rad, at the same
  level) and the hand-14 residual after the thumb fix (index length or distal stiffness) are the two cheapest follow-ups,
  two or three passes each; neither was run, the cap being ten of twelve and the question of the brief answered.
