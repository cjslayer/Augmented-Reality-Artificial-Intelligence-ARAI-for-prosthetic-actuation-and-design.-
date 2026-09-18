"""Morphology vector theta for the BO evaluation endpoint.

theta is a dict with exactly these keys (the natural parameterization of MorphologyManager.Sample):
  lengthScale  : 5 floats, per-finger link-length scale (index, middle, ring, pinky, thumb); training range 0.8-1.2
  omega        : 16 floats, natural frequency (rad/s) per impedance group (14 finger groups + wristFlex, wristPron);
                 training range 12-40 (fingers), 8-25 (wrist)
  zeta         : 16 floats, damping ratio per group; training range 0.4-0.9 (fingers), 0.5-0.9 (wrist)
  inertiaScale : 16 floats, multiplier on the geometric nominal inertia at these link scales; training range 0.5-2.0
  mask         : 14 ints, 1 = finger group actuated, 0 = held at the neutral pose (training: Bernoulli(0.8) with
                 >= 6 active and at least one thumb group)
The harness converts (omega, zeta, inertiaScale) to (k, b, I) = (I w^2, 2 zeta I w, nominalI * inertiaScale).
"""
import random

FINGER_NAMES = ("index", "middle", "ring", "pinky", "thumb")
GROUP_NAMES = ("indexBase", "indexMiddle", "indexEnd", "middleBase", "middleMiddle", "middleEnd", "ringBase", "ringMiddle",
               "ringEnd", "pinkyBase", "pinkyMiddle", "pinkyEnd", "thumbBase", "thumbEnd", "wristFlex", "wristPron")
FINGER_GROUPS = 14
GROUPS = 16
THUMB_GROUPS = (12, 13)
KEYS = ("lengthScale", "omega", "zeta", "inertiaScale", "mask")
LENGTHS = {"lengthScale": 5, "omega": GROUPS, "zeta": GROUPS, "inertiaScale": GROUPS, "mask": FINGER_GROUPS}

# training distribution (MorphologyManager serialized defaults)
LENGTH_RANGE = (0.8, 1.2)
FINGER_OMEGA_RANGE = (12.0, 40.0)
FINGER_ZETA_RANGE = (0.4, 0.9)
WRIST_OMEGA_RANGE = (8.0, 25.0)
WRIST_ZETA_RANGE = (0.5, 0.9)
INERTIA_SCALE_RANGE = (0.5, 2.0)
MASK_ACTIVE_PROBABILITY = 0.8
MIN_ACTIVE_GROUPS = 6


def reference_theta():
    """The reference hand: scales 1.0, fingers omega 25 / wrist 15 rad/s, zeta 0.7, nominal inertia, full mask."""
    return {
        "lengthScale": [1.0] * 5,
        "omega": [25.0] * FINGER_GROUPS + [15.0, 15.0],
        "zeta": [0.7] * GROUPS,
        "inertiaScale": [1.0] * GROUPS,
        "mask": [1] * FINGER_GROUPS,
    }


def random_theta(rng=None):
    """One draw from the training distribution (same rule as MorphologyManager.Sample, including the mask constraint)."""
    rng = rng if rng is not None else random.Random()
    u = rng.uniform
    theta = {
        "lengthScale": [u(*LENGTH_RANGE) for _ in range(5)],
        "omega": [u(*FINGER_OMEGA_RANGE) for _ in range(FINGER_GROUPS)] + [u(*WRIST_OMEGA_RANGE) for _ in range(2)],
        "zeta": [u(*FINGER_ZETA_RANGE) for _ in range(FINGER_GROUPS)] + [u(*WRIST_ZETA_RANGE) for _ in range(2)],
        "inertiaScale": [u(*INERTIA_SCALE_RANGE) for _ in range(GROUPS)],
        "mask": [1] * FINGER_GROUPS,
    }
    for _ in range(100):
        mask = [1 if rng.random() < MASK_ACTIVE_PROBABILITY else 0 for _ in range(FINGER_GROUPS)]
        if sum(mask) >= MIN_ACTIVE_GROUPS and any(mask[g] for g in THUMB_GROUPS):
            theta["mask"] = mask
            break
    return theta


def validate(theta):
    """Returns a normalized copy (floats / ints, plain lists) or raises ValueError."""
    if not isinstance(theta, dict):
        raise ValueError("theta must be a dict with keys " + ", ".join(KEYS))
    extra = set(theta) - set(KEYS)
    if extra:
        raise ValueError("unknown theta keys: " + ", ".join(sorted(extra)))
    out = {}
    for k in KEYS:
        if k not in theta:
            raise ValueError("theta is missing " + k)
        v = list(theta[k])
        if len(v) != LENGTHS[k]:
            raise ValueError(f"theta[{k!r}] must have {LENGTHS[k]} entries, got {len(v)}")
        out[k] = [int(round(float(x))) for x in v] if k == "mask" else [float(x) for x in v]
    if any(x not in (0, 1) for x in out["mask"]):
        raise ValueError("mask entries must be 0 or 1")
    if any(x <= 0 for x in out["omega"]) or any(x <= 0 for x in out["inertiaScale"]) or any(x < 0 for x in out["zeta"]):
        raise ValueError("omega and inertiaScale must be > 0, zeta >= 0")
    if any(x <= 0 for x in out["lengthScale"]):
        raise ValueError("lengthScale must be > 0")
    return out


def is_reference(theta, tol=1e-6):
    ref = reference_theta()
    t = validate(theta)
    return all(abs(a - b) <= tol for k in ("lengthScale", "omega", "zeta", "inertiaScale") for a, b in zip(t[k], ref[k])) and t["mask"] == ref["mask"]
