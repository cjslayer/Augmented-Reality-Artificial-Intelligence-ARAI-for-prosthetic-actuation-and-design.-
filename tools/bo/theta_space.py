"""theta space of the articulated hand for BO Part B: the training randomization of MorphologyManager, in k (N m/rad).

Every bound below is read from Assets/Scripts/MorphologyManager.cs (working tree at commit d427759; serialized defaults
of the scene component are the field initializers):
  lengthScaleRange = (0.8, 1.2)                     MorphologyManager.cs:41   (uniform, one scale per finger, :197)
  stiffnessRangeBase (MCP) = (0.5, 6)               MorphologyManager.cs:43
  stiffnessRangeMiddle (PIP) = (0.3, 3)             MorphologyManager.cs:45
  stiffnessRangeEnd (DIP) = (0.1, 1)                MorphologyManager.cs:47
  stiffnessRangeThumbBase (CMC/MCP) = (1, 15)       MorphologyManager.cs:49
  stiffnessRangeThumbEnd (IP) = (0.3, 3)            MorphologyManager.cs:51
  wristStiffnessRange = (1, 10)                     MorphologyManager.cs:53
  logUniformStiffness = true                        MorphologyManager.cs:55   (draw: exp(uniform(log lo, log hi)), :188-192)
  fingerZetaRange = (0.3, 1.0), wristZetaRange = (0.3, 1.0)   MorphologyManager.cs:57, :59  (uniform, :202)
  inertiaScaleRange = (0.5, 2.0)                    MorphologyManager.cs:63   (uniform, :203; link masses x scale, ArticulatedHand.cs:314)
  maskActiveProbability = 0.8, minActiveGroups = 6, requireThumbActive = true   MorphologyManager.cs:65, :67, :69
  mask draw: 14 Bernoulli(0.8) bits, redrawn up to 100 times until >= 6 active and a thumb group active, else all true   :205-211
  class of a group: StiffnessRange(g)               MorphologyManager.cs:179-184 (g < 12: g % 3 = base / middle / end; 12 thumb base; 13 thumb end; 14-15 wrist)
  reference hand: lengths 1, k = sqrt(lo * hi) of the class range (:186, Initialize :143-144), zeta = referenceZeta 0.7 (:61),
  inertia scale 1, mask all true (:80-84)
Sample order in MorphologyManager.Sample (:195-212): 5 length scales, then per group (k, zeta, inertiaScale), then the mask.
Dimensions: 5 + 16 + 16 + 16 + 14 = 67 (the brief's "61" omits the two wrist axes, which training randomizes too).
"""
import math, sys, os
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo_eval.theta import GROUP_NAMES, FINGER_NAMES   # names only (Part A module; its omega ranges are not used)

FINGER_GROUPS, GROUPS, FINGERS = 14, 16, 5
THUMB_GROUPS = (12, 13)                       # k_GroupFinger = {0,0,0, 1,1,1, 2,2,2, 3,3,3, 4,4}, MorphologyManager.cs:36
LENGTH_RANGE = (0.8, 1.2)
K_RANGE_BASE, K_RANGE_MIDDLE, K_RANGE_END = (0.5, 6.0), (0.3, 3.0), (0.1, 1.0)
K_RANGE_THUMB_BASE, K_RANGE_THUMB_END, K_RANGE_WRIST = (1.0, 15.0), (0.3, 3.0), (1.0, 10.0)
ZETA_RANGE_FINGER, ZETA_RANGE_WRIST = (0.3, 1.0), (0.3, 1.0)
INERTIA_RANGE = (0.5, 2.0)
MASK_ACTIVE_PROBABILITY, MIN_ACTIVE_GROUPS, REQUIRE_THUMB = 0.8, 6, True
REFERENCE_ZETA = 0.7


def k_range(g):
    """StiffnessRange(g), MorphologyManager.cs:179-184"""
    if g >= FINGER_GROUPS: return K_RANGE_WRIST
    if g == 12: return K_RANGE_THUMB_BASE
    if g == 13: return K_RANGE_THUMB_END
    return (K_RANGE_BASE, K_RANGE_MIDDLE, K_RANGE_END)[g % 3]


def zeta_range(g): return ZETA_RANGE_WRIST if g >= FINGER_GROUPS else ZETA_RANGE_FINGER


# ---- vector layout: 67 dims. continuous dims are stored in the unit box [0, 1] (log-scaled for k), mask bits as 0 / 1
DIMS = ([("len", f, "lin", LENGTH_RANGE) for f in range(FINGERS)] + [("k", g, "log", k_range(g)) for g in range(GROUPS)]
        + [("zeta", g, "lin", zeta_range(g)) for g in range(GROUPS)] + [("inertia", g, "lin", INERTIA_RANGE) for g in range(GROUPS)]
        + [("mask", g, "bit", (0, 1)) for g in range(FINGER_GROUPS)])
N_DIMS = len(DIMS)
DIM_NAMES = [f"{kind}_{FINGER_NAMES[i] if kind == 'len' else GROUP_NAMES[i]}" for kind, i, _, _ in DIMS]


