"""Discrete outer loop over hand morphology on the 014CD policy (brief 2026-10-13, Part 2). Editor harness only (ArticulatedGates mode eval), nothing trains.

Candidate = (composition c, actuation mask m over the joint groups present in c). Every candidate is pinned for every episode with the fixed-theta path
(thetaFixed, slot format): nominal theta of the composition (tools/bo/theta_space_014.reference_theta: length scale 1, class-reference k, zeta 0.7,
inertia scale 1) with the candidate's mask; absent groups carry no bit. Instrument as in tools/eval/eval_014.py: perturbation 1.0, K 50, mu 1.0,
lift budget 300, held-out shapes 65-80 (stratified), one job worker, per-episode reseed, spawnStream placements, model results/014/CD/Prosthetic_CD.onnx.

J(c, m) = success over the search block: upright (canonical) seeds 13001-13064 + lying (randomStable) seeds 13065-13128 = 16 held-out shapes x 4
seeds per pose, 128 episodes; deterministic head (sampled head recorded in stages A and D only).
  A  all-on baseline per composition (det + sampled)
  B  single ablation: all-on with one present group off (det); delta_g = J(all-on) - J(g off)
  C  ordered elimination: remove groups in ascending delta_g (ties: group index), re-evaluate after each removal, stop at J < J(all-on) - 0.10 or
     when no group is left (greedy path, not a combinatorial search); step 1 equals a stage-B candidate and is re-run as a determinism check
  D  confirmation on fresh seeds 13201-13500 per pose (n 300 each), det + sampled: all-on and the last elimination step with J >= J(all-on) - 0.05
     and with J >= J(all-on) - 0.10, paired with all-on on identical rows

Checks on every pass (a failure stops the run, see StopRun): n rows, activeGroups == n(m), digitCount / slots of the composition, lenMean and
inertiaScaleMean at 1 (theta pinned), head_mode, no NaN / inf, placement (objIndex, poseMode, poseIndex, spawnYaw) identical to the first pass of the
same seed block, 5-digit all-on det J within 0.05 of the nominal-theta reference 0.898 (0.984 upright / 0.812 lying; re-based 2026-10-13 from the
first run's 0.60-0.75 band, which came from the randomized-theta eval mean 0.673), protected model hashes before and after.

usage: python -m tools.bo.discrete_search STAGE [STAGE ...]   (STAGE: A B C D report)
outputs: results/bo/discrete/ (passes/, passes.json, allon.csv, ablation.csv, elimination.csv, confirm.csv, pareto.csv)
"""
import csv, hashlib, json, math, os, subprocess, sys, time
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); sys.path.insert(0, REPO)
from tools.bo.gates_runner import run_pass, pass_hash, play_mode, _cli
from tools.bo import theta_space_014 as TS
from tools.objects import run_zero_shot as Z
from tools.eval.report_014 import wilson, mcnemar, ok

OUT = "results/bo/discrete"; PASSES = f"{OUT}/passes"; LOG = os.path.join(REPO, OUT, "passes.json")
MODEL = "results/014/CD/Prosthetic_CD.onnx"
PROTECTED = {MODEL: "68d86b813cc8ab671696f297c53a7e7f08ad2c091a3f0203d4ddbbfdaea8a9cf",
             "results/014/Prosthetic.onnx": "164d42736cd7d0d5f80067fe1b1a8c8c28ddd54f5424267dfbe98f0e75bf24cd"}
COMPS = ["3", "4", "5", "6a", "6b", "7"]
SEARCH = {"canonical": (13001, 13064), "randomStable": (13065, 13128)}
CONFIRM = {"canonical": (13201, 13500), "randomStable": (13201, 13500)}
THROWAWAY = (6091, 6100)   # spent seeds; the instrument's throwaway pass after every Editor launch
SANITY_REF, SANITY_TOL = 0.898, 0.05   # 5-digit all-on det J at nominal theta, mask pinned (first stage-A passes); check |J - ref| <= tol
RELAUNCH_EVERY = 100       # passes between Editor relaunches (013 Part C practice)
POSE_NAME = {"canonical": "upright", "randomStable": "lying"}
os.makedirs(os.path.join(REPO, PASSES), exist_ok=True)
log = json.load(open(LOG)) if os.path.exists(LOG) else []
done = {e["tag"]: e for e in log if e.get("ok")}
state = {"since_launch": 0}


class StopRun(RuntimeError):
    pass


