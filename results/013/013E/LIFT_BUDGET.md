# Run 013E, 1a: lift-budget check

Episodes on random stable rest poses (held-out shapes 65-80) that met the contact gate (transitionStep >= 0) and never lifted
(stepsToLift < 0), by end reason.

| pass | gated, never lifted | ended by liftBudget | ended by drop | other | liftBudget share |
|---|---|---|---|---|---|
| 013C p0 | 109 | 89 | 20 | 0 | 0.817 |
| 013C p1 | 94 | 68 | 26 | 0 | 0.723 |
| 013D p0 | 109 | 76 | 33 | 0 | 0.697 |
| 013D p1 | 93 | 70 | 23 | 0 | 0.753 |
| 013C pulse (perturbation 0) | 108 | 92 | 16 | 0 | 0.852 |
| 013D pulse (perturbation 0) | 103 | 74 | 29 | 0 | 0.718 |
| **all** | **616** | **469** | **147** | 0 | **0.761** |

Rule: >= 50 % ended by the lift-budget timeout -> raise the lift budget 200 -> 300 steps. Share 76.1%: **raise to 300**.

Budget derivation (ArmGraspAgent.cs, OnEpisodeBegin): TaskBudgetSteps = liftBudget + HoldStepsNeeded() + taskBudgetMarginSteps
= 200 + 500 + 100 = 800 steps at K = 50 (hold 50 decisions x DecisionPeriod 10); with 300: 300 + 500 + 100 = 900.
The lift check (`sinceTransition >= liftBudget` without a lift -> FailEpisode("liftBudget")) now reads the per-episode value m_LiftBudgetEp,
set from the environment parameter task/liftBudgetSteps (absent = the serialized field, 200; the Editor harness sets the field to 200, so
every eval pass, including the 013E ones, keeps the 200-step lift budget and the 800-step task budget of Part 3). No reward, observation or
action code changed.
