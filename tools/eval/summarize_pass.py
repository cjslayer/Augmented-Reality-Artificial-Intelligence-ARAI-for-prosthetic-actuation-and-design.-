"""Summarize eval-harness passes (ArticulatedGates mode "eval" CSVs) with Wilson intervals and zero-step exclusion.

usage: python tools/eval/summarize_pass.py [--out SUMMARY.csv] PASS.csv [PASS.csv ...]

Prints one markdown table row per pass and optionally writes a summary CSV. Rows flagged maxStepTimeout = 1 (ML-Agents
MaxStep interruptions, whose agent record reads steps 0; in files written before the flag existed: endReason maxStep with
steps 0) stay in the pass CSV but are excluded from every aggregate (n, success, lifted, palm, contacts, hold); the count
per pass is reported as excluded_zero_step. Success carries a 95 % Wilson interval.
"""
import argparse, csv, math, os, statistics as st


def load(path):
    return [r for r in csv.DictReader(open(path, newline="")) if (r.get("seed") or "").isdigit()]


def wilson(k, n, z=1.96):
    if n == 0:
        return float("nan"), float("nan"), float("nan")
    p = k / n; den = 1 + z * z / n; c = (p + z * z / (2 * n)) / den; h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / den
    return p, c - h, c + h


def zero_step(r):
    return r.get("maxStepTimeout") == "1" or (r.get("endReason") == "maxStep" and r.get("steps") == "0")


def summarize(path):
    rows = load(path); ex = [r for r in rows if zero_step(r)]; r = [x for x in rows if not zero_step(x)]; n = len(r)
    k = sum(int(x["success"]) for x in r); s, lo, hi = wilson(k, n)
    ends = {e: sum(1 for x in r if x["endReason"] == e) for e in sorted(set(x["endReason"] for x in r))}
    lifted = sum(1 for x in r if int(x["stepsToLift"]) >= 0) / n if n else float("nan"); palm = sum(int(x["palm"]) for x in r) / n if n else float("nan")
    cont = st.mean(int(x["contacts"]) for x in r) if n else float("nan"); hold = st.mean(int(x["holdSteps"]) for x in r) if n else float("nan")
    head = r[0]["head_mode"] if r else "?"; mu = r[0]["mu"] if r else "?"
    return dict(file=path, head=head, mu=mu, n_total=len(rows), excluded_zero_step=len(ex), n=n, success=s, lo=lo, hi=hi, lifted=lifted, palm=palm, contacts=cont, meanHold=hold, ends=ends, rows=r)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0], epilog="Wilson 95 % interval; zero-step rows excluded from the aggregates and counted as excluded_zero_step.")
    ap.add_argument("--out", metavar="SUMMARY.csv", help="also write the summary rows to this CSV")
    ap.add_argument("passes", nargs="+", metavar="PASS.csv", help="eval-harness pass CSV(s)")
    a = ap.parse_args()
    print("| file | head | mu | n | excl. zero-step | success [95 % Wilson] | lifted | palm at end | contacts | mean hold steps | ends |"); print("|---|---|---|---|---|---|---|---|---|---|---|")
    w = None
    if a.out:
        f = open(a.out, "w", newline=""); w = csv.writer(f); w.writerow(["file", "head", "mu", "n_total", "excluded_zero_step", "n", "success", "wilson95_lo", "wilson95_hi", "lifted", "palm", "contacts", "meanHold", "ends"])
    for p in a.passes:
        d = summarize(p)
        print(f"| {os.path.basename(p)} | {d['head']} | {d['mu']} | {d['n']} | {d['excluded_zero_step']} | {d['success']:.3f} [{d['lo']:.3f}, {d['hi']:.3f}] | {d['lifted']:.3f} | {d['palm']:.3f} | {d['contacts']:.2f} | {d['meanHold']:.0f} | {d['ends']} |")
        if w:
            w.writerow([os.path.basename(p), d["head"], d["mu"], d["n_total"], d["excluded_zero_step"], d["n"], "%.4f" % d["success"], "%.4f" % d["lo"], "%.4f" % d["hi"], "%.4f" % d["lifted"], "%.4f" % d["palm"], "%.3f" % d["contacts"], "%.1f" % d["meanHold"], str(d["ends"]).replace(",", ";")])


if __name__ == "__main__":
    main()