def sha256(path):
    h = hashlib.sha256()
    with open(os.path.join(REPO, path), "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""): h.update(b)
    return h.hexdigest()


def check_protected(when):
    for p, want in PROTECTED.items():
        got = sha256(p)
        if got != want: raise StopRun(f"protected file hash changed ({when}): {p} {got}")
    print(f"protected hashes OK ({when})", flush=True)


def present(comp): return TS.present_groups(TS.composition(comp))


def mask_vec(comp, off):
    """19-entry mask: 1 = actuated; absent groups 1 (they carry no bit); groups in `off` 0"""
    return [0 if g in off else 1 for g in range(TS.N_GROUPS)]


def n_on(comp, off): return len([g for g in present(comp) if g not in off])
def names(gs): return ";".join(TS.GROUP_NAMES[g] for g in gs)


def config(comp, off, pose, seeds, head, csv_path):
    th = TS.reference_theta(comp); th["mask"] = mask_vec(comp, off)
    c = Z.base_config(seeds, csv_path, perturb=1.0, stratify="heldout")
    digits = TS.COMPOSITIONS[comp]
    c.update({"modelPath": MODEL, "objectPose": pose, "headMode": head, "liftBudgetSteps": 300, "spawnStream": True,
              "topology": "slots", "digits": digits, "digits6b": comp == "6b"})
    c.update(TS.harness_fields(th))
    return c


def save_log(): json.dump(log, open(LOG, "w"), indent=1)


# ---- Editor lifecycle ----
def editor_up():
    return play_mode() != "unreachable"


def launch_editor():
    """start the Editor on the project (detached) and wait until the pipeline answers, then the throwaway pass"""
    subprocess.Popen(["unity", "open", REPO, "--no-banner"], cwd=REPO, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0))
    for _ in range(120):
        time.sleep(10)
        if play_mode() == "stopped": break
    else: raise StopRun("Editor did not come up within 20 min")
    time.sleep(20); print("Editor up", flush=True); state["since_launch"] = 0
    throwaway()


def editor_pid_alive():
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Unity.exe"], capture_output=True, text=True).stdout
    return "Unity.exe" in out


def close_editor():
    if editor_up():
        if play_mode() != "stopped": _cli("editor_stop"); time.sleep(5)
        _cli("menu", "--path", "File/Exit")
    for _ in range(60):
        if not editor_pid_alive(): break
        time.sleep(5)
    else: raise StopRun("Editor did not exit")
    time.sleep(5); print("Editor closed", flush=True)


def ensure_editor():
    if not editor_up(): launch_editor()
    elif state["since_launch"] >= RELAUNCH_EVERY: close_editor(); launch_editor()


def throwaway():
    tag = "throwaway_" + time.strftime("%Y%m%d_%H%M%S"); csv_path = f"{PASSES}/{tag}.csv"
    path, wall = run_pass(config("5", [], "canonical", THROWAWAY, "deterministic", csv_path), max_wait=1800)
    log.append({"tag": tag, "ok": True, "throwaway": True, "csv": csv_path, "hash": pass_hash(path), "wall_s": round(wall), "time": time.strftime("%F %T")}); save_log()
    print(f"{tag}: {wall:.0f} s (discarded)", flush=True)


# ---- one pass ----
def load(csv_path): return [r for r in csv.DictReader(open(os.path.join(REPO, csv_path), newline="")) if (r.get("seed") or "").isdigit()]


