# Run 014 gates by hand (slot layout) vs the fresh 5-digit TGS baseline (unmodified HEAD, `results/014/gates_baseline`)

Cells: trials passed (lift / pinch: success; G4: finite; G6: episode not ended by the agent, 'held' = not pushed out), max = the agent's per-episode maximum
finger-object penetration (mm, ratio to the baseline), win = the hold / squeeze / push window maximum (mm, ratio). **>2x** marks the checkpoint-2 rule.

| gate | baseline (5-digit, HEAD) | 3-digit | 5-digit | 6a-digit | 6b-digit | 7-digit |
|---|---|---|---|---|---|---|
| G1 lift 0.2 / 0.6 / 1.5 kg | 9/9, max 1.72, win 2.23 | 9/9, max 1.39 (0.81x), win 1.95 (0.87x) | 9/9, max 1.69 (0.98x), win 2.17 (0.97x) | 9/9, max 2.03 (1.18x), win 2.92 (1.31x) | 9/9, max 1.91 (1.11x), win 1.31 (0.59x) | 9/9, max 2.55 (1.48x), win 1.38 (0.62x) |
| G1 ext 2.0 / 2.5 kg | 6/6, max 1.83, win 1.50 | 6/6, max 1.50 (0.82x), win 2.91 (1.94x) | 6/6, max 1.83 (1.00x), win 1.50 (1.00x) | 6/6, max 2.00 (1.09x), win 1.49 (0.99x) | 6/6, max 1.91 (1.04x), win 1.29 (0.86x) | 6/6, max 2.47 (1.35x), win 1.30 (0.87x) |
| G2 perturb 0.25-1.5 | 18/18, max 1.62, win 2.26 | 18/18, max 0.63 (0.39x), win 2.17 (0.96x) | 18/18, max 1.62 (1.00x), win 2.26 (1.00x) | 18/18, max 1.96 (1.21x), win 2.83 (1.25x) | 18/18, max 1.85 (1.14x), win 1.34 (0.59x) | 18/18, max 2.50 (1.54x), win 1.52 (0.67x) |
| G2 ext 2.0-4.0 | 12/12, max 1.81, win 3.86 | 12/12, max 2.71 (1.50x), win 5.45 (1.41x) | 12/12, max 1.81 (1.00x), win 3.86 (1.00x) | 12/12, max 2.09 (1.15x), win 3.92 (1.02x) | 12/12, max 2.78 (1.54x), win 2.78 (0.72x) | 12/12, max 2.59 (1.43x), win 1.52 (0.39x) |
| G3 pinch 0.2 / 0.6 / 1.5 kg | 9/9, max 1.38, win 2.00 | 9/9, max 1.37 (0.99x), win 1.74 (0.87x) | 9/9, max 1.38 (1.00x), win 2.00 (1.00x) | 9/9, max 0.88 (0.64x), win 2.09 (1.04x) | 9/9, max 1.81 (1.31x), win 1.11 (0.56x) | 9/9, max 2.46 (1.78x), win 0.88 (0.44x) |
| G3 pinch 1.0 kg | 3/3, max 1.19, win 1.88 | 3/3, max 1.13 (0.95x), win 1.81 (0.96x) | 3/3, max 1.19 (1.00x), win 1.88 (1.00x) | 3/3, max 0.71 (0.60x), win 1.97 (1.05x) | 3/3, max 1.83 (1.54x), win 1.05 (0.56x) | 3/3, max 2.47 (2.08x), win 0.87 (0.46x) **>2x** |
| G3 pinch perturb 0.25-1.5 | 18/18, max 0.75, win 2.08 | 18/18, max 0.73 (0.97x), win 2.11 (1.01x) | 18/18, max 0.75 (1.00x), win 2.08 (1.00x) | 18/18, max 0.76 (1.01x), win 1.90 (0.91x) | 18/18, max 1.92 (2.56x), win 1.08 (0.52x) **>2x** | 18/18, max 2.56 (3.41x), win 0.96 (0.46x) **>2x** |
| G3 pinch perturb 2.0-4.0 | 12/12, max 4.02, win 5.63 | 11/12, max 3.53 (0.88x), win 7.06 (1.25x) | 12/12, max 4.02 (1.00x), win 5.63 (1.00x) | 12/12, max 3.26 (0.81x), win 5.67 (1.01x) | 12/12, max 3.02 (0.75x), win 3.40 (0.60x) | 12/12, max 3.12 (0.78x), win 3.23 (0.57x) |
| G4 stability (20 draws) | 19/19, max 24.99, win nan | 20/20, max 19.37 (0.78x), win nan (nanx) | 20/20, max 19.22 (0.77x), win nan (nanx) | 20/20, max 23.86 (0.95x), win nan (nanx) | 20/20, max 17.68 (0.71x), win nan (nanx) | 20/20, max 18.07 (0.72x), win nan (nanx) |
| G6 push 10 N | 1/1 held 1, max 11.91, win 7.38 | 1/1 held 1, max 13.11 (1.10x), win 7.83 (1.06x) | 1/1 held 1, max 11.00 (0.92x), win 5.97 (0.81x) | 1/1 held 1, max 10.35 (0.87x), win 4.37 (0.59x) | 0/1 held 0, max 17.09 (1.43x), win 3.98 (0.54x) | 0/1 held 0, max 17.52 (1.47x), win 1.01 (0.14x) |
| G6 push 20 N | 1/1 held 1, max 11.03, win 3.89 | 1/1 held 1, max 13.11 (1.19x), win 8.20 (2.11x) **>2x** | 1/1 held 1, max 11.03 (1.00x), win 3.89 (1.00x) | 1/1 held 1, max 10.28 (0.93x), win 6.46 (1.66x) | 0/1 held 0, max 17.06 (1.55x), win 3.62 (0.93x) | 0/1 held 0, max 17.42 (1.58x), win 5.20 (1.34x) |
| G6 push 40 N | 1/1 held 1, max 11.03, win 4.65 | 1/1 held 1, max 13.11 (1.19x), win 8.20 (1.76x) | 1/1 held 1, max 11.03 (1.00x), win 4.65 (1.00x) | 1/1 held 1, max 10.27 (0.93x), win 6.85 (1.47x) | 0/1 held 0, max 17.06 (1.55x), win 4.70 (1.01x) | 0/1 held 0, max 17.42 (1.58x), win 4.76 (1.02x) |
| G6 push 80 N | 0/1 held 0, max 11.03, win 7.37 | 1/1 held 1, max 13.11 (1.19x), win 8.20 (1.11x) | 0/1 held 0, max 11.03 (1.00x), win 7.37 (1.00x) | 0/1 held 0, max 10.27 (0.93x), win 8.27 (1.12x) | 0/1 held 0, max 17.06 (1.55x), win 4.52 (0.61x) | 0/1 held 0, max 17.42 (1.58x), win 4.75 (0.64x) |

Hands over 2x the baseline on any gate: 3-digit: g6_20, 5-digit: none, 6a-digit: none, 6b-digit: g3s, 7-digit: g3mx, g3s
