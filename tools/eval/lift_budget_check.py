"""Run 013E brief 1a (read-only): of the random-stable (lying) episodes that met the contact gate and never lifted, what share ended by the
lift-budget timeout (endReason liftBudget) rather than a drop? Sources: the Part 3 held-out random-stable passes of 013C and 013D (p0, p1) and
the perturbation-0 pulse passes. Rule (brief): >= 50 % -> raise the lift budget from 200 to 300 steps. Writes results/013/013E/LIFT_BUDGET.md.
"""
import csv, os, sys
from collections import Counter
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FILES = [("013C p0", "results/013/eval/013C/heldout_randomStable_10001_10300_p0.csv"), ("013C p1", "results/013/eval/013C/heldout_randomStable_10001_10300_p1.csv"),
         ("013D p0", "results/013/eval/013D/heldout_randomStable_10001_10300_p0.csv"), ("013D p1", "results/013/eval/013D/heldout_randomStable_10001_10300_p1.csv"),
         ("013C pulse (perturbation 0)", "results/013/eval/pulse/013C_heldout_randomStable_10001_10300_p0_perturb0.csv"), ("013D pulse (perturbation 0)", "results/013/eval/pulse/013D_heldout_randomStable_10001_10300_p0_perturb0.csv")]

md = ["# Run 013E, 1a: lift-budget check", "", "Episodes on random stable rest poses (held-out shapes 65-80) that met the contact gate (transitionStep >= 0) and never lifted",
      "(stepsToLift < 0), by end reason.", "", "| pass | gated, never lifted | ended by liftBudget | ended by drop | other | liftBudget share |", "|---|---|---|---|---|---|"]
T = Counter()
for lab, f in FILES:
    rows = [r for r in csv.DictReader(open(os.path.join(REPO, f), newline="")) if (r.get("seed") or "").isdigit()]
    g = [r for r in rows if int(r["transitionStep"]) >= 0 and int(r["stepsToLift"]) < 0]; c = Counter(r["endReason"] for r in g)
    oth = len(g) - c["liftBudget"] - c["drop"]; T.update({"n": len(g), "lb": c["liftBudget"], "drop": c["drop"], "other": oth})
    md.append(f"| {lab} | {len(g)} | {c['liftBudget']} | {c['drop']} | {oth} | {c['liftBudget'] / max(1, len(g)):.3f} |")
share = T["lb"] / T["n"]
md.append(f"| **all** | **{T['n']}** | **{T['lb']}** | **{T['drop']}** | {T['other']} | **{share:.3f}** |")
md += ["", f"Rule: >= 50 % ended by the lift-budget timeout -> raise the lift budget 200 -> 300 steps. Share {share:.1%}: **{'raise to 300' if share >= 0.5 else 'keep 200'}**.", "",
       "Budget derivation (ArmGraspAgent.cs, OnEpisodeBegin): TaskBudgetSteps = liftBudget + HoldStepsNeeded() + taskBudgetMarginSteps", "= 200 + 500 + 100 = 800 steps at K = 50 (hold 50 decisions x DecisionPeriod 10); with 300: 300 + 500 + 100 = 900.",
       "The lift check (`sinceTransition >= liftBudget` without a lift -> FailEpisode(\"liftBudget\")) now reads the per-episode value m_LiftBudgetEp,",
       "set from the environment parameter task/liftBudgetSteps (absent = the serialized field, 200; the Editor harness sets the field to 200, so",
       "every eval pass, including the 013E ones, keeps the 200-step lift budget and the 800-step task budget of Part 3). No reward, observation or",
       "action code changed."]
open(os.path.join(REPO, "results/013/013E/LIFT_BUDGET.md"), "w", encoding="utf-8").write("\n".join(md) + "\n"); print("\n".join(md[5:13]))
