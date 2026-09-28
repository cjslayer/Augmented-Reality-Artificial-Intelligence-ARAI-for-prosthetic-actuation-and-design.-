"""Run 013 training control: launch a phase, monitor from TensorBoard, switch phases by the documented within-lineage resume.

  python -m tools.eval.train_run013 launch --phase A            fresh run id 013 (refuses if results/013 exists)
  python -m tools.eval.train_run013 launch --phase B|C          within-lineage --resume of run id 013 (after `switch`)
  python -m tools.eval.train_run013 monitor [--watch]           one row per 500k-step boundary -> results/013/monitor.csv; --watch prints new
                                                                rows, lesson changes, stop-condition flags and SWITCH READY lines as they appear
  python -m tools.eval.train_run013 switch --to B|C             hard stop + status file + checkpoint.pt, then `launch --phase B|C`
  python -m tools.eval.train_run013 stop                        hard stop of the trainer tree (python + Run013 players), verified
  python -m tools.eval.train_run013 procs                       list trainer / player / python processes

Phase switch rule (brief 1b): A -> B when hold/decisions lesson 3 (Hold50) has been entered AND >= 250k steps have elapsed in it; B -> C when
pose/canonicalFraction lesson 2 (Canon0) has been entered AND >= 250k steps have elapsed. Resume point: the latest Prosthetic-N.pt with
N >= the entry step; checkpoint.pt := that file; run_logs/training_status.json written by hand (the trainer writes it only at a graceful exit;
MLAGENTS_UPGRADE.md, run 011b) with the next phase's lesson numbers (fixed parameters at lesson 0) and the checkpoint list pruned to <= N.
Every switch is appended to results/013/transitions.json.
"""
import argparse, csv, glob, json, math, os, shutil, subprocess, sys, time
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RUN = os.path.join(REPO, "results", "013"); ART = os.path.join(RUN, "Prosthetic"); TRANS = os.path.join(RUN, "transitions.json")
LEARN = r"C:\Users\chris\ml-agents\venv\Scripts\mlagents-learn.exe"
YAML = {"A": "Config/run_013_phaseA.yaml", "B": "Config/run_013_phaseB.yaml", "C": "Config/run_013_phaseC.yaml"}
LESSON_VALUES = {"hold/decisions": [5, 10, 20, 50], "pose/canonicalFraction": [1.0, 0.5, 0.0], "perturb/scale": [0.25, 0.5, 1.0]}
PARAMS = ("morph/randomize", "object/mode", "pose/canonicalFraction", "perturb/scale", "hold/decisions")
NEXT_STATUS = {"B": {"morph/randomize": 0, "object/mode": 0, "perturb/scale": 0, "hold/decisions": 0, "pose/canonicalFraction": 0},
               "C": {"morph/randomize": 0, "object/mode": 0, "pose/canonicalFraction": 0, "hold/decisions": 0, "perturb/scale": 0}}
SWITCH_PARAM = {"B": ("hold/decisions", 3), "C": ("pose/canonicalFraction", 2)}


def transitions():
    return json.load(open(TRANS)) if os.path.exists(TRANS) else []


def phase_at(step, tr):
    ph = "A"
    for t in tr:
        if step > t["resume_step"]: ph = t["to"]
    return ph


# ------------------------------------------------------------------ TensorBoard
def scalars():
    from tensorboard.backend.event_processing.event_accumulator import EventAccumulator
    out = {}
    for f in sorted(glob.glob(os.path.join(ART, "events.out.tfevents.*"))):
        ea = EventAccumulator(f, size_guidance={"scalars": 0}); ea.Reload()
        for tag in ea.Tags()["scalars"]:
            for e in ea.Scalars(tag): out.setdefault(tag, {})[e.step] = (e.value, e.wall_time)   # a resumed segment overwrites the stopped one's steps
    return out


