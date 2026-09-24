"""Run one ArticulatedGates pass in the open Unity Editor (the mechanics of the session runner run_gates.sh, in Python).

The Editor must be open on Dynamic_Scene and stopped. The config is written to Temp/gates.json (ArticulatedGates.Boot
attaches the harness at scene load when that file exists), Play mode is entered through the Unity CLI, the CSV is
polled for its DONE footer (the harness stops Play itself in Finish), and the config file is removed afterwards.
"""
import json, os, subprocess, time, hashlib, csv

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


class EditorError(RuntimeError):
    pass


def _cli(*args, timeout=120):
    p = subprocess.run(["unity", "command", *args, "--no-banner", "--format", "json"], capture_output=True, text=True, timeout=timeout, cwd=REPO)
    try:
        return json.loads(p.stdout)
    except Exception:
        return {"success": False, "raw": p.stdout[-400:], "err": p.stderr[-400:]}


def play_mode():
    d = _cli("editor_status")
    try:
        return ((d.get("data") or {}).get("result") or {}).get("playMode") or "unreachable"
    except Exception:
        return "unreachable"


def console(tail=300, pattern=None):
    d = _cli("console", "--tail", str(tail)); out = []
    for e in (((d.get("data") or {}).get("result") or {}).get("entries") or []):
        m = str(e.get("message", ""))
        if pattern is None or pattern in m: out.append((e.get("level"), m))
    return out


def run_pass(cfg, max_wait=2400, poll=5):
    """cfg: the ArticulatedGates config dict (must contain 'csv'). Returns (csv_path, wall_seconds). Raises EditorError."""
    csv_path = os.path.join(REPO, cfg["csv"]); os.makedirs(os.path.dirname(csv_path), exist_ok=True)
    st = play_mode()
    if st == "unreachable": raise EditorError("Editor unreachable")
    if st != "stopped": _cli("editor_stop"); time.sleep(5)
    if os.path.exists(csv_path): os.remove(csv_path)
    os.makedirs(os.path.join(REPO, "Temp"), exist_ok=True)
    with open(os.path.join(REPO, "Temp", "gates.json"), "w") as f: json.dump(cfg, f)
    _cli("clear_console"); t0 = time.time(); d = _cli("editor_play")
    if not d.get("success"): os.remove(os.path.join(REPO, "Temp", "gates.json")); raise EditorError("editor_play failed: " + str(d)[:300])
    rc = None; t = 0
    try:
        while t < max_wait:
            time.sleep(poll); t += poll
            if os.path.exists(csv_path):
                with open(csv_path, "rb") as f: tail = f.read()[-16:]
                if b"DONE" in tail: rc = 0; break
            if t % 30 == 0:
                st = play_mode()
                if st == "unreachable": rc = 3; break
                if st == "stopped" and t > 15: rc = 4; break
        if rc is None: _cli("editor_stop"); rc = 5
    finally:
        try: os.remove(os.path.join(REPO, "Temp", "gates.json"))
        except OSError: pass
    wall = time.time() - t0
    if rc != 0:
        errs = [m for lvl, m in console(200) if lvl in ("Error", "Exception")][:3]
        raise EditorError(f"pass did not complete (rc={rc}, {wall:.0f} s): {errs}")
    time.sleep(2)
    return csv_path, wall


def pass_hash(csv_path):
    """sha256 of the data rows (header and DONE footer excluded), hex prefix"""
    h = hashlib.sha256()
    with open(csv_path, "rb") as f:
        for line in f.read().splitlines()[1:]:
            if line.strip() and line.strip() != b"DONE": h.update(line.strip() + b"\n")
    return h.hexdigest()[:16]


def load_rows(csv_path):
    return [r for r in csv.DictReader(open(csv_path, newline="")) if (r.get("seed") or r.get("trial") or "").strip().isdigit()]
