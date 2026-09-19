"""BO campaign over the morphology theta with tools/bo_eval.evaluate as the objective (Part B).

Objective: pass x success at mu = 1.0, 20 episodes, seeds 4001-4020, sampled action head (deployment behaviour), one
player build for the whole campaign. Arms: 'bo' (reference anchor + 29 random feasible draws, then 70 BO iterations) and
'random' (100 random feasible draws). Oracle-infeasible candidates feed the feasibility model only. The reference theta is
re-evaluated after every 25 objective evaluations and must reproduce the anchor byte for byte. Confirmation: top 5 of
each arm on seeds 4021-4100 (80 episodes, mu 0.6 / 1.0 / 1.5). Everything is appended to records.jsonl so a run resumes.
"""
import hashlib
import json
import os
import random
import statistics
import time

import torch

from tools.bo_eval import evaluate, feasible
from .models import MixedGP, FeasibilityModel, expected_improvement
from .space import (to_vector, random_candidate, perturb, theta_key, describe, reference_theta, validate)

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(REPO, "results", "bo_optim")
PLAYER_DIR = os.path.join(REPO, "Builds", "BoEval")
OPT_SEEDS, CONFIRM_SEEDS = (4001, 4020), (4021, 4100)
N_OPT_EPISODES, N_CONFIRM_EPISODES = 20, 80
N_INIT, N_BO, N_RANDOM = 30, 70, 100
POOL_GLOBAL, POOL_LOCAL, LOCAL_TOP = 10000, 5000, 5
DRIFT_EVERY = 25
MIN_FEASIBILITY_RATE, MAX_EVAL_ERRORS = 0.20, 2
INIT_RNG_SEED, RANDOM_ARM_RNG_SEED, POOL_RNG_SEED = 20260919, 20260920, 20260921


class StopCampaign(Exception):
    pass


# ---------------------------------------------------------------- build hash / records
def build_hash():
    """sha256 over every file of the player build except runtime outputs (ML-Agents timers, logs)."""
    h = hashlib.sha256(); n = 0
    for root, dirs, files in os.walk(PLAYER_DIR):
        dirs[:] = sorted(d for d in dirs if d != "ML-Agents")
        for f in sorted(files):
            if f.endswith(".log"):
                continue
            p = os.path.join(root, f); rel = os.path.relpath(p, PLAYER_DIR).replace("\\", "/")
            h.update(rel.encode()); h.update(open(p, "rb").read()); n += 1
    return h.hexdigest(), n


class Records:
    def __init__(self, path):
        self.path = path; self.rows = []
        if os.path.isfile(path):
            with open(path) as f:
                self.rows = [json.loads(l) for l in f if l.strip()]

    def get(self, arm, idx):
        for r in self.rows:
            if r["arm"] == arm and r["idx"] == idx:
                return r
        return None

    def add(self, row):
        self.rows.append(row)
        with open(self.path, "a") as f:
            f.write(json.dumps(row) + "\n")

    def arm(self, arm):
        return [r for r in self.rows if r["arm"] == arm]


