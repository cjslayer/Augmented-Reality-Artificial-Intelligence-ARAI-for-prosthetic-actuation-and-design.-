# Run 014, attempt 1: from scratch on the final mix (stopped at 9.54M, 2026-10-05)

## Config

`run_014.yaml` (copied here; also at `Config/run_014.yaml`), run id 014_run, player Assembly-CSharp `d558617ea7a174ea`, 8 envs, slot layout.

- PPO as runs 012 / 013. 24M max_steps, one linear LR schedule, checkpoints every 1M.
- Fixed mixture from step 0, no lessons: pose 50/50 canonical / random stable, perturbation 1.0, hold K = 50, cylinder 10 % (trainUniform 1–64),
  lift budget 300, digit shares 0.10 / 0.15 / 0.45 / 0.075 / 0.075 / 0.15, morph/randomize 1.
- Launched 2026-10-04 20:04. Paused by machine sleep at 4.99M (9.5 h). The PC crashed at 6.21M; resumed from Prosthetic-5999949 at 09:45.
  Stopped by CJ at 9.54M (2026-10-05).

Contents: `014_run/` (checkpoints 1M–9M, TensorBoard, `checkpoint_crash_backup_5999949.pt`), `014_run_episodes/` (per-player episode logs),
`logs/` (trainer log: `run_seg1.*` before the crash, `run_seg2.*` after; `run.*` is the original stream).

## Lift rate, 0.5M to 8M (TensorBoard `Task/Lifted`, mean of the summaries in the 500k before each mark; all compositions)

| step | 0.5M | 1M | 2M | 4M | 8M |
|---|---|---|---|---|---|
| lift | 0.097 | 0.056 | 0.012 | 0.007 | 0.004 |
| gate | 0.567 | 0.860 | 0.945 | 0.960 | 0.978 |
| success | 0.000 | 0.000 | 0.000 | 0.000 | 0.000 |

## Per composition at 8M (episode logs, last quarter of the post-resume players' steps, about 7.5M–8M; sampled head)

| composition | episodes | gate | lift | hold entered | success |
|---|---|---|---|---|---|
| 3 | 1053 | 0.869 | 0.011 | 0.007 | 0.000 |
| 4 | 1486 | 0.989 | 0.005 | 0.003 | 0.000 |
| 5 | 4657 | 0.994 | 0.003 | 0.002 | 0.000 |
| 6a | 777 | 0.978 | 0.005 | 0.001 | 0.000 |
| 6b | 768 | 0.996 | 0.004 | 0.004 | 0.000 |
| 7 | 1552 | 0.997 | 0.003 | 0.002 | 0.000 |

## End reasons at 8M

| composition | drop | liftBudget | maxStep |
|---|---|---|---|
| 3 | 0.577 | 0.287 | 0.136 |
| 4 | 0.608 | 0.380 | 0.012 |
| 5 | 0.577 | 0.417 | 0.007 |
| 6a | 0.564 | 0.414 | 0.022 |
| 6b | 0.599 | 0.397 | 0.004 |
| 7 | 0.568 | 0.428 | 0.004 |

All compositions (TensorBoard): drop 0.579, liftBudget 0.398, maxStep 0.023, taskBudget 0.000.

## Conclusion

Training from scratch on the final mix converges to approach-and-touch. The policy reaches the object and fires the contact gate on 87–100 % of
episodes but stops lifting: the lift rate fell from 0.097 at 0.5M to 0.004 at 8M, and success is 0 on every composition. The return is mostly
approach shaping (0.83 per episode at 8M), against phase 0.10 and drop −0.20.

This is not a topology effect: every composition shows it. Runs 012 and 013, which started on easier settings, had success 0.30 / 0.23 at 2M
(012 used a hold ladder from K = 5; 013 used canonical poses only, no perturbation and a ladder from K = 5). The 500k smoke ran on those easier
settings, so it did not test this start. From now on a smoke runs on the exact config it precedes.