def reference_theta():
    return {"lengthScale": [1.0] * FINGERS, "k": [math.sqrt(k_range(g)[0] * k_range(g)[1]) for g in range(GROUPS)],
            "zeta": [REFERENCE_ZETA] * GROUPS, "inertiaScale": [1.0] * GROUPS, "mask": [1] * FINGER_GROUPS}


def mask_ok(mask):
    """the constraint MorphologyManager.Sample enforces (:205-211)"""
    return sum(mask) >= MIN_ACTIVE_GROUPS and (not REQUIRE_THUMB or any(mask[g] for g in THUMB_GROUPS))


def sample_training_distribution(n, seed):
    """n draws with the distribution and constraint logic of MorphologyManager.Sample (:195-212): lengths uniform, k
    log-uniform in the class range, zeta uniform, inertia scale uniform, mask Bernoulli(0.8) redrawn up to 100 times until
    >= 6 active groups including a thumb group, else all true. numpy's generator replaces UnityEngine.Random (same
    distribution, not the same stream)."""
    rng = np.random.default_rng(seed); out = []
    for _ in range(n):
        t = {"lengthScale": [float(rng.uniform(*LENGTH_RANGE)) for _ in range(FINGERS)], "k": [], "zeta": [], "inertiaScale": []}
        for g in range(GROUPS):
            lo, hi = k_range(g); t["k"].append(float(math.exp(rng.uniform(math.log(lo), math.log(hi)))))
            t["zeta"].append(float(rng.uniform(*zeta_range(g)))); t["inertiaScale"].append(float(rng.uniform(*INERTIA_RANGE)))
        mask = [1] * FINGER_GROUPS
        for _attempt in range(100):
            m = [1 if rng.random() < MASK_ACTIVE_PROBABILITY else 0 for _ in range(FINGER_GROUPS)]
            if mask_ok(m): mask = m; break
        t["mask"] = mask; out.append(t)
    return out


def to_unit(theta):
    """theta dict -> 67-vector in the unit box (k on a log scale), mask bits 0 / 1"""
    v = []
    for kind, i, scale, (lo, hi) in DIMS:
        if kind == "mask": v.append(float(theta["mask"][i])); continue
        x = {"len": theta["lengthScale"], "k": theta["k"], "zeta": theta["zeta"], "inertia": theta["inertiaScale"]}[kind][i]
        v.append((math.log(x) - math.log(lo)) / (math.log(hi) - math.log(lo)) if scale == "log" else (x - lo) / (hi - lo))
    return np.array(v)


def from_unit(v):
    v = np.asarray(v, dtype=float); t = {"lengthScale": [], "k": [], "zeta": [], "inertiaScale": [], "mask": []}
    for (kind, i, scale, (lo, hi)), u in zip(DIMS, v):
        if kind == "mask": t["mask"].append(1 if u >= 0.5 else 0); continue
        u = min(1.0, max(0.0, float(u))); x = math.exp(math.log(lo) + u * (math.log(hi) - math.log(lo))) if scale == "log" else lo + u * (hi - lo)
        {"len": t["lengthScale"], "k": t["k"], "zeta": t["zeta"], "inertia": t["inertiaScale"]}[kind].append(float(x))
    return t


def in_bounds(theta, tol=1e-9):
    for kind, i, scale, (lo, hi) in DIMS:
        if kind == "mask":
            if theta["mask"][i] not in (0, 1): return False
            continue
        x = {"len": theta["lengthScale"], "k": theta["k"], "zeta": theta["zeta"], "inertia": theta["inertiaScale"]}[kind][i]
        if x < lo - tol or x > hi + tol: return False
    return True


def harness_fields(theta):
    """the ArticulatedGates config fields that pin this theta for every episode (Diagnostics/ArticulatedGates.cs, thetaFixed)"""
    return {"thetaFixed": True, "thetaLength": [float(x) for x in theta["lengthScale"]], "thetaK": [float(x) for x in theta["k"]],
            "thetaZeta": [float(x) for x in theta["zeta"]], "thetaInertia": [float(x) for x in theta["inertiaScale"]],
            "thetaMask": "".join("1" if b else "0" for b in theta["mask"])}


def summary_features(theta):
    """the theta summaries the eval CSV logs (for cross-checking a pass): mean finger k, mean zeta, mean inertia scale, active groups"""
    return {"kFingerMean": sum(theta["k"][:FINGER_GROUPS]) / FINGER_GROUPS, "zetaMean": sum(theta["zeta"][:FINGER_GROUPS]) / FINGER_GROUPS,
            "inertiaScaleMean": sum(theta["inertiaScale"][:FINGER_GROUPS]) / FINGER_GROUPS, "activeGroups": sum(theta["mask"]), "lenMean": sum(theta["lengthScale"]) / FINGERS}


if __name__ == "__main__":
    import json
    print(N_DIMS, "dims;", "reference k:", [round(x, 3) for x in reference_theta()["k"]])
    s = sample_training_distribution(3, 0); print(json.dumps(s[0])[:300]); print("round trip ok:", np.allclose(to_unit(from_unit(to_unit(s[0]))), to_unit(s[0])))
