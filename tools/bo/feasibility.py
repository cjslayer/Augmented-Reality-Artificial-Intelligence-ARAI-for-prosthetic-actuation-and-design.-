"""Feasibility of a theta for the articulated hand: the Part A definition restated, and its implementation here.

Part A (tools/bo_eval, commit 79dd2eb) defined feasibility by a forced-close oracle in the BoEval player, quoted from
tools/bo_eval/README.md (How it runs, step 2):
    "optionally runs the forced-close oracle (MorphVerify close mode: object teleported to `GraspPoint`, backed off along
     the palm normal, staged wrap for 300 steps under HeuristicOnly; `feasible` = hold criterion met);"
and from Assets/Scripts/BoEval/BoEvalHarness.cs:11-12 / 313-318: staged wrap = base groups closed from step 1, middle
groups from step 61, end groups from step 121, thumb closed throughout (heuristic actions -1 / +1), `gate = agent.LastHoldCriterionMet`,
`gateMet` once the gate is true at any of the `oracleSteps` (300) steps. The oracle ran on the kinematic run-010 hand with
an omega-based theta (k = I omega^2, tools/bo_eval/README.md:17). The criterion itself (the agent's hold criterion:
LastHoldCriterionMet, ArmGraspAgent.cs:219) does not depend on omega, so no translation of the definition is needed; only
the theta space changes: k is native here and bounded by MorphologyManager's class ranges (tools/bo/theta_space.py) instead
of the omega ranges 12-40 rad/s (fingers) / 8-25 rad/s (wrist) of tools/bo_eval/theta.py:26-28.

Implementation against the current theta space, two layers:
  1. analytic (no Editor): the bounds of theta_space and the mask constraint MorphologyManager.Sample enforces
     (>= 6 active finger groups including a thumb group, MorphologyManager.cs:67-69, :205-211): `feasible_analytic`.
  2. oracle (Editor harness, mode "lift" with thetaFixed): the scripted enveloping close of ArticulatedGates (contact
     controller, envelope grip, object placed on the palm, mass 0.6 kg, settleSteps 300); `gateStep >= 0` in the trial row
     means the agent's hold criterion (`agent.LastHoldCriterionMet`, ArticulatedGates.cs:275) was met during the close:
     the same criterion as Part A's `gateMet`, on the articulated hand. `feasible_oracle` returns (bool, record).
Finding (2026-09-24, results/bo/partB/smoke/oracle_*.csv): on the current articulated hand the scripted close reaches only
4-5 contacts for the reference hand and for random training draws (gateStep -1 with the default placement, with the G1
placement placeUp 0.08 / liftExtra 0.05, and with settleSteps 600 or the contact controller off), while the policy grasps
the reference hand on 100 / 100 episodes. The scripted oracle therefore rejects hands the policy handles and is NOT used as
the campaign's feasibility; the campaign uses layer 1 (`feasible(theta, oracle=False)`), and the oracle stays available.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_space
from tools.bo.gates_runner import run_pass, load_rows

ORACLE_SETTLE_STEPS, ORACLE_MASS = 300, 0.6


def feasible_analytic(theta):
    return theta_space.in_bounds(theta) and theta_space.mask_ok(theta["mask"])


def oracle_config(theta, csv):
    cfg = {"mode": "lift", "scales": [0], "grips": ["envelope"], "seeds": 1, "masses": [ORACLE_MASS], "settleSteps": ORACLE_SETTLE_STEPS, "liftBudget": 400, "holdSteps": 100,
           "holdDecisions": 50, "liftSpeedDeg": 20, "timeScale": 20, "maxTrialSteps": 1500, "csv": csv}
    cfg.update(theta_space.harness_fields(theta)); return cfg


def feasible_oracle(theta, csv, max_wait=600):
    """one scripted-close trial at theta; feasible = the contact gate fired (gateStep >= 0)"""
    path, wall = run_pass(oracle_config(theta, csv), max_wait=max_wait); rows = load_rows(path)
    if not rows: return False, {"error": "no trial row", "csv": csv, "wall_s": wall}
    r = rows[-1]; gate = int(r["gateStep"]); rec = {"gateStep": gate, "transitionStep": int(r["transitionStep"]), "contactsEnd": int(r["contactsEnd"]), "success": r["success"], "endReason": r["endReason"], "csv": csv, "wall_s": wall}
    return gate >= 0, rec


def feasible(theta, csv=None, oracle=True):
    if not feasible_analytic(theta): return False, {"analytic": False}
    if not oracle or csv is None: return True, {"analytic": True, "oracle": None}
    ok, rec = feasible_oracle(theta, csv); rec["analytic"] = True; return ok, rec