def validate(tag, rows, comp, off, pose, seeds, head):
    n_exp = seeds[1] - seeds[0] + 1
    if len(rows) != n_exp: raise StopRun(f"{tag}: {len(rows)} rows, expected {n_exp}")
    want_active, want_slots, want_digits = str(n_on(comp, off)), TS.slot_string(TS.composition(comp)), str(sum(TS.composition(comp)))
    for r in rows:
        if r["activeGroups"] != want_active: raise StopRun(f"{tag}: seed {r['seed']} activeGroups {r['activeGroups']} != n(m) {want_active} (row invalid)")
        if r["slots"] != want_slots or r["digitCount"] != want_digits: raise StopRun(f"{tag}: seed {r['seed']} composition {r['slots']}/{r['digitCount']} != {want_slots}/{want_digits}")
        if abs(float(r["lenMean"]) - 1) > 1e-3 or abs(float(r["inertiaScaleMean"]) - 1) > 1e-3: raise StopRun(f"{tag}: seed {r['seed']} theta not pinned (lenMean {r['lenMean']}, inertiaScaleMean {r['inertiaScaleMean']})")
        if r["head_mode"][:3] != head[:3]: raise StopRun(f"{tag}: head_mode {r['head_mode']} != {head}")
        for k, v in r.items():
            if v and v.strip().lower() in ("nan", "-nan", "inf", "-inf", "infinity", "-infinity"): raise StopRun(f"{tag}: seed {r['seed']} {k} = {v}")
    # placement: identical to the first pass of this seed block (spawnStream pairs placements across compositions and masks)
    block = f"{pose}_{seeds[0]}_{seeds[1]}"
    ref = next((e for e in log if e.get("ok") and not e.get("throwaway") and e.get("block") == block and e["tag"] != tag), None)
    if ref is not None:
        R = {x["seed"]: x for x in load(ref["csv"])}
        bad = [r["seed"] for r in rows if r["seed"] not in R or any(r[k] != R[r["seed"]][k] for k in ("objIndex", "poseMode", "poseIndex", "spawnYaw"))]
        if bad: raise StopRun(f"{tag}: placement mismatch vs {ref['tag']} on seeds {bad[:10]} ({len(bad)} rows)")
    return block


def evaluate(tag, comp, off, pose, seeds, head):
    """one pass; returns its rows (validated). Resumable by tag."""
    if tag in done: return load(done[tag]["csv"])
    ensure_editor()
    csv_path = f"{PASSES}/{tag}.csv"; t0 = time.time()
    try: path, wall = run_pass(config(comp, off, pose, seeds, head, csv_path), max_wait=5400)
    except Exception as ex:
        log.append({"tag": tag, "ok": False, "error": repr(ex)[:400], "time": time.strftime("%F %T")}); save_log()
        raise StopRun(f"{tag}: harness pass failed: {repr(ex)[:300]}")
    state["since_launch"] += 1
    rows = load(csv_path); block = validate(tag, rows, comp, off, pose, seeds, head)
    k = sum(ok(r) for r in rows)
    e = {"tag": tag, "ok": True, "csv": csv_path, "hash": pass_hash(path), "wall_s": round(wall), "time": time.strftime("%F %T"), "block": block,
         "comp": comp, "off": [TS.GROUP_NAMES[g] for g in off], "n_on": n_on(comp, off), "pose": pose, "head": head, "n": len(rows), "k": k}
    log.append(e); done[tag] = e; save_log()
    print(f"{tag}: {k}/{len(rows)} = {k / len(rows):.3f}  ({wall:.0f} s)", flush=True)
    return rows


def key(comp, off): return f"c{comp}_" + ("allon" if not off else "off" + "-".join(str(g) for g in sorted(off)))


def J(comp, off, head="deterministic", stage="S"):
    """search-block J: (J, upright, lying, rows-by-(pose, seed))"""
    res, allrows = {}, {}
    for pose, seeds in SEARCH.items():
        rows = evaluate(f"{stage}_{key(comp, off)}_{pose}_{head[:3]}", comp, off, pose, seeds, head)
        res[pose] = sum(ok(r) for r in rows) / len(rows)
        for r in rows: allrows[(pose, r["seed"])] = r
    j = sum(ok(r) for r in allrows.values()) / len(allrows)
    return j, res["canonical"], res["randomStable"], allrows


def write_csv(name, header, rows):
    with open(os.path.join(REPO, OUT, name), "w", newline="") as f:
        w = csv.writer(f); w.writerow(header); w.writerows(rows)
    print("wrote", f"{OUT}/{name}", flush=True)


def f3(x): return f"{x:.4f}"


# ---- stages ----
def stage_A():
    out = {}
    for comp in ["5"] + [c for c in COMPS if c != "5"]:   # the 5-digit sanity check first
        jd, ud, ld, R = J(comp, [], "deterministic", "A")
        if comp == "5" and abs(jd - SANITY_REF) > SANITY_TOL + 1e-9: raise StopRun(f"5-digit all-on det J {jd:.3f} (upright {ud:.3f}, lying {ld:.3f}) outside {SANITY_REF} +/- {SANITY_TOL}")
        js, us, ls, _ = J(comp, [], "sampled", "A")
        lo, hi = wilson(round(jd * len(R)), len(R))
        out[comp] = [comp, len(present(comp)), f3(jd), f3(lo), f3(hi), f3(ud), f3(ld), f3(js), f3(us), f3(ls), len(R)]
    write_csv("allon.csv", ALLON_H, [out[c] for c in COMPS])
