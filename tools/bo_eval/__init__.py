"""Fixed-theta evaluation endpoint for the BO outer loop (Part A).

    from tools.bo_eval import evaluate, feasible, reference_theta, random_theta
    r = evaluate(theta, n_episodes=20, mu_levels=(1.0,), seed_block=(4001, 4020))

See evaluate.py for the return dict and README.md for the workflow.
"""
from .evaluate import evaluate, feasible, format_summary, summarize, RESERVED_SEED_BLOCK, SPENT_SEED_BLOCKS
from .theta import reference_theta, random_theta, validate, is_reference, KEYS, GROUP_NAMES, FINGER_NAMES

__all__ = ["evaluate", "feasible", "format_summary", "summarize", "reference_theta", "random_theta", "validate", "is_reference",
           "KEYS", "GROUP_NAMES", "FINGER_NAMES", "RESERVED_SEED_BLOCK", "SPENT_SEED_BLOCKS"]
