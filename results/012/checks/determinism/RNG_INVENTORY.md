# Randomness inventory for one eval episode (Editor harness, mode `eval`) — 2026-09-23

Scope: `Assets/Scripts/ArmGraspAgent.cs`, `MorphologyManager.cs`, `ArticulatedHand.cs`, `Diagnostics/ArticulatedGates.cs`,
the ML-Agents 4.1.0 and Inference Engine 2.6.1 packages (`Library/PackageCache/com.unity.ml-agents@51fac9654958`,
`com.unity.ai.inference@9a123aee5df7`), `ProjectSettings/DynamicsManager.asset`. Line numbers are the working tree at
the time of the audit (harness after the reseeding patch). There is no `System.Random`, `Guid`, `DateTime.Now` or
`GetHashCode` in `Assets/Scripts` outside `Assets/Myo` and `MeshSampler.cs` (not in the scene); nothing wall-clock or
frame-count based feeds a decision in eval mode.

## A. Draws through `UnityEngine.Random` (one global stream), all AFTER the hook

The hook `ArmGraspAgent.BeforeEpisodeBegin` is invoked at `ArmGraspAgent.cs:367`, the first RNG-touching statement of
every episode (nothing draws before it in `OnEpisodeBegin`, `FailEpisode`, `LogEpisode` or `EndEpisode`). The harness
installs it at `ArticulatedGates.cs:239` (working tree) and it now runs, per episode:
`SnapshotTheta(); evalIndex++; d = DerivedSeed(seed, mu, passIndex); UnityEngine.Random.InitState(d); Unity.InferenceEngine.Random.SetSeed(d != 0 ? d : 1);`

`DerivedSeed(seed, mu, pass)` (`ArticulatedGates.cs:173-182`): FNV-1a/murmur-style mix, `h = 2166136261; for v in
[seed, round(mu*1000), pass]: h ^= v; h *= 16777619; h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (int)h`.
Consequence: the same nominal seed gives different theta at different mu (mu passes are no longer theta-paired) and a
different `passIndex` gives an independent replicate.

| file:line | draw | decides | when |
|---|---|---|---|
| ArmGraspAgent.cs:375 | `Random.Range(log mMin, log mMax)` | object mass, log-uniform 0.2-1.5 kg | 1st draw after the hook |
| MorphologyManager.cs:197 | `Random.Range` x5 | finger length scales 0.8-1.2 (rig rebuilt from them) | in `ApplyForEpisode` (ArmGraspAgent.cs:384) |
| MorphologyManager.cs:190-191 | `Random.Range` x16 (log-uniform) | joint stiffness k per group | same |
| MorphologyManager.cs:202 | `Random.Range` x16 | damping ratio zeta per group (b = 2 zeta sqrt(k I)) | same |
| MorphologyManager.cs:203 | `Random.Range` x16 | inertia scale per group (link masses) | same |
| MorphologyManager.cs:205-211 | `Random.value` x14 per attempt, up to 100 attempts | actuation mask (>= 6 active, thumb active; fallback all true) | same; variable draw count |
| ArmGraspAgent.cs:429 | `Random.insideUnitCircle` | spawn x/z on a 0.09 m disk | `SpawnCylinder` (393), <= 50 attempts |
| ArmGraspAgent.cs:430 | `Random.Range(0, spawnHeightRange)` | spawn height — NOT evaluated (`restOnPlatform` = 1) | - |
| ArmGraspAgent.cs:433 | `Random.Range(0, 360)` | object yaw, drawn only when the reach test passes | variable draw count |
| ArmGraspAgent.cs:702-706 | `Random.Range` x3, `Random.Range(0.5,1)` x3, `insideUnitCircle` x3, `onUnitSphere` x3 | pulse start hold-steps, magnitudes, force directions, torque axes | at EVERY Lift -> Hold entry (646; cleared at 656), i.e. trajectory-dependent draw count |
| ArticulatedGates.cs:286, 551-552 | `InitState`, `Range` | scripted modes only | unreachable in eval |

Not reachable in eval: `Diagnostics/StepRate.cs:29`, `BoEval/BoEvalHarness.cs:202` (not in the scene).

## B. The policy's sampled head (outside `UnityEngine.Random`)

