# Eval-harness reproducibility (2026-09-23)

Model `results/012/Prosthetic.onnx` (9bda654f...), Editor harness `ArticulatedGates` mode `eval`, seeds 6001-6100,
mu 1.0, K = 50, perturbation 1.0, timeScale 20 unless stated. Every "pass" is its own Play session (domain reload).
Files: `repro_*` (before the inference seed), `t_*` (10 seeds), `repro2_*` (100 seeds, after the inference seed),
`tail_det_*` (seeds 6091-6100 alone), `jw1_det_*` (job system at one worker), `*_runs.log`. Inventory of the
randomness sources and how each is now seeded: `RNG_INVENTORY.md`.

## What was changed (Diagnostics only, `Assets/Scripts/Diagnostics/ArticulatedGates.cs`)

1. Per-episode seed `d = DerivedSeed(seed, mu, passIndex)` (FNV-1a/murmur mix of seed, round(mu x 1000), passIndex);
   the hook at the top of `ArmGraspAgent.OnEpisodeBegin` calls `UnityEngine.Random.InitState(d)` (theta, mass, spawn,
   pulse schedule) and `Unity.InferenceEngine.Random.SetSeed(d)` (the policy's sampled head: the ONNX `RandomNormalLike`
   has no seed attribute and draws from the package's static stream, which no worker re-creation resets).
2. `headMode` config: `"sampled"` (training-time stochastic head) or `"deterministic"` (`BehaviorParameters.DeterministicInference`
   set before `SetModel`, selecting the ONNX `deterministic_continuous_actions` output). Column `head_mode` appended to the CSV.
3. Theta columns are snapshotted in the hook (`SnapshotTheta`) before the next episode's draws: at HEAD b71fa17 every row
   logged the NEXT episode's theta (off by one; the last row logged the extra discarded episode).
4. `jobWorkers` config: sets `JobsUtility.JobWorkerCount` for the Play session (restored in Finish / OnDestroy).

## Results

| test | head | inference seed | job workers | seeds | passes | identical rows | note |
|---|---|---|---|---|---|---|---|
| repro (19:01/19:02) | deterministic | - | default | 6001-6100 | 2 | 0 / 100 | pass A (first Play after the recompile) differs from row 1 in steps/stepsToLift/contacts; pass B equals every later good pass |
| repro (19:03/19:04) | sampled | none | default | 6001-6100 | 2 | 6 / 100 | old pattern: identical prefix, drift from seed 6007 |
| t (19:12-19:16) | deterministic | - | default | 6001-6010 | 2 x timeScale 20, 2 x timeScale 1 | 10 / 10 each; all four passes identical | frame timing is not an input |
| t (19:17) | sampled | per episode | default | 6001-6010 | 2 | 10 / 10 | |
| repro2 (19:19-19:23) | sampled | per episode | default | 6001-6100 | 2 | **100 / 100, byte-identical** | success 0.96 / 0.96 |
| repro2 (19:19-19:21) | deterministic | - | default | 6001-6100 | 2 | 98 / 100 | pass A diverges at seed 6099 (gripTorqueMean 1.143 vs 1.172, retShaping 0.8575 vs 0.8567; steps equal), seed 6100 then differs in contacts 7 vs 6, maxPen 2.89 vs 3.20; pass B is identical to the 19:02 pass (100 / 100) |
| tail (19:22) | deterministic | - | default | 6091-6100 alone | 2 | 10 / 10 | identical to each other, but 0 / 10 equal to the same seeds inside the 100-seed pass: an episode depends on the episodes run before it in the same pass |
| jw1 (19:24-19:27) | deterministic | - | **1** | 6001-6100 | 3 | **100 / 100 / 100, byte-identical**; also 100 / 100 equal to the good multithreaded pass | success 0.99 |

Theta columns: 100 / 100 identical between passes in every test after the snapshot fix (1e).

## Verdicts

- **Sampled head, reseeded per episode: IDENTICAL** across two Play sessions (100 / 100 rows, byte-identical). The
  inference package's `Random.SetSeed` is the one supported lever; re-creating the worker or the ML-Agents inference seed
  cannot do it (see RNG_INVENTORY.md B).
- **Deterministic head: IDENTICAL with the job system at one worker** (three sessions byte-identical). With the default
  worker count it is reproducible except for sporadic divergence events in contact resolution (two events in about 500
  episodes: row 1 of the 19:01 pass, row 99 of the 19:19 pass), after which every later episode of that pass differs.
  The divergence columns (gripTorqueMean, maxPenMm, contacts, retShaping) name PhysX contact resolution; inference runs
  on the CPU Burst backend and is bit-stable; frame timing was excluded (timeScale 1 vs 20 identical). Unity's PhysX
  simulation tasks run on the job system and `m_EnableEnhancedDeterminism` is 0 (a ProjectSettings value, not touched),
  so thread scheduling is the remaining input; restricting the job system to one worker removes it. Speed cost: none
  measurable for this one-hand scene (65 s per 100 episodes either way).
- **History dependence** (not a nondeterminism): the same seed gives a different episode when the preceding episodes
  differ (`tail_det` vs the full pass), because the PhysX scene persists across the per-episode rig teardown/rebuild
  (and any agent state the reset does not clear). The reproducible unit is therefore a pass = (build, seed sequence,
  mu, passIndex, headMode, jobWorkers), not an isolated (build, seed) episode. bo_eval-style single-episode objectives
  must run each theta as its own pass from a fresh Play session, or accept this.
- The `DerivedSeed` mixes mu into the seed, so mu passes are no longer theta-paired (a design choice of this brief).

Evaluation setting adopted for the n = 300 measurement: deterministic head, `jobWorkers: 1`, `passIndex: 0`, fresh
block 7001-7300; sampled head at mu 1.0 on the same block for the gap.