def lesson_series(S):
    """per summary step: phase and the active lesson value of each curriculum parameter (phase-aware: a fixed parameter reports lesson 0)"""
    tr = transitions(); steps = sorted(S.get("Environment/Cumulative Reward", {}))
    rows = []
    for s in steps:
        ph = phase_at(s, tr); L = lambda p: int(S.get(f"Environment/Lesson Number/{p}", {}).get(s, (0, 0))[0])
        hold = LESSON_VALUES["hold/decisions"][min(L("hold/decisions"), 3)] if ph == "A" else 50
        frac = 1.0 if ph == "A" else (LESSON_VALUES["pose/canonicalFraction"][min(L("pose/canonicalFraction"), 2)] if ph == "B" else 0.0)
        pert = 0.0 if ph in "AB" else LESSON_VALUES["perturb/scale"][min(L("perturb/scale"), 2)]
        rows.append((s, ph, hold, frac, pert, L("hold/decisions"), L("pose/canonicalFraction"), L("perturb/scale")))
    return rows


def window(S, tag, s0, s1):
    v = [x[0] for st, x in S.get(tag, {}).items() if s0 < st <= s1]; return (sum(v) / len(v)) if v else float("nan")


FIELDS = ["time", "step", "phase", "hold", "canonicalFraction", "perturb", "reward", "success", "successCanonical", "successLying", "gate", "lift", "gateCanonical", "gateLying", "liftCanonical", "liftLying",
          "canonicalShare", "episodeLength", "entropy", "sps_window", "sps_500k", "nan"]


def monitor_rows(S, include_current=False):
    les = lesson_series(S)
    if not les: return []
    wall = {s: v[1] for s, v in S.get("Environment/Cumulative Reward", {}).items()}
    rows, last = [], 0
    for s, ph, hold, frac, pert, *_ in les:
        k = s // 500000
        if k > last or (include_current and s == les[-1][0]):
            if k > last: last = k
            prev = [t for t in wall if t <= s - 500000]; w0 = max(prev) if prev else min(wall)
            near = [t for t in wall if t <= s - 50000]; n0 = max(near) if near else min(wall)
            sps500 = (s - w0) / max(1e-6, wall[s] - wall[w0]) if s > w0 else float("nan"); spsw = (s - n0) / max(1e-6, wall[s] - wall[n0]) if s > n0 else float("nan")
            nan = any(math.isnan(v[0]) or math.isinf(v[0]) for tag in ("Policy/Entropy", "Losses/Value Loss", "Losses/Policy Loss", "Environment/Cumulative Reward") for st, v in S.get(tag, {}).items() if st <= s)
            w = lambda tag: window(S, tag, s - 50000, s)
            rows.append({"time": time.strftime("%H:%M", time.localtime(wall[s])), "step": s, "phase": ph, "hold": hold, "canonicalFraction": frac, "perturb": pert, "reward": w("Environment/Cumulative Reward"),
                         "success": w("Grasp/Success"), "successCanonical": w("Success/Canonical"), "successLying": w("Success/Lying"), "gate": w("Task/Transitioned"), "lift": w("Task/Lifted"),
                         "gateCanonical": w("Gate/Canonical"), "gateLying": w("Gate/Lying"), "liftCanonical": w("Lift/Canonical"), "liftLying": w("Lift/Lying"), "canonicalShare": w("Pose/CanonicalShare"),
                         "episodeLength": w("Environment/Episode Length"), "entropy": w("Policy/Entropy"), "sps_window": spsw, "sps_500k": sps500, "nan": int(nan)})
    return rows


def lesson_entries(S):
    """first summary step at which each (phase, parameter, lesson) appeared"""
    ent = {}
    for s, ph, hold, frac, pert, lh, lp, lq in lesson_series(S):
        for p, l in (("hold/decisions", lh), ("pose/canonicalFraction", lp), ("perturb/scale", lq)):
            ent.setdefault((ph, p, l), s)
    return ent


def switch_ready(S, to):
    p, l = SWITCH_PARAM[to]; frm = {"B": "A", "C": "B"}[to]; ent = lesson_entries(S).get((frm, p, l))
    last = max(S.get("Environment/Cumulative Reward", {0: 0}))
    return (ent is not None and last - ent >= 250000), ent, last


