# Part C setup (Part 2 of the run-013E brief), 2026-09-29

Substrate: **013D** (results/013/013D/Prosthetic.onnx, Prosthetic-20000063, sha256 96ac32e6), because 013E failed the 1h gate on the anchor
(results/013/013E/PART1.md).

## 2a. Reduced theta (tools/bo/theta_reduced.py)

20 dimensions in the unit box. Five length scales (index, middle, ring, pinky, thumb). k, zeta and inertia scale each per class: MCP, PIP,
DIP, thumb, wrist. All 14 mask bits are on. Class of group g (MorphologyManager.cs:179-184): g < 12 gives g % 3 (MCP / PIP / DIP), g 12-13
thumb, g 14-15 wrist. Every group of a class shares one unit coordinate. k is log-scaled inside each group's own range; the thumb-base
(1-15) and thumb-end (0.3-3) groups share the quantile, and u = 0.5 gives each its reference sqrt(lo hi). The mapping goes through
theta_space.py.

| bound | value | MorphologyManager.cs |
|---|---|---|
| lengthScaleRange | 0.8-1.2 | :41 |
| k MCP / PIP / DIP | 0.5-6 / 0.3-3 / 0.1-1 N m/rad | :43 / :45 / :47 |
| k thumb base / thumb end | 1-15 / 0.3-3 | :49 / :51 |
| k wrist | 1-10 | :53 |
| log-uniform k | true | :55 |
| zeta finger / wrist | 0.3-1.0 / 0.3-1.0 | :57 / :59 |
| inertia scale | 0.5-2.0 | :63 |
| reference zeta | 0.7 | :61 |
| reference k = sqrt(lo hi) | | :186 |

Reference θ = the spec nominal hand: lengths 1, k = sqrt(lo hi), zeta 0.7, inertia 1, mask all on. In unit coordinates this is 0.5 for
lengths and k, 0.571 for zeta and 0.333 for inertia; it maps exactly to theta_space.reference_theta(). Sobol round trips and corner bounds
were checked.

## 2b. Objective J(θ) (tools/bo/objective_013.py)

J(θ) is the mean success over 128 episodes: the 32 even train shapes 2-64 × both presentations × 2 episodes.

- **Seeds.** Canonical uses 11001-11064; random stable uses 11065-11128.
- **Episode settings.** Perturbation 1.0, K = 50, mu 1.0, mass as training. Deterministic head, one job worker, per-episode reseed, passIndex 0.
- **Plays.** One evaluation is two harness plays, one per presentation, because the harness sets the pose mode per play.
- **Shape mapping.** objectStratify "train" with the Editor-only key objectIndexStride 2 (ObjectBank.cs). It gives each even shape exactly 4 episodes, checked on every J.
- **Logs.** Each J records per-mode and per-family (length bin × pose mode) success.
- **Failures.** A failed play is retried once. A second failure stops the run.

## 2c. Noise floor

| session | J | canonical | random stable | hashes (canonical / random stable) | seconds |
|---|---|---|---|---|---|
| 1, repeat 0 | 0.8594 | 0.969 | 0.750 | f8816016 / 1329567a | 71 |
| 1, repeat 1 | 0.8594 | 0.969 | 0.750 | f8816016 / 1329567a | 71 |
| 1, repeat 2 | 0.8594 | 0.969 | 0.750 | f8816016 / 1329567a | 71 |
| 2 (Editor relaunched, throwaway) | 0.8672 | 0.969 | 0.766 | f8816016 / bea5360d | 77 |

Observed variance is 1.5e-5 (SD 0.004), so **the GP noise floor is max(0.002, 1.5e-5) = 0.002**.

- **Within a session.** Repeats are byte-identical.
- **Across the relaunch.** One random-stable episode changed; this is the instrument's known timing sensitivity.
- **What this measures.** The floor covers only re-running the same θ on the same seeds. Across θ, J also carries the realization noise of
  128 Bernoulli episodes, about SD 0.03 at J ≈ 0.86. The GP fits its noise variance above the floor.

## 2d. Cost

The mean is **72 s per J(θ)**, below the 180 s limit. The campaign is 200 evaluations plus about 11 reference checks, roughly 4.2 h.

## Note on headroom

At the reference, canonical J on train shapes is 0.969, near the ceiling. Most of the objective's range across θ will come from the
random-stable half (0.75) and from hands that lose the upright grasp. In the Part 3 probes, the thumb-masked hands lost it, and here the
thumb base is always driven.
