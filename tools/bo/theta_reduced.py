"""Reduced theta space for the run-013 BO campaign (brief 2a): 20 dimensions, tied by joint class, mapped through theta_space.

  dims 0-4    length scale per finger (index, middle, ring, pinky, thumb: the theta_space / MorphologyManager.Sample order)
  dims 5-9    k per class      MCP, PIP, DIP, thumb, wrist   (log scale inside each group's own range)
  dims 10-14  zeta per class   MCP, PIP, DIP, thumb, wrist
  dims 15-19  inertia per class MCP, PIP, DIP, thumb, wrist
  mask: all 14 bits ON (not a dimension).

Class of a group g (MorphologyManager.StiffnessRange, MorphologyManager.cs:179-184): g < 12 -> g % 3 = 0 MCP (base), 1 PIP (middle),
2 DIP (end); g 12 thumb base, g 13 thumb end (both "thumb"); g 14-15 wrist.

Tying: every group of a class takes the SAME unit coordinate u of that class. For k the unit coordinate is log-scaled inside the
group's own range, x = exp(log lo + u (log hi - log lo)): for MCP / PIP / DIP / wrist all groups share one range, so the tie is one value;
for the thumb class the two thumb groups have different ranges (base 1-15, end 0.3-3), so they share u (the same quantile of their
ranges, and u = 0.5 gives each group its reference sqrt(lo hi)). zeta and inertia ranges are the same for every group of a class.

Bounds (all from Assets/Scripts/MorphologyManager.cs, verified 2026-09-29 against the working tree; last change 096f508):
  lengthScaleRange (0.8, 1.2)              :41
  stiffnessRangeBase (MCP) (0.5, 6)        :43
  stiffnessRangeMiddle (PIP) (0.3, 3)      :45
  stiffnessRangeEnd (DIP) (0.1, 1)         :47
  stiffnessRangeThumbBase (1, 15)          :49
  stiffnessRangeThumbEnd (0.3, 3)          :51
  wristStiffnessRange (1, 10)              :53
  logUniformStiffness true                 :55
  fingerZetaRange (0.3, 1.0)               :57
  wristZetaRange (0.3, 1.0)                :59
  referenceZeta 0.7                        :61
  inertiaScaleRange (0.5, 2.0)             :63
  ReferenceStiffness = sqrt(lo * hi)       :186
Reference theta (the spec nominal hand): lengths 1, k = sqrt(lo hi) per group, zeta 0.7, inertia 1, mask all on
(= theta_space.reference_theta()); its reduced vector is exact (checked in __main__).
"""
import math, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
from tools.bo import theta_space as TS

CLASSES = ("MCP", "PIP", "DIP", "thumb", "wrist")
N_DIMS = 20


def group_class(g):
    """MorphologyManager.cs:179-184"""
    if g >= TS.FINGER_GROUPS: return 4
    if g in TS.THUMB_GROUPS: return 3
    return g % 3


def _lin(u, lo, hi): return lo + u * (hi - lo)
def _log(u, lo, hi): return math.exp(math.log(lo) + u * (math.log(hi) - math.log(lo)))


# per dimension: (name, kind, class or finger, lo, hi, scale) in natural units; for k the listed range is the class's (thumb: the base range; the end group maps by quantile)
DIMS = ([(f"len_{TS.FINGER_NAMES[f]}", "len", f) + TS.LENGTH_RANGE + ("lin",) for f in range(TS.FINGERS)]
        + [(f"k_{c}", "k", i) + (TS.k_range([0, 1, 2, 12, 14][i])) + ("log",) for i, c in enumerate(CLASSES)]
        + [(f"zeta_{c}", "zeta", i) + (TS.ZETA_RANGE_WRIST if i == 4 else TS.ZETA_RANGE_FINGER) + ("lin",) for i, c in enumerate(CLASSES)]
        + [(f"inertia_{c}", "inertia", i) + TS.INERTIA_RANGE + ("lin",) for i, c in enumerate(CLASSES)])
DIM_NAMES = [d[0] for d in DIMS]
assert len(DIMS) == N_DIMS


def to_theta(u):
    """reduced unit vector (20,) in [0, 1] -> full theta dict (theta_space layout, 67 dims)"""
    u = np.clip(np.asarray(u, dtype=float), 0.0, 1.0)
    t = {"lengthScale": [_lin(u[f], *TS.LENGTH_RANGE) for f in range(TS.FINGERS)], "k": [], "zeta": [], "inertiaScale": [], "mask": [1] * TS.FINGER_GROUPS}
    for g in range(TS.GROUPS):
        c = group_class(g)
        t["k"].append(_log(u[5 + c], *TS.k_range(g))); t["zeta"].append(_lin(u[10 + c], *TS.zeta_range(g))); t["inertiaScale"].append(_lin(u[15 + c], *TS.INERTIA_RANGE))
    return {k: [float(x) for x in v] for k, v in t.items()}


def from_theta(theta):
    """full theta (tied by class, mask all on) -> reduced unit vector; raises if the theta is not tied"""
    full = TS.to_unit(theta); v = np.zeros(N_DIMS)
    v[:5] = full[:5]
    for c in range(5):
        gs = [g for g in range(TS.GROUPS) if group_class(g) == c]
        for off, base in ((5, 5), (10, 21), (15, 37)):
            vals = [full[base + g] for g in gs]
            if max(vals) - min(vals) > 1e-6: raise ValueError(f"theta not tied in class {CLASSES[c]}")
            v[off + c] = vals[0]
    return v


def reference_u():
    return from_theta(TS.reference_theta())


def natural(u):
    """the 20 reduced coordinates in natural units (k in N m/rad; thumb k = the thumb-base group's value)"""
    u = np.clip(np.asarray(u, dtype=float), 0, 1)
    return {n: (_log(x, lo, hi) if sc == "log" else _lin(x, lo, hi)) for (n, kind, i, lo, hi, sc), x in zip(DIMS, u)}


def sobol(n, d=N_DIMS, seed=0):
    """scrambled Sobol points in [0, 1)^d (torch.quasirandom; torch is in the venv)"""
    import torch
    return torch.quasirandom.SobolEngine(dimension=d, scramble=True, seed=seed).draw(n).double().numpy()


if __name__ == "__main__":
    r = reference_u(); t = to_theta(r); ref = TS.reference_theta()
    ok = all(np.allclose(t[k], ref[k]) for k in ref)
    print("dims", N_DIMS, DIM_NAMES); print("reference u", np.round(r, 6).tolist(), "maps to the spec reference hand:", ok)
    for u in sobol(64, seed=1):
        th = to_theta(u); assert TS.in_bounds(th) and np.allclose(from_theta(th), u, atol=1e-9)
    print("64 Sobol round trips ok; corners in bounds:", TS.in_bounds(to_theta(np.zeros(N_DIMS))), TS.in_bounds(to_theta(np.ones(N_DIMS))))