`Assets/Models/Prosthetic.onnx` (and every run's export) samples inside the graph: `RandomNormalLike -> Mul -> Add` on
the `mu` output (`continuous_actions = mu + exp(log sigma) * N(0,1)`); the node has only a `dtype` attribute, no `seed`.
Inference Engine: `Runtime/Core/Layers/Layer.Random.cs:8-21` (`RandomLayer.ResetSeed`: `hasSeed ? new Random(seed) :
new Random()`), so this node draws from the package's process-wide static `Random.s_Random`
(`Runtime/Core/Random.cs:17`, constant seed `0x6E624EB7`, reset at `SubsystemRegistration` on every Play, advanced by
one `NextInt` per `Worker.Schedule`, i.e. per decision, `Worker.cs:211-262`). The per-element value is a pure function
of (that int, flat output index): `Backends/CPU/BurstCPU.Jobs.Other.cs:208-239`. ML-Agents' `seed` (`ModelRunner.cs:54-126`,
`Academy.InferenceSeed`) only feeds an `epsilon` input generator and the legacy discrete applier — inert for this model.
Re-creating the `ModelRunner`/`Worker` does NOT reset the stream (`Layer.Random.cs:19` routes an unseeded node to the
static; `Agent.SetModel` returns early for the same asset reference, `Agent.cs:664-681`; `Academy.GetOrCreateModelRunner`
keys on (model, device) only, `Academy.cs:629-640`). The single public knob is `Unity.InferenceEngine.Random.SetSeed(int)`
(`Random.cs:30-34`, 0 means the default seed) — now called in the hook with the derived seed. Inference runs on the CPU
Burst backend (`ModelRunner.cs:103-105`, `InferenceDevice.Default -> BackendType.CPU`), deterministic.
`BehaviorParameters.DeterministicInference` (`m_DeterministicInference`, scene value 0) is read once at policy creation
(`BehaviorParameters.cs:245/254 -> SentisPolicy -> ModelRunner`), selecting the `deterministic_continuous_actions`
output (`SentisModelInfo.cs:279-288`); the harness sets it before `SetModel` (`ArticulatedGates.cs:217`) — a fresh
ModelAsset import per pass guarantees a new runner. Before this patch the `headMode` field did not exist, so every
recorded eval used the sampled head.

## C. Physics inputs and history

| item | site | value / note |
|---|---|---|
| fixed timestep | `ArticulatedHand.cs:195` (Awake) | 0.01 s; `ProjectSettings/TimeManager.asset` 0.02 is dead |
| simulation mode | `DynamicsManager.asset` `m_SimulationMode 0` | FixedUpdate; no `autoSimulation` use |
| solver | `m_SolverType 1` (TGS), per-link 16/4 iterations (`ArticulatedHand.cs:367`), object 6/1 | per step |
| enhanced determinism | `m_EnableEnhancedDeterminism 0` | results not guaranteed independent of actor creation history |
| rig lifecycle | `ArticulatedHand.cs:251-369` | full DestroyImmediate + rebuild (~20 ArticulationBodies) once per episode inside `OnEpisodeBegin`; never mid-episode |
| object reset | `ArmGraspAgent.cs:378, 674-682` | kinematic on reset, released at the contact gate with zeroed velocities |
| sleep / contact offset | `ArmGraspAgent.cs:293-294`, `ArticulatedHand.cs:367` | sleepThreshold 0, contactOffset 0.0036 |
| timeScale | `ArticulatedGates.cs:162` | config (20 in evals); `maximumDeltaTime` untouched; cannot change a trajectory |
| interpolation | `ArmGraspAgent.cs:294/378/678` | None during the dynamic phase |
| contact callback order | `ArticulatedHand.cs:547-561` | affects only float-summation order of the impulse diagnostics |
| first episode after Play | `Agent.LazyInitialize -> OnEpisodeBegin` at OnEnable, before `ArticulatedGates.Start` | UNSEEDED theta/mass/spawn; discarded by the warm-up (no row) but it leaves physics history (a different rig was created and destroyed) |
| Editor statics | `EditorSettings.asset` `m_EnterPlayModeOptionsEnabled 0` | full domain reload per Play: the hook, `MorphologyManager` statics and the inference static stream all reset per Play |

## D. CSV row path

`EvalRow` (`ArticulatedGates.cs:224-232`) runs on the FixedUpdate after the agent's `EndEpisode -> OnEpisodeBegin`
(synchronous), so the next episode's hook has already fired. Outcome columns come from `agent.LastEpisode`
(captured at episode end, correct); `holdNeeded`, `mu`, `dtMs`, `head_mode` are constants; theta columns
(`lenMean, len*, kFingerMean, zetaMean, inertiaScaleMean, activeGroups, mask, handSpanRatio`) were live reads of
`MorphologyManager` at HEAD b71fa17 and therefore logged the NEXT episode's theta on every row (the last row logged the
extra discarded episode's theta). Fixed: `SnapshotTheta()` (`ArticulatedGates.cs:185-192`) captures them in the hook,
before the next episode's draws. Every eval CSV committed before this patch (`results/011_artic2/eval011b_*`,
`results/011b/analysis/eval011b_*`, `results/012/eval/*`, `results/012/checks/eval012_fresh_repeat_*`) carries theta
shifted by one episode; aggregate theta distributions are unaffected, per-episode theta-bin tables are invalid.