ALLON_H = ["composition", "n_groups", "J_det", "J_det_lo", "J_det_hi", "upright_det", "lying_det", "J_sam", "upright_sam", "lying_sam", "n"]


def allon_J(comp): return J(comp, [], "deterministic", "A")[0]


def ablation(comp):
    """[(g, J, upright, lying, delta)] for each present group, in group order"""
    j0 = allon_J(comp); res = []
    for g in present(comp):
        j, u, l, _ = J(comp, [g], "deterministic", "B"); res.append((g, j, u, l, j0 - j))
    return j0, res


def stage_B():
    out = []
    for comp in COMPS:
        j0, res = ablation(comp)
        ranked = sorted(res, key=lambda t: (-t[4], t[0]))
        rank = {t[0]: i + 1 for i, t in enumerate(ranked)}
        for g, j, u, l, d in res:
            flag = "thumbBase" if g == 12 else ("thumb2" if g in (17, 18) else ("thumb" if g == 13 else ""))
            out.append([comp, TS.GROUP_NAMES[g], g, n_on(comp, [g]), f3(j), f3(u), f3(l), f3(j0), f3(d), rank[g], flag])
    write_csv("ablation.csv", ["composition", "group", "group_index", "n_on", "J", "upright", "lying", "J_allon", "delta", "rank_desc", "flag"], out)


def elimination(comp):
    """the greedy path: [(step, removed g, off list, J, upright, lying)]; step 0 = all-on"""
    j0, res = ablation(comp)
    order = [t[0] for t in sorted(res, key=lambda t: (t[4], t[0]))]   # ascending delta = least important first
    path = [(0, None, [], j0, *J(comp, [], "deterministic", "A")[1:3])]
    off = []
    for g in order:
        off = off + [g]; j, u, l, _ = J(comp, off, "deterministic", "C")
        path.append((len(off), g, list(off), j, u, l))
        if j < j0 - 0.10: break
    return j0, path


def frontier(j0, path):
    """last step with J >= J0 - 0.05 and last step with J >= J0 - 0.10 (step 0 = all-on qualifies by definition)"""
    p05 = max((p for p in path if p[3] >= j0 - 0.05 - 1e-9), key=lambda p: p[0])
    p10 = max((p for p in path if p[3] >= j0 - 0.10 - 1e-9), key=lambda p: p[0])
    return p05, p10


def stage_C():
    out = []; det = []
    for comp in COMPS:
        j0, path = elimination(comp)
        p05, p10 = frontier(j0, path)
        for step, g, off, j, u, l in path:
            out.append([comp, step, "" if g is None else TS.GROUP_NAMES[g], names(off), n_on(comp, off), f3(j), f3(u), f3(l), f3(j0), f3(j0 - j),
                        int(step == p05[0]), int(step == p10[0]), int(j < j0 - 0.10)])
        # determinism: step 1 (one group off) = the stage-B candidate on the same seeds; the pass hashes must agree
        if len(path) > 1:
            g1 = path[1][2]
            for pose in SEARCH:
                hb, hc = done[f"B_{key(comp, g1)}_{pose}_det"]["hash"], done[f"C_{key(comp, g1)}_{pose}_det"]["hash"]
                det.append(f"{comp} {pose}: B {hb} C {hc} {'match' if hb == hc else 'MISMATCH'}")
    write_csv("elimination.csv", ["composition", "step", "removed", "groups_off", "n_on", "J", "upright", "lying", "J_allon", "drop", "frontier_005", "frontier_010", "below_010"], out)
    print("determinism (stage C step 1 vs stage B):\n  " + "\n  ".join(det), flush=True)
    with open(os.path.join(REPO, OUT, "determinism_C.txt"), "w") as f: f.write("\n".join(det) + "\n")


def points(comp):
    j0, path = elimination(comp)
    p05, p10 = frontier(j0, path)
    pts = [("allon", 0, [])]
    if p05[0] > 0: pts.append(("f005", p05[0], p05[2]))
    if p10[0] > 0 and p10[0] != p05[0]: pts.append(("f010", p10[0], p10[2]))
    elif p10[0] > 0: pts.append(("f010=f005", p10[0], p10[2]))
    return pts


