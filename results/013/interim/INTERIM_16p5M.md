# Run 013 interim check at 16.5M, phase D (2026-09-28 11:52; training not stopped or altered)

Checkpoint `results/013/Prosthetic/Prosthetic-16499988.onnx` (phase D: fixed canonical fraction 0.5, perturbation 1.0, since 13.5M) against 012
(`results/012/Prosthetic.onnx`) and 013C (`results/013/013C/Prosthetic.onnx` = Prosthetic-13499981, end of phase C), on the same 192
(seed, shape, theta) rows as the 12.25M check: train shapes 1-64 × 2 on seeds 9001-9128 and held-out shapes 65-80 × 4 on 9129-9192,
passIndex 0, in both presentations. Editor harness, deterministic head, one job worker, mu 1.0, K = 50, perturbation 1.0, mass as 012.
012 canonical rows are the v2 passes (`results/013_prep/passes_v2/`, recorded on an idle machine); every other cell was run during
training (`012/`, `013C/`, `013_16p5M/`), and 012 random-stable was re-run because the class-based pose draw of 7b3f8ca changes which
rest pose a seed gets. Differences are paired per row with a normal 95 % CI on the mean paired difference.

### canonical (192 rows: train shapes 1-64 x2 on 9001-9128, held-out 65-80 x4 on 9129-9192)

| subset | 012 | 013C | 013 @16.5M | 16.5M − 012 | 16.5M − 013C |
|---|---|---|---|---|---|
| all (n=192) | 0.615 [0.544, 0.681] | 0.031 [0.014, 0.066] | 0.521 [0.450, 0.590] | -0.094 [-0.179, -0.008] | +0.490 [+0.415, +0.565] |
| train shapes (n=128) | 0.633 [0.547, 0.711] | 0.023 [0.008, 0.067] | 0.531 [0.445, 0.616] | -0.102 [-0.209, +0.006] | +0.508 [+0.419, +0.597] |
| held-out shapes (n=64) | 0.578 [0.456, 0.691] | 0.047 [0.016, 0.129] | 0.500 [0.381, 0.619] | -0.078 [-0.217, +0.061] | +0.453 [+0.317, +0.590] |
| length < 130 mm (n=62) | 0.081 [0.035, 0.175] | 0.097 [0.045, 0.195] | 0.403 [0.290, 0.527] | +0.323 [+0.198, +0.447] | +0.306 [+0.168, +0.445] |
| length 130-210 mm (n=92) | 0.837 [0.748, 0.899] | 0.000 [-0.000, 0.040] | 0.598 [0.496, 0.692] | -0.239 [-0.349, -0.129] | +0.598 [+0.498, +0.698] |
| length 210-280 mm (n=38) | 0.947 [0.827, 0.985] | 0.000 [0.000, 0.092] | 0.526 [0.373, 0.675] | -0.421 [-0.594, -0.248] | +0.526 [+0.368, +0.685] |
- 012: gate 0.750, lifted 0.677, palm 0.844, thumb 0.828, reach failures 48
- 013C: gate 0.839, lifted 0.104, palm 0.302, thumb 0.688, reach failures 31
- 013 @16.5M: gate 0.948, lifted 0.630, palm 0.583, thumb 0.859, reach failures 11

### randomStable (192 rows: train shapes 1-64 x2 on 9001-9128, held-out 65-80 x4 on 9129-9192)

| subset | 012 | 013C | 013 @16.5M | 16.5M − 012 | 16.5M − 013C |
|---|---|---|---|---|---|
| all (n=192) | 0.005 [0.001, 0.029] | 0.583 [0.513, 0.651] | 0.536 [0.466, 0.606] | +0.531 [+0.459, +0.603] | -0.047 [-0.110, +0.017] |
| train shapes (n=128) | 0.008 [0.001, 0.043] | 0.594 [0.507, 0.675] | 0.547 [0.461, 0.630] | +0.539 [+0.450, +0.628] | -0.047 [-0.121, +0.028] |
| held-out shapes (n=64) | 0.000 [0.000, 0.057] | 0.562 [0.441, 0.677] | 0.516 [0.396, 0.634] | +0.516 [+0.393, +0.638] | -0.047 [-0.165, +0.071] |
| length < 130 mm (n=62) | 0.016 [0.003, 0.086] | 0.565 [0.441, 0.681] | 0.468 [0.349, 0.590] | +0.452 [+0.320, +0.583] | -0.097 [-0.204, +0.010] |
| length 130-210 mm (n=92) | 0.000 [-0.000, 0.040] | 0.598 [0.496, 0.692] | 0.598 [0.496, 0.692] | +0.598 [+0.498, +0.698] | +0.000 [-0.095, +0.095] |
| length 210-280 mm (n=38) | 0.000 [0.000, 0.092] | 0.579 [0.422, 0.721] | 0.500 [0.348, 0.652] | +0.500 [+0.341, +0.659] | -0.079 [-0.213, +0.055] |
- 012: gate 0.078, lifted 0.016, palm 0.786, thumb 0.057, reach failures 177
- 013C: gate 0.943, lifted 0.635, palm 0.615, thumb 0.969, reach failures 12
- 013 @16.5M: gate 0.964, lifted 0.641, palm 0.583, thumb 0.948, reach failures 7

## Reading

- **The mixture recovered most of the upright grasp without losing the lying one.** On upright objects 013 moved from 0.031 (013C) to
  0.521 in 3M steps of the 50/50 mix, +0.490 [+0.415, +0.565] paired; on rest poses it kept 0.536 against 0.583 for the lying specialist,
  −0.047 [−0.110, +0.017], inside noise. 012 remains the better upright grasper overall (0.615 against 0.521, −0.094 [−0.179, −0.008])
  and has no lying grasp at all (0.005).
- **Where the upright deficit to 012 sits.** It is on the long objects 012 grasps best: 130-210 mm −0.239, 210-280 mm −0.421 (0.53
  against 0.95). On short objects the 013 policy is far better than 012 in both presentations (upright 0.40 against 0.08): the
  lying-object skill transfers to upright pucks, which 012 never learned.
- **Mechanism.** Upright, the 16.5M policy gates more often than 012 (0.948 against 0.750) and has the fewest reach failures (11 of 192
  against 48), with palm contact recovering from 0.30 (013C) to 0.58 but still below 012's 0.84. The long-bar loss matches that:
  012 wedges a long upright bar against the palm; the 013 policy grasps more often with fingers and thumb (thumb 0.86) and less
  often with the palm.
- **Interference flag (brief).** Not raised at any row: Success/Lying stayed at 0.38-0.44 (100k means) through phase D while
  Success/Canonical rose from 0.016 (14.0M) to 0.292 (16.5M).
- **Caveat.** Passes run during training are not byte-reproducible: a 012 throwaway during training diverged at its second episode from
  the byte-stable idle-machine throwaways (hypothesis: frame-timing dependence under CPU load; to be tested after training). The rows
  above are correctly paired samples, but the final Part 3 evaluations will run on an idle machine.