# ---------------------------------------------------------------- evaluation
class Campaign:
    def __init__(self, out=OUT):
        self.out = out; os.makedirs(out, exist_ok=True)
        self.records = Records(os.path.join(out, "records.jsonl"))
        self.log_path = os.path.join(out, "campaign.log")
        self.errors = 0; self.t_start = time.time()
        self.reference = reference_theta()
        self.pool_rng = random.Random(POOL_RNG_SEED)

    def log(self, msg):
        line = time.strftime("%H:%M:%S ") + msg
        print(line, flush=True)
        with open(self.log_path, "a") as f:
            f.write(line + "\n")

    def check_build(self):
        digest, n = build_hash()
        p = os.path.join(self.out, "build_hash.json")
        if os.path.isfile(p):
            prev = json.load(open(p))
            if prev["sha256"] != digest:
                raise StopCampaign(f"player build changed: recorded {prev['sha256'][:16]} vs now {digest[:16]}")
        else:
            json.dump({"sha256": digest, "files": n, "player_dir": PLAYER_DIR, "recorded": time.strftime("%Y-%m-%d %H:%M:%S")}, open(p, "w"), indent=1)
        self.log(f"player build sha256 {digest} ({n} files)")
        return digest

    def n_objective_evals(self):
        return sum(1 for r in self.records.rows if r["arm"] in ("bo", "random") and r.get("objective") is not None)

    def eval_candidate(self, arm, idx, theta, extra=None):
        """Oracle first; the objective only when the oracle says feasible. Returns the record (cached if already done)."""
        done = self.records.get(arm, idx)
        if done is not None and done.get("status") == "ok":
            return done
        theta = validate(theta); key = theta_key(theta)
        base = os.path.join(self.out, "evals", f"{arm}_{idx:03d}")
        row = {"arm": arm, "idx": idx, "key": key, "theta": theta, "desc": describe(theta), "status": "ok", "time": time.strftime("%Y-%m-%d %H:%M:%S")}
        if extra:
            row.update(extra)
        t0 = time.time()
        try:
            ok, contacts, oracle = feasible(theta, out_dir=os.path.join(base, "oracle"))
            row.update({"feasible": bool(ok), "oracle": oracle, "objective": None})
            if ok:
                r = evaluate(theta, n_episodes=N_OPT_EPISODES, mu_levels=(1.0,), seed_block=OPT_SEEDS, out_dir=os.path.join(base, "obj"), oracle=False)
                d = r["drop_pass"]["1.0"]
                row.update({"objective": d["times_success"]["mean"], "objective_ci": [d["times_success"]["lo"], d["times_success"]["hi"]],
                            "pass_given_hold": d["given_hold"]["mean"], "success_rate": r["success"]["mean"], "success_count": r["success"]["count"],
                            "palm_contact_rate": r["palm_contact_rate"], "mean_contacts": r["mean_contacts"], "mean_steps_to_hold": r["mean_steps_to_hold"],
                            "discarded": r["discarded"], "out_dir": r["out_dir"]})
        except Exception as e:
            self.errors += 1
            row.update({"status": "error", "error": str(e)})
            self.log(f"ERROR {arm} #{idx}: {e}")
            if self.errors > MAX_EVAL_ERRORS:
                self.records.add(row)
                raise StopCampaign(f"evaluate() failed on {self.errors} candidates")
        row["wall_s"] = time.time() - t0
        self.records.add(row)
        self.log(f"{arm} #{idx} key={key} feasible={row.get('feasible')} objective={row.get('objective')} palm={row.get('palm_contact_rate')} desc=len {row['desc']['lenMean']:.3f} act {row['desc']['active']} w {row['desc']['omegaActiveMean']:.1f} ({row['wall_s']:.0f}s)")
        self.maybe_drift_check()
        return row

    # ------------------------------------------------------------ drift control
    def anchor_dir(self):
        a = self.records.get("bo", 1)
        return a["out_dir"] if a and a.get("out_dir") else None

    def maybe_drift_check(self):
        n = self.n_objective_evals()
        if n == 0 or n % DRIFT_EVERY != 0:
            return
        k = n // DRIFT_EVERY
        if self.records.get("drift", k) is not None:
            return
        self.drift_check(k)

    def drift_check(self, k):
        anchor = self.anchor_dir()
        if anchor is None:
            raise StopCampaign("drift check requested before the anchor evaluation")
        out = os.path.join(self.out, "drift", f"drift_{k:02d}")
        r = evaluate(self.reference, n_episodes=N_OPT_EPISODES, mu_levels=(1.0,), seed_block=OPT_SEEDS, out_dir=out, oracle=False)
        same = {}
        for name in ("episodes.csv", "drops.csv"):
            same[name] = open(os.path.join(anchor, name), newline="").read() == open(os.path.join(out, name), newline="").read()
        row = {"arm": "drift", "idx": k, "after_objective_evals": k * DRIFT_EVERY, "objective": r["drop_pass"]["1.0"]["times_success"]["mean"], "identical": same, "out_dir": out, "status": "ok", "time": time.strftime("%Y-%m-%d %H:%M:%S")}
        self.records.add(row)
        self.log(f"drift check {k} after {k * DRIFT_EVERY} evaluations: identical={same} objective={row['objective']}")
        if not all(same.values()):
            raise StopCampaign(f"DRIFT: reference re-evaluation {k} differs from the anchor ({same}); the instrument has changed")

    # ------------------------------------------------------------ arms
    def feasible_rows(self, arm):
        return [r for r in self.records.arm(arm) if r.get("status") == "ok" and r.get("feasible") and r.get("objective") is not None]

    def run_bo_arm(self):
        rng = random.Random(INIT_RNG_SEED)
        init = [self.reference] + [random_candidate(rng) for _ in range(N_INIT - 1)]
        for i, th in enumerate(init, start=1):
            self.eval_candidate("bo", i, th, {"phase": "init"})
        rows = [r for r in self.records.arm("bo") if r.get("status") == "ok" and r.get("feasible") is not None]
        rate = sum(1 for r in rows if r["feasible"]) / max(1, len(rows))
        self.log(f"initial design feasibility rate {rate:.2f} ({len(rows)} candidates)")
        if rate < MIN_FEASIBILITY_RATE:
            raise StopCampaign(f"oracle feasibility rate {rate:.2f} < {MIN_FEASIBILITY_RATE} after the initial design; the space may be mis-specified")
        for i in range(N_INIT + 1, N_INIT + N_BO + 1):
            if self.records.get("bo", i) is not None and self.records.get("bo", i).get("status") == "ok":
                continue
            theta, info = self.propose()
            self.eval_candidate("bo", i, theta, {"phase": "bo", **info})

    def propose(self):
        obs = [r for r in self.records.arm("bo") if r.get("status") == "ok" and r.get("feasible") is not None]
        fx = self.feasible_rows("bo")
        X = [to_vector(r["theta"])[0] for r in fx]; M = [to_vector(r["theta"])[1] for r in fx]; y = [r["objective"] for r in fx]
        gp = MixedGP(X, M, y)
        fm = FeasibilityModel([to_vector(r["theta"])[0] for r in obs], [to_vector(r["theta"])[1] for r in obs], [r["feasible"] for r in obs])
        best = max(y)
        seen = {r["key"] for r in self.records.rows if "key" in r}
        pool = [random_candidate(self.pool_rng) for _ in range(POOL_GLOBAL)]
        top = sorted(fx, key=lambda r: -r["objective"])[:LOCAL_TOP]
        for _ in range(POOL_LOCAL):
            pool.append(perturb(top[self.pool_rng.randrange(len(top))]["theta"], self.pool_rng))
        origin = ["global"] * POOL_GLOBAL + ["local"] * POOL_LOCAL
        keys = [theta_key(t) for t in pool]
        keep = [i for i, k in enumerate(keys) if k not in seen]
        pool = [pool[i] for i in keep]; origin = [origin[i] for i in keep]
        PX = torch.tensor([to_vector(t)[0] for t in pool]); PM = torch.tensor([to_vector(t)[1] for t in pool], dtype=torch.float64)
        mean, std = gp.predict(PX, PM)
        pf = fm.prob(PX, PM)
        ei = expected_improvement(mean, std, best)
        acq = ei * pf
        j = int(torch.argmax(acq))
        info = {"acq": float(acq[j]), "ei": float(ei[j]), "p_feasible": float(pf[j]), "gp_mean": float(mean[j]), "gp_std": float(std[j]),
                "incumbent": best, "pool": len(pool), "origin": origin[j], "gp": gp.hyperparameters(), "n_gp": len(y), "n_feas_model": len(obs)}
        return pool[j], info

    def run_random_arm(self):
        rng = random.Random(RANDOM_ARM_RNG_SEED)
        cands = [random_candidate(rng) for _ in range(N_RANDOM)]
        for i, th in enumerate(cands, start=1):
            self.eval_candidate("random", i, th)

    def top5(self, arm):
        return sorted(self.feasible_rows(arm), key=lambda r: (-r["objective"], r["idx"]))[:5]

    def run_confirmation(self):
        for arm in ("bo", "random"):
            for rank, r in enumerate(self.top5(arm), start=1):
                idx = rank if arm == "bo" else 10 + rank
                if self.records.get("confirm", idx) is not None:
                    continue
                out = os.path.join(self.out, "confirm", f"{arm}_rank{rank}_{arm}{r['idx']:03d}")
                t0 = time.time()
                c = evaluate(r["theta"], n_episodes=N_CONFIRM_EPISODES, mu_levels=(0.6, 1.0, 1.5), seed_block=CONFIRM_SEEDS, out_dir=out, oracle=False)
                row = {"arm": "confirm", "idx": idx, "source_arm": arm, "rank": rank, "source_idx": r["idx"], "key": r["key"], "theta": r["theta"], "desc": r["desc"],
                       "opt_objective": r["objective"], "success_rate": c["success"]["mean"], "success_count": c["success"]["count"],
                       "drop_pass": c["drop_pass"], "palm_contact_rate": c["palm_contact_rate"], "mean_contacts": c["mean_contacts"], "mean_steps_to_hold": c["mean_steps_to_hold"],
                       "oracle": r.get("oracle"), "out_dir": c["out_dir"], "status": "ok", "wall_s": time.time() - t0, "time": time.strftime("%Y-%m-%d %H:%M:%S")}
                self.records.add(row)
                d = c["drop_pass"]["1.0"]["times_success"]
                self.log(f"confirm {arm} rank {rank} (#{r['idx']}): opt {r['objective']:.3f} -> confirmation {d['mean']:.3f} [{d['lo']:.3f}, {d['hi']:.3f}] success {c['success']['mean']:.2f} palm {c['palm_contact_rate']:.2f}")

    # ------------------------------------------------------------ full run
    def run_all(self):
        self.check_build()
        try:
            self.run_bo_arm()
            self.run_random_arm()
            self.run_confirmation()
        finally:
            self.write_summary()
        digest, _ = build_hash()
        self.log(f"campaign complete; build sha256 at end {digest} wall {time.time() - self.t_start:.0f}s")
        return digest

    def write_summary(self):
        s = {"n_bo": len(self.records.arm("bo")), "n_random": len(self.records.arm("random")), "n_drift": len(self.records.arm("drift")), "n_confirm": len(self.records.arm("confirm")),
             "errors": self.errors, "wall_s": time.time() - self.t_start, "written": time.strftime("%Y-%m-%d %H:%M:%S")}
        json.dump(s, open(os.path.join(self.out, "summary.json"), "w"), indent=1)
