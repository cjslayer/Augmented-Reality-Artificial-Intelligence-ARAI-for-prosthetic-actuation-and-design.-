"""Superquadric object bank for run 013 prep: deterministic shapes from (seed, index), OBJ + params CSV under Assets/Objects/Bank/.

Family: superellipsoids  ((|x/a1|^(2/e2) + |z/a2|^(2/e2))^(e2/e1) + |y/a3|^(2/e1)) = 1  in Unity's frame:
  local Y  = the LENGTH axis (a3 = half-length), vertical on the pedestal, the axis the existing tilt rule and yaw draw use;
  local X, Z = the CROSS-SECTION the hand closes on (a1, a2 = semi-axes; e2 = cross-section shape, 1 circle -> 0.3 rounded square, 1.5 rounded diamond);
  e1 = axial profile (1 ellipsoidal ends, 0.3 nearly flat ends and straight sides, 1.5 pinched ends).
Ranges (decided): cross-section diameters 30-80 mm (a1, a2 ~ U[15, 40] mm independently), length 80-280 mm (a3 ~ U[40, 140] mm),
e1, e2 ~ U[0.3, 1.5]. Every shape is convex (a superellipse |x|^p + |y|^p = 1 is convex for p = 2/e >= 1, i.e. e <= 2).
Draws: numpy default_rng([seed, index]) per index, so a shape depends on (seed, index) only.
Bank: index 0 = the anchor (the scene's own built-in-cylinder mesh, radius 25 mm, half-length 140 mm; exported by ObjectBank in the
Editor as obj_000.obj), 1-64 train, 65-80 held-out, all from the same distribution.
Mesh: parametric grid N_OMEGA x N_ETA rings + 2 poles = 114 vertices, every vertex an extreme point of a strictly convex surface, so the
convex hull has exactly the sampled vertices (<= 255, the PhysX limit) and at most 2 V - 4 = 224 triangles (<= 255 polygons even
without coplanar merging). Vertices are written in metres in Unity coordinates; faces are triangles.

usage: python -m tools.objects.superquadric_bank [--seed 13013] [--out Assets/Objects/Bank] [--check-anchor]
"""
import argparse, csv, math, os, sys
import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_SEED = 13013
N_TRAIN, N_HELDOUT = 64, 16
A12_RANGE_MM, A3_RANGE_MM, EPS_RANGE = (15.0, 40.0), (40.0, 140.0), (0.3, 1.5)
N_OMEGA, N_ETA = 16, 7          # azimuth samples x latitude rings (poles added): 16 x 7 + 2 = 114 vertices
ANCHOR = dict(a1=25.0, a2=25.0, a3=140.0, e1=0.0, e2=1.0)   # the built-in cylinder scaled by (0.05, 0.14, 0.05): e1 = 0 marks the cylinder limit


def split_of(index):
    return "anchor" if index == 0 else ("train" if index <= N_TRAIN else "heldout")


def params(seed, index):
    """(a1, a2, a3) in mm and (e1, e2) for bank index >= 1, a deterministic function of (seed, index)"""
    rng = np.random.default_rng([int(seed), int(index)])
    a1 = rng.uniform(*A12_RANGE_MM); a2 = rng.uniform(*A12_RANGE_MM); a3 = rng.uniform(*A3_RANGE_MM)
    e1 = rng.uniform(*EPS_RANGE); e2 = rng.uniform(*EPS_RANGE)
    return dict(a1=float(a1), a2=float(a2), a3=float(a3), e1=float(e1), e2=float(e2))


def _spow(x, e):
    return np.sign(x) * np.abs(x) ** e


def surface(p, eta, omega):
    """point (x, y, z) in mm, Unity frame (y = length axis)"""
    ce, se = np.cos(eta), np.sin(eta); cw, sw = np.cos(omega), np.sin(omega)
    x = p["a1"] * _spow(ce, p["e1"]) * _spow(cw, p["e2"])
    z = p["a2"] * _spow(ce, p["e1"]) * _spow(sw, p["e2"])
    y = p["a3"] * _spow(se, p["e1"])
    return np.stack([x, y, z], axis=-1)


def normal(p, eta, omega):
    """outward normal of the superellipsoid at (eta, omega) (Jaklic et al. 2000, eq. 2.29), Unity frame"""
    ce, se = np.cos(eta), np.sin(eta); cw, sw = np.cos(omega), np.sin(omega)
    nx = _spow(ce, 2 - p["e1"]) * _spow(cw, 2 - p["e2"]) / p["a1"]
    nz = _spow(ce, 2 - p["e1"]) * _spow(sw, 2 - p["e2"]) / p["a2"]
    ny = _spow(se, 2 - p["e1"]) / p["a3"]
    n = np.stack([nx, ny, nz], axis=-1); return n / np.linalg.norm(n, axis=-1, keepdims=True)