def fmt_row(r):
    f = lambda x, n=3: "nan" if isinstance(x, float) and math.isnan(x) else (f"{x:.{n}f}" if isinstance(x, float) else str(x))
    return (f"{r['time']} step={r['step']} phase={r['phase']} K={r['hold']} canonFrac={r['canonicalFraction']} perturb={r['perturb']} reward={f(r['reward'])} success={f(r['success'])} "
            f"S/canon={f(r['successCanonical'])} S/lying={f(r['successLying'])} gate={f(r['gate'])} lift={f(r['lift'])} canonShare={f(r['canonicalShare'], 2)} epLen={f(r['episodeLength'], 0)} "
            f"entropy={f(r['entropy'])} sps50k={f(r['sps_window'], 0)} sps500k={f(r['sps_500k'], 0)} nan={r['nan']}")


def write_monitor(rows):
    with open(os.path.join(RUN, "monitor.csv"), "w", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=FIELDS); w.writeheader(); [w.writerow(r) for r in rows]


# ------------------------------------------------------------------ processes
def procs():
    ps = subprocess.run(["powershell", "-NoProfile", "-Command", "Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'python|Prosthetic|mlagents' } | ForEach-Object { '{0}|{1}|{2}' -f $_.ProcessId, $_.Name, $_.CommandLine }"], capture_output=True, text=True).stdout
    out = []
    for line in ps.splitlines():
        parts = line.split("|", 2)
        if len(parts) == 3: out.append((int(parts[0]), parts[1], parts[2]))
    return out


def trainer_tree():
    return [p for p in procs() if ("mlagents" in p[2].lower() or "run_013" in p[2]) or ("Run013" in p[2] and "Prosthetic" in p[1])]


def hard_stop():
    for _ in range(3):
        tt = trainer_tree()
        if not tt: break
        for pid, name, _ in tt: subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True)
        time.sleep(5)
    left = trainer_tree(); print("hard stop: remaining trainer / Run013 player processes:", left if left else "none"); return not left


# ------------------------------------------------------------------ launch / switch
def launch(phase):
    if phase == "A" and os.path.exists(RUN): sys.exit("results/013 exists: phase A is a fresh lineage; refusing to overwrite")
    if phase in "BC" and not os.path.exists(os.path.join(ART, "checkpoint.pt")): sys.exit("no checkpoint.pt to resume from")
    if trainer_tree(): sys.exit("a trainer / player tree is already running: " + str(trainer_tree()))
    os.makedirs(RUN, exist_ok=True)
    cmd = [LEARN, YAML[phase], "--run-id=013", "--results-dir=results"] + (["--resume"] if phase in "BC" else [])
    log = open(os.path.join(RUN, f"trainer_phase{phase}.log"), "a")
    log.write(f"=== {time.strftime('%Y-%m-%d %H:%M:%S')} launch phase {phase}: {' '.join(cmd)}\n"); log.flush()
    p = subprocess.Popen(cmd, cwd=REPO, stdout=log, stderr=subprocess.STDOUT, creationflags=0x00000200)   # CREATE_NEW_PROCESS_GROUP
    print("launched phase", phase, "pid", p.pid, "log", log.name)


def ckpt_steps():
    return sorted(int(os.path.basename(f)[len("Prosthetic-"):-3]) for f in glob.glob(os.path.join(ART, "Prosthetic-*.pt")))


