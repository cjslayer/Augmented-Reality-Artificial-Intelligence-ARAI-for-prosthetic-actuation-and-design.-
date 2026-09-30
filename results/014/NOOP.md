# Run 014 no-op proof: the new code with the default (legacy) layout vs the recorded 013E passes (2026-09-30)

Recorded passes: `results/013/eval/013E/` (2026-09-29): 013E model, deterministic head, one job worker, per-episode reseed, 300 episodes each.
Tool: `tools/eval/noop_014.py`; gates: `tools/eval/gates_014.py` + `tools/eval/gates_compare.py` against `results/014/gates_baseline` (unmodified HEAD, same day).

## Unmodified HEAD (the instrument)

Both recorded passes (anchor, held-out random stable) reproduced byte-identically on HEAD in a fresh Editor session (`noop_head/`).

## New code, legacy layout: four runs over two Editor sessions

| run (session) | anchor 7001-7300 | held-out random stable p0 | held-out canonical p0 |
|---|---|---|---|
| noop_core (A, after a recompile) | **identical** | 0.553 vs 0.560, −0.007 [−0.060, +0.046], diverges at episode 8 | - |
| noop_core_r2 (A) | **identical** | same as the run above (hash 8becc01d) | - |
| noop_core_fresh (B, fresh launch) | **identical** | 0.553 vs 0.560, −0.007 [−0.057, +0.044], diverges at episode 28 (hash 88f11f74) | **identical** |
| noop_final (B, final Part 2 code) | 0.883 vs 0.883, +0.000 [−0.013, +0.013], diverges at episode 9 | **identical** | 0.750 vs 0.730, +0.020 [−0.004, +0.044], diverges at episode 108 |

In every diverging run, all per-episode draws (θ, object, pose, mass, spawn, yaw) are identical to the recorded pass, and the divergence begins inside an
episode, after the object is released. **Every recorded pass has been reproduced byte for byte by the new code at least once**, and which pass fails
to reproduce changes between runs of the same code. A change in the legacy computation would change a pass on every run. The divergences are the
instrument's timing channel (known since run 013: `results/013/REPORT.md` section 8). The likely mechanism is that the legacy fingertip ray sensors
read armature bones that follow the links only in LateUpdate, i.e. once per rendered frame while several physics steps run per frame at timeScale 20,
so the ray observations depend on frame timing. The mechanism is not proven here; the slot layout reads its folded rays from the current link pose
instead.

Paired success differences on the non-identical runs are 0.000, −0.007 and +0.020, every 95 % CI includes zero: **pass-level agreement**.

## Scripted gates (policy and rays not involved), legacy layout

| file | new code vs HEAD baseline (twice: gates_newcode_legacy, gates_final_legacy) |
|---|---|
| g1x, g2, g2x, g3m, g3mx, g3s, g3x, g4 (19 rows), g6 at 10 / 20 / 40 / 80 N | **byte-identical** (every numeric column; note column without wall-clock fields) |
| g1 | differs by about 1 % (e.g. max penetration 1.56 vs 1.57 mm). Not a code effect: two further G1 runs on the same new code differ from each other and from the first (`gates_newcode_legacy_g1a`, `_g1b`) |

The G4 file has 19 of its 20 rows in the HEAD baseline and in both new-code runs (the trial-3 row is not written; a pre-existing harness row-write
issue, not investigated).
