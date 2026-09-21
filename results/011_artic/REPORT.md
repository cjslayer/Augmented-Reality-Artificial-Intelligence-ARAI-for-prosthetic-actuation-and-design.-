# Articulated-hand feasibility spike (branch `articulated-hand`, 2026-09-21)

Scripted gates on the PhysX articulation (`Assets/Scripts/ArticulatedHand.cs`, `ArmGraspAgent.cs`), driven by
`Assets/Scripts/Diagnostics/ArticulatedGates.cs` (config `Temp/gates.json`), reference morphology unless stated, object =
convex flat cylinder r 0.0647 m, mu 1.0, kinematic pedestal, fixed timestep 10 ms, decision period 10 (0.1 s), solver
16 / 4 iterations. All CSVs in this folder are the raw per-trial rows.

## Drive calibration (`calibrate.csv`)
Index base step to -30 deg, reference theta (omega 25 rad/s, zeta 0.70, k = I omega^2 = 6.09 N m/rad, b = 0.34, I_nominal
9.7e-3 kg m^2, link mass 0.160 kg): rise 10-90 % = 100 ms (theory 86 ms), overshoot 4.9 % (theory 4.6 %), steady angle
-31.5 deg (1.5 deg gravity sag), flexion moves the fingertip toward the palm normal. Force-mode drives with the
morphology's (k, b) reproduce the run-010 impedance semantics on real physics; the acceleration-mode variant did not
(rise 130 ms, overshoot 28 %, distal joints sagging to gravity because PhysX scales those gains by the link's own inertia).

## Gates
| gate | result | numbers |
|---|---|---|
| G1 squeeze-and-lift, 50-decision hold, no perturbation (`g1_lift.csv`) | 0.2 kg 3/3, 0.6 kg 3/3, **1.5 kg 0/3** | lifts at 180-187 steps after release, bottom 0.10-0.13 m above the pedestal, 2-4 finger contacts + palm through the hold; 1.5 kg (14.7 N) slips before the lift completes |
| G2 perturbation hold at 0.6 kg, 3 seeds per scale (`g2_perturb.csv`) | scale 0.25 3/3, 0.50 1/3, 0.75-1.50 0/3 | peak pulse 2.2 N at 0.25 (0.37 mg), 4.4 N at 0.5; every failure is a drop during the first pulse; **max scale fully survived 0.25**, target was 1.0 (1.5 mg) |
| G3 two-finger + thumb pinch (`g3_pinch.csv`) | fails 8/8 | 0.6 kg: slips at release before any lift (2 seeds x 2 scales); 0.2 kg: lifts at 186-190 steps then drops before the hold window; also fails at 0.2 kg |
| G4 theta sweep, 20 draws x 2000 steps random targets (`g4_stability.csv`) | pass 20/20 | no NaN, max joint speed 981-3028 deg/s (cap 50 rad/s = 2865), max penetration into the kinematic object 0-5.4 mm, omega 20.6-30.6 mean, zeta 0.59-0.76, 8-14 active groups |
| G5 throughput | ratio 1.15 (build), 0.90 (Editor) | 8-env headless, 20k decisions, same PPO config, same machine, sequential: articulated 9.0 s / 5k = **554 decisions/s**, kinematic run-010 player 10.4 s / 5k = 481 decisions/s. Editor random actions: articulated 4877 physics steps/s at 10 ms = 488 decisions/s, kinematic 2706 at 20 ms = 541 decisions/s. Zero orphans after both probes. |
| G6 penetration / push-through (`g6_push.csv`, `g6_push150.csv`) | 9.8 mm; escapes outward at 60 N | drives commanded 1.3x the closing targets at the force limits: max penetration 9.6-9.8 mm, 4-5 contacts, drive torque sum 0.9 N m; 60 N and 150 N toward the palm for 2 s: held, penetration unchanged; 60 N away from the palm: the object is pushed out through the fingers and falls (the run-002 exploit class exists above the ~40 N the finger force limits can oppose) |

Gate verdict: G1 fails at 1.5 kg, G2 survives only a quarter of the target perturbation, G3 / G4 pass, G5 well above the
25 % stop threshold, G6 quantified. The reference hand (force limits 4 / 2.5 / 1.2 N m base / middle / end, thumb 6 / 3)
holds up to about 6 N of object weight under a 0.4 mg lateral pulse; heavier objects and stronger pulses need higher force
limits or stiffer theta, both morphology parameters.

## Harness notes
Scripted placement puts the object on the palm surface 0.20 m from the palm pivot along the finger direction (the base
segments wrap its outer side, the tips come around to the wrist side); placing it over the palm centre gives a fingertip
hook that ejects the object at release. The placement radius must be the upright object's, not its AABB (a tumbled
object's AABB is its tilted extent: this bug produced the run-order-dependent failures seen before the fix). Arm commands
go through the action channel because the agent writes every drive velocity from its actions each step.