def switch(to):
    S = scalars(); ok, ent, last = switch_ready(S, to)
    if ent is None: sys.exit(f"switch to {to}: the lesson has not been entered")
    if not ok: sys.exit(f"switch to {to}: entered at {ent}, only {last - ent} steps elapsed (< 250k)")
    if not hard_stop(): sys.exit("hard stop failed; processes remain")
    after = [c for c in ckpt_steps() if c >= ent]
    if not after: sys.exit(f"no checkpoint after the lesson entry {ent}: cannot switch at lesson entry (stop condition)")
    n = after[-1]; pt = os.path.join(ART, f"Prosthetic-{n}.pt")
    shutil.copyfile(pt, os.path.join(ART, "checkpoint.pt"))
    sp = os.path.join(RUN, "run_logs", "training_status.json"); st = json.load(open(sp)) if os.path.exists(sp) else {}
    if os.path.exists(sp): shutil.copyfile(sp, sp + f".before_switch_{to}")
    ck = []
    for c in ckpt_steps():
        if c > n: continue
        onnx = os.path.join("results", "013", "Prosthetic", f"Prosthetic-{c}.onnx")
        ck.append({"steps": c, "file_path": onnx, "reward": None, "creation_time": os.path.getmtime(os.path.join(REPO, onnx)) if os.path.exists(os.path.join(REPO, onnx)) else time.time(),
                   "auxillary_file_paths": [os.path.join("results", "013", "Prosthetic", f"Prosthetic-{c}.pt")]})
    st["Prosthetic"] = {"checkpoints": ck, "final_checkpoint": None} if "Prosthetic" not in st else {**st["Prosthetic"], "checkpoints": ck}
    for p, l in NEXT_STATUS[to].items(): st[p] = {"lesson_num": l}
    st.setdefault("metadata", {"stats_format_version": "0.3.0", "mlagents_version": "1.2.0.dev0", "torch_version": "2.8.0+cpu"})
    os.makedirs(os.path.dirname(sp), exist_ok=True); json.dump(st, open(sp, "w"), indent=4)
    tr = transitions(); tr.append({"to": to, "entry_step": ent, "last_logged_step_at_stop": last, "resume_step": n, "checkpoint": os.path.basename(pt), "time": time.strftime("%Y-%m-%d %H:%M:%S"),
                                   "status_lessons": NEXT_STATUS[to], "checkpoint_sha256": __import__("hashlib").sha256(open(pt, "rb").read()).hexdigest()[:16]})
    json.dump(tr, open(TRANS, "w"), indent=1)
    print(f"switch to {to}: entry {ent}, stopped at logged step {last}, resume from {os.path.basename(pt)}; status lessons {NEXT_STATUS[to]}")
    launch(to)


def watch(interval=60):
    """print each new 500k boundary row once, lesson entries, SWITCH READY, NaN and missing-process notes; rewrite monitor.csv every poll"""
    S0 = scalars() if os.path.exists(ART) else {}; seen = len(monitor_rows(S0)); lessons_seen = set(lesson_entries(S0)) if S0 else set(); ready_flag = set(); noproc = 0
    while True:
        try:
            S = scalars(); rows = monitor_rows(S)
            if S: write_monitor(monitor_rows(S, include_current=True))
            for r in rows[seen:]: print("ROW " + fmt_row(r), flush=True)
            seen = max(seen, len(rows))
            for k, st in sorted(lesson_entries(S).items(), key=lambda kv: kv[1]) if S else []:
                if k not in lessons_seen: lessons_seen.add(k); print(f"LESSON phase {k[0]} {k[1]} lesson {k[2]} entered at step {st}", flush=True)
            for to in "BC":
                if not S: break
                ok, ent, last = switch_ready(S, to)
                if ok and phase_at(last, transitions()) == {"B": "A", "C": "B"}[to] and to not in ready_flag: ready_flag.add(to); print(f"SWITCH READY to {to}: entry {ent}, now {last}", flush=True)
            if rows and rows[-1]["nan"]: print("STOP NaN detected", flush=True)
            if not trainer_tree():
                noproc += 1
                if noproc == 2: print("NOTE no trainer / player process running", flush=True)
            else: noproc = 0
        except Exception as ex:
            print("WATCH error", repr(ex)[:200], flush=True)
        time.sleep(interval)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("cmd"); ap.add_argument("--phase"); ap.add_argument("--to"); ap.add_argument("--watch", action="store_true"); a = ap.parse_args()
    if a.cmd == "launch": launch(a.phase)
    elif a.cmd == "switch": switch(a.to)
    elif a.cmd == "stop": hard_stop()
    elif a.cmd == "procs": [print(p) for p in procs()]
    elif a.cmd == "monitor":
        if a.watch: watch()
        else:
            rows = monitor_rows(scalars(), include_current=True); write_monitor(rows); [print(fmt_row(r)) for r in rows]