def confirm_rows(comp, off, pose, head):
    return {r["seed"]: r for r in evaluate(f"D_{key(comp, off)}_{pose}_{head[:3]}", comp, off, pose, CONFIRM[pose], head)}


def paired_d(A, B, keys):
    d = [int(ok(A[s])) - int(ok(B[s])) for s in keys]; n = len(d); m = sum(d) / n
    sd = math.sqrt(sum((x - m) ** 2 for x in d) / (n - 1)) if n > 1 else 0.0; h = 1.96 * sd / math.sqrt(n)
    b = sum(x == 1 for x in d); c = sum(x == -1 for x in d)
    return m, m - h, m + h, b, c, mcnemar(b, c)


def stage_D():
    conf, pareto = [], []
    for comp in COMPS:
        pts = points(comp)
        for head in ("deterministic", "sampled"):
            base = {pose: confirm_rows(comp, [], pose, head) for pose in CONFIRM}
            for label, step, off in pts:
                cur = {pose: confirm_rows(comp, off, pose, head) for pose in CONFIRM}
                cells = {}
                for pose in CONFIRM:
                    A, B = cur[pose], base[pose]; keys = sorted(A)
                    bad = [s for s in keys if any(A[s][k] != B[s][k] for k in ("objIndex", "poseMode", "poseIndex", "spawnYaw"))]
                    if bad: raise StopRun(f"stage D {comp} {label} {pose} {head}: placement mismatch vs all-on on {len(bad)} rows")
                    k = sum(ok(A[s]) for s in keys); n = len(keys); lo, hi = wilson(k, n)
                    m, dlo, dhi, b, c, p = paired_d(A, B, keys) if off else (0.0, 0.0, 0.0, 0, 0, 1.0)
                    cells[pose] = (k, n, lo, hi)
                    conf.append([comp, label, step, n_on(comp, off), names(off), POSE_NAME[pose], head[:3], n, f3(k / n), f3(lo), f3(hi), f3(m), f3(dlo), f3(dhi), b, c, f"{p:.3g}"])
                # both poses: 600 rows, paired over (pose, seed)
                A2 = {(p, s): cur[p][s] for p in CONFIRM for s in cur[p]}; B2 = {(p, s): base[p][s] for p in CONFIRM for s in base[p]}; keys2 = sorted(A2)
                k2 = sum(ok(A2[x]) for x in keys2); n2 = len(keys2); lo2, hi2 = wilson(k2, n2)
                m2, dlo2, dhi2, b2, c2, p2 = paired_d(A2, B2, keys2) if off else (0.0, 0.0, 0.0, 0, 0, 1.0)
                conf.append([comp, label, step, n_on(comp, off), names(off), "both", head[:3], n2, f3(k2 / n2), f3(lo2), f3(hi2), f3(m2), f3(dlo2), f3(dhi2), b2, c2, f"{p2:.3g}"])
                if head == "deterministic":
                    (ku, nu, ulo, uhi), (kl, nl, llo, lhi) = cells["canonical"], cells["randomStable"]
                    pareto.append([comp, label, n_on(comp, off), len(present(comp)), names(off), f3(ku / nu), f3(ulo), f3(uhi), f3(kl / nl), f3(llo), f3(lhi), f3(k2 / n2), f3(lo2), f3(hi2), f3(m2), f3(dlo2), f3(dhi2), f"{p2:.3g}"])
    write_csv("confirm.csv", ["composition", "point", "step", "n_on", "groups_off", "pose", "head", "n", "success", "lo", "hi", "d_vs_allon", "d_lo", "d_hi", "b", "c", "mcnemar_p"], conf)
    write_csv("pareto.csv", ["composition", "point", "n_on", "n_groups", "groups_off", "upright", "upright_lo", "upright_hi", "lying", "lying_lo", "lying_hi",
                             "both", "both_lo", "both_hi", "d_both_vs_allon", "d_lo", "d_hi", "mcnemar_p"], pareto)


if __name__ == "__main__":
    stages = sys.argv[1:] or ["A"]
    try:
        check_protected("start")
        for s in stages: {"A": stage_A, "B": stage_B, "C": stage_C, "D": stage_D}[s](); print(f"stage {s} complete", flush=True)
        check_protected("end")
    except StopRun as ex:
        print("STOP:", ex, flush=True)
        with open(os.path.join(REPO, OUT, "STOP.txt"), "a") as f: f.write(time.strftime("%F %T ") + str(ex) + "\n")
        sys.exit(2)