def build_mesh(p, n_omega=N_OMEGA, n_eta=N_ETA):
    """vertices (V x 3, mm) and triangles (F x 3, 0-based, wound so the face normal points outward under Unity's clockwise-front convention)"""
    etas = (np.arange(1, n_eta + 1) / (n_eta + 1) - 0.5) * math.pi         # rings strictly between the poles
    omegas = np.arange(n_omega) / n_omega * 2 * math.pi
    E, W = np.meshgrid(etas, omegas, indexing="ij")
    ring_v = surface(p, E, W).reshape(-1, 3); ring_n = normal(p, E, W).reshape(-1, 3)
    bottom = np.array([[0.0, -p["a3"], 0.0]]); top = np.array([[0.0, p["a3"], 0.0]])
    V = np.concatenate([bottom, ring_v, top]); N = np.concatenate([[[0, -1, 0]], ring_n, [[0, 1, 0]]]).astype(float)
    vid = lambda i, j: 1 + i * n_omega + (j % n_omega)
    tris = []
    for j in range(n_omega): tris.append((0, vid(0, j), vid(0, j + 1)))                       # bottom fan
    for i in range(n_eta - 1):
        for j in range(n_omega):
            a, b, c, d = vid(i, j), vid(i, j + 1), vid(i + 1, j), vid(i + 1, j + 1)
            tris.append((a, c, b)); tris.append((b, c, d))
    itop = len(V) - 1
    for j in range(n_omega): tris.append((itop, vid(n_eta - 1, j + 1), vid(n_eta - 1, j)))    # top fan
    T = np.array(tris, dtype=int)
    # orient every triangle so that its Unity-facing normal points away from the centroid (Unity: clockwise vertex order = front face, left-handed)
    c = V.mean(0)
    for k in range(len(T)):
        a, b, d = V[T[k]]; nrm = np.cross(b - a, d - a)      # right-handed normal of (a, b, d)
        # in Unity's left-handed frame a clockwise-seen-from-outside triangle has right-handed normal pointing INWARD; keep that
        if np.dot(nrm, (a + b + d) / 3 - c) > 0: T[k] = (T[k][0], T[k][2], T[k][1])
    return V, N, T


def hull_checks(V, N):
    """every sampled vertex is an extreme point iff its supporting plane (analytic outward normal) has all other vertices on the inner side"""
    d = (V[None, :, :] - V[:, None, :]) * N[:, None, :]      # (i, j, xyz): n_i . (v_j - v_i)
    slack = d.sum(-1); np.fill_diagonal(slack, -np.inf)
    max_violation = float(slack.max())                        # <= ~1e-9 mm when every vertex is extreme
    n_extreme = int((slack.max(1) <= 1e-6).sum())
    return n_extreme, max_violation


def mesh_volume_mm3(V, T):
    """signed volume by the divergence theorem (absolute value; the winding is Unity-clockwise so the sign is negative)"""
    a, b, c = V[T[:, 0]], V[T[:, 1]], V[T[:, 2]]
    return abs(float((np.einsum("ij,ij->i", a, np.cross(b, c))).sum()) / 6.0)


def analytic_volume_mm3(p):
    """2 a1 a2 a3 e1 e2 B(e1/2 + 1, e1) B(e2/2, e2/2) (Jaklic et al. 2000); the cylinder limit e1 -> 0 is pi a1 a2 2 a3"""
    if p["e1"] <= 0: return math.pi * p["a1"] * p["a2"] * 2 * p["a3"]
    B = lambda x, y: math.gamma(x) * math.gamma(y) / math.gamma(x + y)
    return 2 * p["a1"] * p["a2"] * p["a3"] * p["e1"] * p["e2"] * B(p["e1"] / 2 + 1, p["e1"]) * B(p["e2"] / 2, p["e2"] / 2)


def write_obj(path, V_mm, T, comment):
    with open(path, "w", newline="\n") as f:
        f.write("# " + comment + "\n# units: metres, Unity frame (y = length axis); faces are triangles, Unity-clockwise front faces\n")
        for v in V_mm: f.write("v %.7f %.7f %.7f\n" % (v[0] * 1e-3, v[1] * 1e-3, v[2] * 1e-3))
        for t in T: f.write("f %d %d %d\n" % (t[0] + 1, t[1] + 1, t[2] + 1))


def read_obj(path):
    V, T = [], []
    for line in open(path):
        s = line.split()
        if not s: continue
        if s[0] == "v": V.append([float(s[1]), float(s[2]), float(s[3])])
        elif s[0] == "f": T.append([int(x.split("/")[0]) - 1 for x in s[1:4]])
    return np.array(V), np.array(T, dtype=int)


def anchor_regeneration(n_sides=20):
    """a 20-gon prism, radius 25 mm, half-length 140 mm, with cap-centre vertices: what Unity's built-in cylinder is expected to be"""
    th = np.arange(n_sides) / n_sides * 2 * math.pi
    ring = np.stack([25.0 * np.cos(th), np.zeros(n_sides), 25.0 * np.sin(th)], -1)
    return np.concatenate([ring + [0, -140, 0], ring + [0, 140, 0], [[0, -140, 0]], [[0, 140, 0]]])


