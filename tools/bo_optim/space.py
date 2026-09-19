"""Search space for the morphology optimizer: the bo_eval theta (53 continuous dims + 14-bit mask).

Continuous dims, normalized to [0, 1] against the bounds below (taken from tools/bo_eval/theta.py = MorphologyManager.Sample):
  0-4    lengthScale[5]        [0.8, 1.2]
  5-20   omega[16]             fingers [12, 40] rad/s, wrist (dims 19, 20) [8, 25]
  21-36  zeta[16]              fingers [0.4, 0.9], wrist [0.5, 0.9]
  37-52  inertiaScale[16]      [0.5, 2.0]
Mask: 14 bits, constrained to >= 6 active and at least one thumb group (12, 13) active.
"""
import hashlib
import json
import random

from tools.bo_eval.theta import (FINGER_GROUPS, GROUPS, THUMB_GROUPS, LENGTH_RANGE, FINGER_OMEGA_RANGE, FINGER_ZETA_RANGE,
                                 WRIST_OMEGA_RANGE, WRIST_ZETA_RANGE, INERTIA_SCALE_RANGE, MASK_ACTIVE_PROBABILITY,
                                 MIN_ACTIVE_GROUPS, random_theta, reference_theta, validate)

N_CONT = 5 + 3 * GROUPS   # 53
LOWER = [LENGTH_RANGE[0]] * 5 + [FINGER_OMEGA_RANGE[0]] * FINGER_GROUPS + [WRIST_OMEGA_RANGE[0]] * 2 \
    + [FINGER_ZETA_RANGE[0]] * FINGER_GROUPS + [WRIST_ZETA_RANGE[0]] * 2 + [INERTIA_SCALE_RANGE[0]] * GROUPS
UPPER = [LENGTH_RANGE[1]] * 5 + [FINGER_OMEGA_RANGE[1]] * FINGER_GROUPS + [WRIST_OMEGA_RANGE[1]] * 2 \
    + [FINGER_ZETA_RANGE[1]] * FINGER_GROUPS + [WRIST_ZETA_RANGE[1]] * 2 + [INERTIA_SCALE_RANGE[1]] * GROUPS
# parameter blocks (one GP lengthscale each)
GROUP_SLICES = {"lengthScale": slice(0, 5), "omega": slice(5, 21), "zeta": slice(21, 37), "inertiaScale": slice(37, 53)}
assert len(LOWER) == len(UPPER) == N_CONT


def to_vector(theta):
    """theta dict -> (x in [0,1]^53, mask list of 14 ints)."""
    t = validate(theta)
    raw = list(t["lengthScale"]) + list(t["omega"]) + list(t["zeta"]) + list(t["inertiaScale"])
    x = [(v - lo) / (hi - lo) for v, lo, hi in zip(raw, LOWER, UPPER)]
    return x, list(t["mask"])


def from_vector(x, mask):
    raw = [lo + min(max(xi, 0.0), 1.0) * (hi - lo) for xi, lo, hi in zip(x, LOWER, UPPER)]
    return validate({"lengthScale": raw[0:5], "omega": raw[5:21], "zeta": raw[21:37], "inertiaScale": raw[37:53], "mask": [int(m) for m in mask]})


def mask_ok(mask):
    return sum(mask) >= MIN_ACTIVE_GROUPS and any(mask[g] for g in THUMB_GROUPS)


def sample_mask(rng):
    """Constrained draw (same rule as MorphologyManager.Sample / bo_eval.random_theta)."""
    for _ in range(100):
        m = [1 if rng.random() < MASK_ACTIVE_PROBABILITY else 0 for _ in range(FINGER_GROUPS)]
        if mask_ok(m):
            return m
    return [1] * FINGER_GROUPS


def random_candidate(rng):
    """Uniform in the continuous bounds, constrained mask (bo_eval.random_theta)."""
    return random_theta(rng)


def perturb(theta, rng, sigma=0.05, flip=0.1):
    """Local draw around theta: Gaussian step of `sigma` (normalized units) on every continuous dim, each mask bit flipped
    with probability `flip`; the mask is kept if the flipped one violates the constraint."""
    x, m = to_vector(theta)
    x2 = [min(max(xi + rng.gauss(0.0, sigma), 0.0), 1.0) for xi in x]
    m2 = [1 - b if rng.random() < flip else b for b in m]
    if not mask_ok(m2):
        m2 = m
    return from_vector(x2, m2)


def theta_key(theta):
    return hashlib.sha1(json.dumps(validate(theta), sort_keys=True).encode()).hexdigest()[:12]


def describe(theta):
    """Short human-readable summary: mean/min/max scale, active groups, mask, finger omega/zeta means."""
    t = validate(theta)
    ls = t["lengthScale"]; w = t["omega"][:FINGER_GROUPS]; z = t["zeta"][:FINGER_GROUPS]; I = t["inertiaScale"][:FINGER_GROUPS]
    act = [i for i in range(FINGER_GROUPS) if t["mask"][i]]
    wa = [w[i] for i in act]; za = [z[i] for i in act]; Ia = [I[i] for i in act]
    return {
        "lenMean": sum(ls) / 5, "lenMin": min(ls), "lenMax": max(ls), "lenThumb": ls[4], "lenIndex": ls[0],
        "active": len(act), "mask": "".join(str(b) for b in t["mask"]), "thumbGroups": int(t["mask"][12]) + int(t["mask"][13]),
        "omegaActiveMean": sum(wa) / len(wa), "omegaActiveMin": min(wa), "zetaActiveMean": sum(za) / len(za),
        "inertiaActiveMean": sum(Ia) / len(Ia), "omegaWrist": (t["omega"][14] + t["omega"][15]) / 2, "zetaWrist": (t["zeta"][14] + t["zeta"][15]) / 2,
    }


__all__ = ["N_CONT", "LOWER", "UPPER", "GROUP_SLICES", "to_vector", "from_vector", "mask_ok", "sample_mask", "random_candidate",
           "perturb", "theta_key", "describe", "reference_theta", "random_theta", "validate", "random"]
