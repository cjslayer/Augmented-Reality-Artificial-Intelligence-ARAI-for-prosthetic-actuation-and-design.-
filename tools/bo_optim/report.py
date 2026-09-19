"""REPORT.md, best-so-far CSV and SVG plot from results/bo_optim/records.jsonl (no plotting library in the venv)."""
import json
import os
import statistics

from .campaign import Records, OUT, OPT_SEEDS, CONFIRM_SEEDS, N_OPT_EPISODES, N_CONFIRM_EPISODES


def best_so_far(rows):
    best, out = None, []
    for r in sorted(rows, key=lambda r: r["idx"]):
        v = r.get("objective")
        if v is not None and (best is None or v > best):
            best = v
        out.append((r["idx"], v, best))
    return out


def svg_plot(bo, rnd, path):
    W, H, L, R, T, B = 720, 400, 60, 20, 20, 50
    n = max(len(bo), len(rnd), 1)
    def px(i): return L + (W - L - R) * (i - 1) / max(1, n - 1)
    def py(v): return T + (H - T - B) * (1 - v)
    def poly(series, color, dash=""):
        pts = " ".join(f"{px(i):.1f},{py(b):.1f}" for i, _, b in series if b is not None)
        return f'<polyline fill="none" stroke="{color}" stroke-width="2" {dash} points="{pts}"/>'
    def dots(series, color):
        return "".join(f'<circle cx="{px(i):.1f}" cy="{py(v):.1f}" r="2.2" fill="{color}" fill-opacity="0.45"/>' for i, v, _ in series if v is not None)
    s = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" font-family="sans-serif" font-size="12">',
         f'<rect width="{W}" height="{H}" fill="white"/>']
    for k in range(0, 11):
        v = k / 10; y = py(v)
        s.append(f'<line x1="{L}" y1="{y:.1f}" x2="{W - R}" y2="{y:.1f}" stroke="#e5e5e5"/>')
        s.append(f'<text x="{L - 8}" y="{y + 4:.1f}" text-anchor="end">{v:.1f}</text>')
    for i in (1, 25, 50, 75, 100):
        if i <= n:
            s.append(f'<line x1="{px(i):.1f}" y1="{T}" x2="{px(i):.1f}" y2="{H - B}" stroke="#eeeeee"/>')
            s.append(f'<text x="{px(i):.1f}" y="{H - B + 16}" text-anchor="middle">{i}</text>')
    s.append(f'<line x1="{L}" y1="{H - B}" x2="{W - R}" y2="{H - B}" stroke="#333"/><line x1="{L}" y1="{T}" x2="{L}" y2="{H - B}" stroke="#333"/>')
    s.append(dots(bo, "#1f5fbf") + dots(rnd, "#c4522a"))
    s.append(poly(bo, "#1f5fbf") + poly(rnd, "#c4522a", 'stroke-dasharray="6,4"'))
    s.append(f'<text x="{(L + W - R) / 2:.0f}" y="{H - 8}" text-anchor="middle">candidate index (same seeds 4001-4020 for every candidate)</text>')
    s.append(f'<text x="14" y="{(T + H - B) / 2:.0f}" text-anchor="middle" transform="rotate(-90 14 {(T + H - B) / 2:.0f})">best-so-far pass x success at mu = 1.0</text>')
    s.append(f'<rect x="{L + 10}" y="{T + 8}" width="14" height="3" fill="#1f5fbf"/><text x="{L + 30}" y="{T + 13}">BO (anchor + 29 random, then 70 EI x P(feasible))</text>')
    s.append(f'<rect x="{L + 10}" y="{T + 26}" width="14" height="3" fill="#c4522a"/><text x="{L + 30}" y="{T + 31}">random baseline (100 draws); dots = individual candidates</text>')
    s.append("</svg>")
    open(path, "w").write("\n".join(s))


def fmt_theta(d):
    return (f"scales mean {d['lenMean']:.3f} (min {d['lenMin']:.2f}, max {d['lenMax']:.2f}, thumb {d['lenThumb']:.2f}); "
            f"{d['active']} active, mask {d['mask']}; active-group omega {d['omegaActiveMean']:.1f} (min {d['omegaActiveMin']:.1f}), zeta {d['zetaActiveMean']:.2f}, I-scale {d['inertiaActiveMean']:.2f}; wrist omega {d['omegaWrist']:.1f}")