def check_anchor(out_dir):
    """compare the Editor-exported anchor OBJ (obj_000.obj, the scene mesh x its scale) with the regeneration: unique positions and nearest-neighbour distances"""
    path = os.path.join(out_dir, "obj_000.obj")
    if not os.path.exists(path): print("anchor OBJ not exported yet (ObjectBank writes it in the Editor):", path); return None
    V, T = read_obj(path); Vmm = V * 1e3
    uniq = np.unique(np.round(Vmm, 4), axis=0)
    R = anchor_regeneration()
    # best rotation about y is unknown: align by the azimuth of the first ring vertex
    ring = uniq[np.abs(uniq[:, 1] + 140) < 1e-3]; ring = ring[np.hypot(ring[:, 0], ring[:, 2]) > 1]
    phase = math.atan2(ring[0, 2], ring[0, 0]) if len(ring) else 0.0
    c, s = math.cos(phase), math.sin(phase); Rr = R.copy(); Rr[:, 0] = R[:, 0] * c - R[:, 2] * s; Rr[:, 2] = R[:, 0] * s + R[:, 2] * c
    d = np.linalg.norm(uniq[:, None, :] - Rr[None, :, :], axis=-1)
    nn = d.min(1); nn2 = d.min(0)
    res = dict(obj_vertices=len(V), unique_positions=len(uniq), regen_positions=len(R), max_nn_mm=float(nn.max()), max_nn_back_mm=float(nn2.max()),
               radius_mm=float(np.hypot(ring[:, 0], ring[:, 2]).mean()) if len(ring) else float("nan"), half_length_mm=float(np.abs(uniq[:, 1]).max()),
               ring_count=int(len(ring)), triangles=len(T), mesh_volume_cm3=mesh_volume_mm3(Vmm, T) * 1e-3)
    return res


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0]); ap.add_argument("--seed", type=int, default=DEFAULT_SEED)
    ap.add_argument("--out", default="Assets/Objects/Bank"); ap.add_argument("--check-anchor", action="store_true"); a = ap.parse_args()
    out = os.path.join(REPO, a.out); os.makedirs(out, exist_ok=True)
    if a.check_anchor:
        r = check_anchor(out); print("anchor check:", r); return
    rows = []; worst = 0.0
    rows.append(dict(index=0, split="anchor", file="obj_000.obj", **{k + ("_mm" if k[0] == "a" else ""): v for k, v in ANCHOR.items()},
                     volume_cm3=analytic_volume_mm3(ANCHOR) * 1e-3, mesh_volume_cm3=math.pi * 25 ** 2 * 280 * 1e-3, extent_x_mm=50.0, extent_y_mm=280.0, extent_z_mm=50.0,
                     n_vertices="(scene mesh)", n_hull_vertices="(scene mesh)", n_triangles="(scene mesh)", max_convexity_violation_mm=0.0))
    for index in range(1, N_TRAIN + N_HELDOUT + 1):
        p = params(a.seed, index); V, N, T = build_mesh(p); n_ext, viol = hull_checks(V, N); worst = max(worst, viol)
        fn = "obj_%03d.obj" % index
        write_obj(os.path.join(out, fn), V, T, "superquadric bank seed %d index %d (%s): a1 %.3f a2 %.3f a3 %.3f mm, e1 %.4f e2 %.4f" % (a.seed, index, split_of(index), p["a1"], p["a2"], p["a3"], p["e1"], p["e2"]))
        rows.append(dict(index=index, split=split_of(index), file=fn, a1_mm=p["a1"], a2_mm=p["a2"], a3_mm=p["a3"], e1=p["e1"], e2=p["e2"],
                         volume_cm3=analytic_volume_mm3(p) * 1e-3, mesh_volume_cm3=mesh_volume_mm3(V, T) * 1e-3,
                         extent_x_mm=2 * p["a1"], extent_y_mm=2 * p["a3"], extent_z_mm=2 * p["a2"], n_vertices=len(V), n_hull_vertices=n_ext, n_triangles=len(T), max_convexity_violation_mm=viol))
        assert len(V) <= 255 and len(T) <= 255 and n_ext == len(V), (index, len(V), len(T), n_ext, viol)
    fields = ["index", "split", "file", "a1_mm", "a2_mm", "a3_mm", "e1", "e2", "volume_cm3", "mesh_volume_cm3", "extent_x_mm", "extent_y_mm", "extent_z_mm", "n_vertices", "n_hull_vertices", "n_triangles", "max_convexity_violation_mm"]
    with open(os.path.join(out, "bank.csv"), "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields); w.writeheader()
        for r in rows: w.writerow({k: (("%.6g" % v) if isinstance(v, float) else v) for k, v in r.items()})
    with open(os.path.join(out, "BANK_SEED.txt"), "w") as f: f.write("%d\n" % a.seed)
    print("wrote", len(rows), "rows to", os.path.join(out, "bank.csv"), "| vertices per shape", N_OMEGA * N_ETA + 2, "| triangles", 2 * (N_OMEGA * N_ETA + 2) - 4, "| worst convexity violation (mm)", worst)


if __name__ == "__main__":
    main()
