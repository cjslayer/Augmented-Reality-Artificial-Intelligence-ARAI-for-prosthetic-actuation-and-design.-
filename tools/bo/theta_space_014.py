"""theta space of the run-014 slot layout (docs/design/014_topology.md; Assets/Scripts/ArticulatedHand.cs HandLayout, MorphologyManager.cs).

Layout (the legacy 67-dim space of tools/bo/theta_space.py is the 5-digit prefix of this one):
  existence   7 digit bits (digit ids: 0 index, 1 middle, 2 ring, 3 pinky, 4 thumb, 5 extra ulnar finger, 6 second thumb); index and thumb always 1;
              the run-014 compositions (HandLayout.Composition, ArticulatedHand.cs:152): 3 = thumb+index+middle, 4 = +ring, 5 = +pinky (the 013 hand),
              6a = +extra finger, 6b = +second thumb, 7 = +extra finger +second thumb
  lengthScale 7 per digit id                       MorphologyManager.cs:60  lengthScaleRange (0.8, 1.2)
  k           21 per morphology index (legacy 0-13 finger groups, 14-15 wrist, 16-18 extra finger base/middle/end, 19-20 second thumb base/end)
              class ranges (log-uniform, :74):  MCP (0.5, 6) :62, PIP (0.3, 3) :64, DIP (0.1, 1) :66, thumb base (1, 15) :68, thumb end (0.3, 3) :70, wrist (1, 10) :72
  zeta        21                                   finger (0.3, 1.0) :76, wrist (0.3, 1.0) :78; reference 0.7 :80
  inertia     21                                   (0.5, 2.0) :82
  mask        19 per finger group (legacy 0-13, 14-16 extra finger, 17-18 second thumb); p 0.8 :84; min active min(6, present groups) :86; a thumb group of any
              thumb active :88
Training distribution over the composition: MorphologyManager.cs:47 digitDistribution = P(3, 4, 5, 6a, 6b, 7) = (0.10, 0.15, 0.45, 0.075, 0.075, 0.15).
The harness pins a slot-layout theta with harness_fields() (ArticulatedGates Config: thetaLength 7, thetaK / thetaZeta / thetaInertia 21, thetaMask 19 chars,
thetaExist 7 chars in design-doc slot order: slot 0 primary thumb, 1-4 index..pinky, 5 extra finger, 6 second thumb).
"""
import math
import numpy as np

DIGITS = ["index", "middle", "ring", "pinky", "thumb", "extra", "thumb2"]
SLOT_DIGIT = [4, 0, 1, 2, 3, 5, 6]                 # design-doc slot -> digit id
GROUP_NAMES = ["indexBase", "indexMiddle", "indexEnd", "middleBase", "middleMiddle", "middleEnd", "ringBase", "ringMiddle", "ringEnd",
               "pinkyBase", "pinkyMiddle", "pinkyEnd", "thumbBase", "thumbEnd", "extraBase", "extraMiddle", "extraEnd", "thumb2Base", "thumb2End"]
GROUP_DIGIT = [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 5, 5, 5, 6, 6]
GROUP_SEGMENT = [0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 0, 1, 2, 0, 1]
MORPH_NAMES = GROUP_NAMES[:14] + ["wristFlex", "wristPron"] + GROUP_NAMES[14:]
N_DIGITS, N_GROUPS, N_MORPH = 7, 19, 21
LENGTH_RANGE = (0.8, 1.2)
K_RANGE = {"mcp": (0.5, 6.0), "pip": (0.3, 3.0), "dip": (0.1, 1.0), "thumbBase": (1.0, 15.0), "thumbEnd": (0.3, 3.0), "wrist": (1.0, 10.0)}
ZETA_RANGE, INERTIA_RANGE, REFERENCE_ZETA = (0.3, 1.0), (0.5, 2.0), 0.7
MASK_P, MIN_ACTIVE = 0.8, 6
COMPOSITIONS = {"3": 3, "4": 4, "5": 5, "6a": 6, "6b": 6, "7": 7}
DISTRIBUTION = {"3": 0.10, "4": 0.15, "5": 0.45, "6a": 0.075, "6b": 0.075, "7": 0.15}


def is_thumb_digit(d): return d in (4, 6)
def morph_to_group(m): return m if m < 14 else (-1 if m < 16 else m - 2)
def group_to_morph(g): return g if g < 14 else g + 2


def k_range(m):
    """StiffnessRange(m), MorphologyManager.cs (class of the morphology index)"""
    g = morph_to_group(m)
    if g < 0: return K_RANGE["wrist"]
    if is_thumb_digit(GROUP_DIGIT[g]): return K_RANGE["thumbBase"] if GROUP_SEGMENT[g] == 0 else K_RANGE["thumbEnd"]
    return (K_RANGE["mcp"], K_RANGE["pip"], K_RANGE["dip"])[GROUP_SEGMENT[g]]