def mean_of(rows, key, sub=None):
    vals = [(r[key][sub] if sub else r[key]) for r in rows if r.get(key) is not None]
    return statistics.mean(vals) if vals else float("nan")


def write_report(out=OUT):
    rec = Records(os.path.join(out, "records.jsonl"))
    bo, rnd, drift, conf = rec.arm("bo"), rec.arm("random"), rec.arm("drift"), rec.arm("confirm")
    bh = json.load(open(os.path.join(out, "build_hash.json")))
    summary = json.load(open(os.path.join(out, "summary.json"))) if os.path.isfile(os.path.join(out, "summary.json")) else {}
    wall_records = sum(r.get("wall_s", 0) for r in rec.rows)
    bsf_bo, bsf_rnd = best_so_far(bo), best_so_far(rnd)
    with open(os.path.join(out, "best_so_far.csv"), "w") as f:
        f.write("idx,bo_objective,bo_best,random_objective,random_best\n")
        for i in range(max(len(bsf_bo), len(bsf_rnd))):
            a = bsf_bo[i] if i < len(bsf_bo) else (i + 1, None, None); b = bsf_rnd[i] if i < len(bsf_rnd) else (i + 1, None, None)
            f.write(f"{i + 1},{'' if a[1] is None else a[1]},{'' if a[2] is None else a[2]},{'' if b[1] is None else b[1]},{'' if b[2] is None else b[2]}\n")
    svg_plot(bsf_bo, bsf_rnd, os.path.join(out, "best_so_far.svg"))
    with open(os.path.join(out, "candidates.csv"), "w") as f:
        f.write("arm,idx,phase,key,feasible,objective,pass_given_hold,success_rate,palm_contact_rate,mean_contacts,lenMean,lenThumb,active,mask,omegaActiveMean,zetaActiveMean,inertiaActiveMean,oracle_gate_step,oracle_max_contacts,oracle_push_mm,acq,ei,p_feasible,origin\n")
        for r in sorted(bo + rnd, key=lambda r: (r["arm"], r["idx"])):
            d, o = r["desc"], r.get("oracle") or {}
            f.write(",".join(str(v) for v in (r["arm"], r["idx"], r.get("phase", ""), r["key"], r.get("feasible"), r.get("objective"), r.get("pass_given_hold"), r.get("success_rate"), r.get("palm_contact_rate"), r.get("mean_contacts"),
                    f"{d['lenMean']:.4f}", f"{d['lenThumb']:.3f}", d["active"], d["mask"], f"{d['omegaActiveMean']:.2f}", f"{d['zetaActiveMean']:.3f}", f"{d['inertiaActiveMean']:.3f}", o.get("firstGateStep"), o.get("maxContacts"), o.get("pushOutMm"), r.get("acq"), r.get("ei"), r.get("p_feasible"), r.get("origin", ""))) + "\n")

    def feas_stats(rows):
        ok = [r for r in rows if r.get("feasible")]; bad = [r for r in rows if r.get("feasible") is False]
        return ok, bad

    md = ["# Bayesian morphology optimization (Part B): campaign report", "",
          f"Generated {summary.get('written', '')}. Objective = pass x success at mu = 1.0 from `tools/bo_eval.evaluate` on {N_OPT_EPISODES} episodes, seeds {OPT_SEEDS[0]}-{OPT_SEEDS[1]}, sampled action head (deployment behaviour), one player build for everything below.", "",
          f"- Player build sha256 `{bh['sha256']}` ({bh['files']} files under `Builds/BoEval/`, recorded {bh['recorded']}); the drift checks below confirm it did not change.",
          f"- Wall time: {wall_records / 60:.1f} min in player runs (sum over evaluations); campaign process {summary.get('wall_s', 0) / 60:.1f} min including model fits.",
          f"- Candidates: BO arm {len(bo)} (reference anchor + 29 random, then {len([r for r in bo if r.get('phase') == 'bo'])} BO iterations), random baseline {len(rnd)}; confirmation runs {len(conf)}; drift checks {len(drift)}.", ""]

    # drift
    md += ["## Drift control", "", "| check | after objective evaluations | episodes.csv identical | drops.csv identical | objective |", "|---|---|---|---|---|"]
    for r in drift:
        md.append(f"| {r['idx']} | {r['after_objective_evals']} | {r['identical']['episodes.csv']} | {r['identical']['drops.csv']} | {r['objective']:.3f} |")
    anchor = rec.get("bo", 1)
    md += [f"\nAnchor (reference theta, BO #1): objective {anchor['objective']:.3f}, success {anchor['success_count']}/{N_OPT_EPISODES}, pass given hold {anchor['pass_given_hold']:.3f}, palm contact {anchor['palm_contact_rate']:.2f}." if anchor and anchor.get("objective") is not None else "", ""]

    # best so far
    fb = [r for r in bo if r.get("objective") is not None]; fr = [r for r in rnd if r.get("objective") is not None]
    md += ["## Best-so-far, BO vs random baseline (optimization block: selection-biased)", "",
           "![best so far](best_so_far.svg)", "", "`best_so_far.csv`, `candidates.csv`. Numbers in this section are 20-episode estimates on the seeds used for selection and are biased upward for the winners; the confirmation section carries the headline numbers.", "",
           "| arm | candidates with an objective | best | mean | median | >= anchor |", "|---|---|---|---|---|---|"]
    for name, rows in (("BO", fb), ("random", fr)):
        vals = [r["objective"] for r in rows]
        if vals:
            md.append(f"| {name} | {len(vals)} | {max(vals):.3f} | {statistics.mean(vals):.3f} | {statistics.median(vals):.3f} | {sum(1 for v in vals if anchor and v >= anchor['objective'])} |")
    for i in (30, 50, 75, 100):
        a = next((b for k, _, b in bsf_bo if k == i), None); b = next((bb for k, _, bb in bsf_rnd if k == i), None)
        md.append(f"\nbest-so-far after {i} candidates: BO {a if a is None else f'{a:.3f}'}, random {b if b is None else f'{b:.3f}'}")
    md.append("")

    # confirmation
    md += ["## Confirmation (headline numbers): top 5 of each arm on seeds "
           f"{CONFIRM_SEEDS[0]}-{CONFIRM_SEEDS[1]}, {N_CONFIRM_EPISODES} fresh episodes, mu 0.6 / 1.0 / 1.5", "",
           "Selection used seeds 4001-4020 only; these seeds were never used for selection.", "",
           "| arm | rank | cand. | theta | opt-block score (biased) | **confirmation pass x success, mu 1.0 [95% CI]** | conf. mu 0.6 / 1.5 | conf. success | conf. pass given hold mu 1.0 | palm contact | contacts | oracle (gate step / contacts / push-out) |",
           "|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for r in sorted(conf, key=lambda r: r["idx"]):
        d10 = r["drop_pass"]["1.0"]; o = r.get("oracle") or {}
        md.append(f"| {r['source_arm']} | {r['rank']} | #{r['source_idx']} | {fmt_theta(r['desc'])} | {r['opt_objective']:.3f} | **{d10['times_success']['mean']:.3f} [{d10['times_success']['lo']:.3f}, {d10['times_success']['hi']:.3f}]** | "
                  f"{r['drop_pass']['0.6']['times_success']['mean']:.3f} / {r['drop_pass']['1.5']['times_success']['mean']:.3f} | {r['success_count']}/{N_CONFIRM_EPISODES} | {d10['given_hold']['mean']:.3f} | {r['palm_contact_rate']:.2f} | {r['mean_contacts']:.1f} | {o.get('firstGateStep')} / {o.get('maxContacts')} / {o.get('pushOutMm', float('nan')):.0f} mm |")
    ref_conf = [r for r in conf if r["desc"]["mask"] == "1" * 14 and abs(r["desc"]["lenMean"] - 1.0) < 1e-9 and abs(r["desc"]["omegaActiveMean"] - 25.0) < 1e-9]
    md.append("")
    if ref_conf:
        md.append(f"The reference hand is among the confirmed candidates (row {ref_conf[0]['source_arm']} rank {ref_conf[0]['rank']}).")
    md.append("")

    # feasibility
    md += ["## Oracle feasibility", ""]
    for name, rows in (("BO arm", bo), ("random arm", rnd), ("all", bo + rnd)):
        ok, bad = feas_stats(rows)
        n = len(ok) + len(bad)
        md.append(f"- {name}: {len(ok)}/{n} feasible ({len(ok) / max(1, n):.2f}).")
    ok, bad = feas_stats(bo + rnd)
    if bad:
        md += ["", "| group | n | mean active groups | thumb groups active (mean) | mean scale | thumb scale | index scale | active-group omega | oracle max contacts |", "|---|---|---|---|---|---|---|---|---|"]
        for name, rows in (("feasible", ok), ("infeasible", bad)):
            md.append(f"| {name} | {len(rows)} | {mean_of(rows, 'desc', 'active'):.2f} | {mean_of(rows, 'desc', 'thumbGroups'):.2f} | {mean_of(rows, 'desc', 'lenMean'):.3f} | {mean_of(rows, 'desc', 'lenThumb'):.3f} | {mean_of(rows, 'desc', 'lenIndex'):.3f} | {mean_of(rows, 'desc', 'omegaActiveMean'):.1f} | {statistics.mean(r['oracle']['maxContacts'] for r in rows if r.get('oracle')):.1f} |")
        by_active = {}
        for r in bo + rnd:
            if r.get("feasible") is None: continue
            by_active.setdefault(r["desc"]["active"], []).append(r["feasible"])
        md += ["", "Feasibility rate by active-group count: " + ", ".join(f"{k}: {sum(v)}/{len(v)}" for k, v in sorted(by_active.items()))]
        masked = {}
        for r in bad:
            for g, bit in enumerate(r["desc"]["mask"]):
                if bit == "0": masked[g] = masked.get(g, 0) + 1
        names = ["indexBase", "indexMiddle", "indexEnd", "middleBase", "middleMiddle", "middleEnd", "ringBase", "ringMiddle", "ringEnd", "pinkyBase", "pinkyMiddle", "pinkyEnd", "thumbBase", "thumbEnd"]
        md.append("Groups most often masked among infeasible candidates: " + ", ".join(f"{names[g]} ({c}/{len(bad)})" for g, c in sorted(masked.items(), key=lambda kv: -kv[1])[:5]))
    md.append("")

    # observations (computed patterns)
    md += ["## Patterns in the evaluated candidates (descriptive)", ""]
    top10 = sorted(fb + fr, key=lambda r: -r["objective"])[:10]; bottom = sorted(fb + fr, key=lambda r: r["objective"])[:10]
    allf = fb + fr
    md += ["| group | n | objective | mean scale | thumb scale | index scale | active groups | active-group omega | zeta | I-scale | palm contact | contacts |", "|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for name, rows in (("top 10 (both arms, opt block)", top10), ("bottom 10", bottom), ("all with an objective", allf)):
        md.append(f"| {name} | {len(rows)} | {mean_of(rows, 'objective'):.3f} | {mean_of(rows, 'desc', 'lenMean'):.3f} | {mean_of(rows, 'desc', 'lenThumb'):.3f} | {mean_of(rows, 'desc', 'lenIndex'):.3f} | {mean_of(rows, 'desc', 'active'):.2f} | {mean_of(rows, 'desc', 'omegaActiveMean'):.1f} | {mean_of(rows, 'desc', 'zetaActiveMean'):.2f} | {mean_of(rows, 'desc', 'inertiaActiveMean'):.2f} | {mean_of(rows, 'palm_contact_rate'):.2f} | {mean_of(rows, 'mean_contacts'):.2f} |")
    last_gp = next((r["gp"] for r in sorted(bo, key=lambda r: -r["idx"]) if r.get("gp")), None)
    if last_gp:
        md += ["", "Final GP hyperparameters (normalized [0, 1] units per block; a small lengthscale means the objective varies quickly along that block): "
               + ", ".join(f"{k} {v:.2f}" for k, v in last_gp["lengthscales"].items()) + f", mask {last_gp['mask_lengthscale']:.2f} (Hamming units), outputscale {last_gp['outputscale']:.2f}, noise {last_gp['noise']:.3f} (standardized)."]
    bo_iters = [r for r in bo if r.get("phase") == "bo" and r.get("origin")]
    if bo_iters:
        md.append(f"\nBO proposals came from the local pool (perturbations of the top-{5} incumbents) in {sum(1 for r in bo_iters if r['origin'] == 'local')}/{len(bo_iters)} iterations, from the global pool otherwise; mean P(feasible) of the proposals {mean_of(bo_iters, 'p_feasible'):.2f}.")
    obs_path = os.path.join(out, "OBSERVATIONS.md")
    md += ["", open(obs_path, encoding="utf-8").read() if os.path.isfile(obs_path) else "OBSERVATIONS_PLACEHOLDER", ""]
    open(os.path.join(out, "REPORT.md"), "w", encoding="utf-8").write("\n".join(md))
    return os.path.join(out, "REPORT.md")
