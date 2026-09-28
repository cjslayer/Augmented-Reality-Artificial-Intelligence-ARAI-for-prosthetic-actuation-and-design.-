"""Run 013 phase-D interference flag (brief amendment): report (do not stop) when Success/Lying stays below 0.25 over 1M consecutive steps while
Success/Canonical rises over the same span. Uses 100k-step window means of the logged stats from the phase-D resume on. Read-only; one line."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
import tools.eval.train_run013 as T
S = T.scalars(); last = max(S["Environment/Cumulative Reward"]); d0 = [t["resume_step"] for t in T.transitions() if t["to"] == "D"][0]
m = lambda tag, a, b: (lambda v: sum(v) / len(v) if v else float("nan"))([x[0] for s, x in S.get(tag, {}).items() if a < s <= b])
if last - d0 < 1000000: print(f"INTERFERENCE n/a: {last - d0} steps into phase D (< 1M)"); sys.exit()
lying = [m("Success/Lying", s - 100000, s) for s in range(last - 900000, last + 1, 100000)]
canon0, canon1 = m("Success/Canonical", last - 1000000, last - 900000), m("Success/Canonical", last - 100000, last)
flag = all(v < 0.25 for v in lying) and canon1 > canon0
print(f"INTERFERENCE {'FLAG' if flag else 'none'}: step {last}, Success/Lying 100k means over the last 1M min {min(lying):.3f} max {max(lying):.3f}; Success/Canonical {canon0:.3f} -> {canon1:.3f}")