def composition(name):
    """existence (digit ids) of a composition name, as HandLayout.Composition"""
    e = [False] * N_DIGITS; e[0] = e[4] = True; n = COMPOSITIONS[name]
    if n >= 3: e[1] = True
    if n >= 4: e[2] = True
    if n >= 5: e[3] = True
    if name == "6a": e[5] = True
    if name == "6b": e[6] = True
    if name == "7": e[5] = e[6] = True
    return e


def slot_string(e): return "".join("1" if e[SLOT_DIGIT[k]] else "0" for k in range(N_DIGITS))
def present_groups(e): return [g for g in range(N_GROUPS) if e[GROUP_DIGIT[g]]]


def reference_theta(comp="5"):
    """the nominal hand of a composition: lengths 1, k = sqrt(lo hi) of the class range, zeta 0.7, inertia 1, every present group actuated"""
    e = composition(comp)
    return {"exist": e, "lengthScale": [1.0] * N_DIGITS, "k": [math.sqrt(k_range(m)[0] * k_range(m)[1]) for m in range(N_MORPH)],
            "zeta": [REFERENCE_ZETA] * N_MORPH, "inertiaScale": [1.0] * N_MORPH, "mask": [1] * N_GROUPS}


def mask_ok(mask, e):
    pg = present_groups(e); active = sum(mask[g] for g in pg)
    return active >= min(MIN_ACTIVE, len(pg)) and any(mask[g] for g in pg if is_thumb_digit(GROUP_DIGIT[g]))


def sample_training_distribution(n, seed):
    """n draws with the distribution of MorphologyManager.Sample in the slot layout (numpy stream, not UnityEngine.Random)"""
    rng = np.random.default_rng(seed); names = list(DISTRIBUTION); p = np.array([DISTRIBUTION[k] for k in names]); p /= p.sum(); out = []
    for _ in range(n):
        comp = names[rng.choice(len(names), p=p)]; e = composition(comp)
        t = {"composition": comp, "exist": e, "lengthScale": [float(rng.uniform(*LENGTH_RANGE)) for _ in range(N_DIGITS)], "k": [], "zeta": [], "inertiaScale": []}
        for m in range(N_MORPH):
            lo, hi = k_range(m); t["k"].append(float(math.exp(rng.uniform(math.log(lo), math.log(hi))))); t["zeta"].append(float(rng.uniform(*ZETA_RANGE))); t["inertiaScale"].append(float(rng.uniform(*INERTIA_RANGE)))
        mask = [1] * N_GROUPS
        for _ in range(100):
            m_ = [1 if (g not in present_groups(e)) or rng.random() < MASK_P else 0 for g in range(N_GROUPS)]
            if mask_ok(m_, e): mask = m_; break
        t["mask"] = mask; out.append(t)
    return out


def harness_fields(theta):
    """ArticulatedGates config fields that pin a slot-layout theta for every episode (thetaFixed, slot format)"""
    return {"thetaFixed": True, "topology": "slots", "thetaLength": [float(x) for x in theta["lengthScale"]], "thetaK": [float(x) for x in theta["k"]],
            "thetaZeta": [float(x) for x in theta["zeta"]], "thetaInertia": [float(x) for x in theta["inertiaScale"]],
            "thetaMask": "".join("1" if b else "0" for b in theta["mask"]), "thetaExist": slot_string(theta["exist"])}


def legacy_prefix(theta):
    """the 013 (67-dim, tools/bo/theta_space.py) view of a five-digit slot-layout theta"""
    assert slot_string(theta["exist"]) == "1111100", "not the five-digit hand"
    return {"lengthScale": theta["lengthScale"][:5], "k": theta["k"][:16], "zeta": theta["zeta"][:16], "inertiaScale": theta["inertiaScale"][:16], "mask": theta["mask"][:14]}


if __name__ == "__main__":
    for c in COMPOSITIONS: print(c, slot_string(composition(c)), "present groups", len(present_groups(composition(c))))
    r = reference_theta(); print("reference k (morph order)", [round(x, 3) for x in r["k"]])
    s = sample_training_distribution(2000, 0); from collections import Counter; print("composition shares", {k: round(v / 2000, 3) for k, v in sorted(Counter(x["composition"] for x in s).items())})
    import sys, os; sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
    from tools.bo import theta_space as TS
    lp = legacy_prefix(r); ref = TS.reference_theta(); print("five-digit prefix equals the legacy reference:", all(np.allclose(lp[k], ref[k]) for k in ref))
