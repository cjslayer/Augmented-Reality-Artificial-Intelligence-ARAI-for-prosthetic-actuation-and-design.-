"""Run 013 plateau rule (brief, Part 2): after 12M, stop when phase-C success (canonicalFraction 0, perturb 1.0) has not improved by >= 0.02 over
the trailing 3M steps. Success = mean of the Success/Lying summaries over a 500k-step window ending at the step compared (every phase-C episode is a
lying / rest-pose presentation). Read-only; prints one line.

usage: python -m tools.eval.plateau_check [--step S]   (default: the latest logged step)
"""
import argparse, os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
import tools.eval.train_run013 as T


def mean500(S, end):
    v = [x[0] for st, x in S.get("Success/Lying", {}).items() if end - 500000 < st <= end]; return (sum(v) / len(v)) if v else float("nan")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--step", type=int); a = ap.parse_args()
    S = T.scalars(); last = a.step or max(S["Environment/Cumulative Reward"])
    now, then = mean500(S, last), mean500(S, last - 3000000); gain = now - then
    verdict = "n/a (< 12M)" if last < 12000000 else ("PLATEAU: stop" if gain < 0.02 else "improving: continue")
    print(f"PLATEAU step={last} success500k_now={now:.3f} success500k_3M_ago={then:.3f} gain={gain:+.3f} -> {verdict}")
