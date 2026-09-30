"""Compare two gate runs row by row (tools/eval/gates_014.py outputs): every numeric column must match exactly; the note column is compared with the wall-clock
fields (wall=..s, stepsPerSec=..) removed. usage: python -m tools.eval.gates_compare A_dir B_dir [tags...]"""
import csv, os, re, sys
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
def rows(p): return [r for r in csv.DictReader(open(os.path.join(REPO, p), newline="")) if (r.get("trial") or "").isdigit()]
def clean(n): return re.sub(r"wall=[\d.]+s|stepsPerSec=\d+", "", n or "")
a, b = sys.argv[1], sys.argv[2]; tags = sys.argv[3:] or sorted(f[:-4] for f in os.listdir(os.path.join(REPO, a)) if f.endswith(".csv"))
allok = True
for t in tags:
    pa, pb = os.path.join(a, t + ".csv"), os.path.join(b, t + ".csv")
    if not (os.path.exists(os.path.join(REPO, pa)) and os.path.exists(os.path.join(REPO, pb))): print(t, "missing"); continue
    A, B = rows(pa), rows(pb); diffs = []
    for ra, rb in zip(A, B):
        for k in ra:
            if k == "note": continue
            if ra[k] != rb.get(k): diffs.append((ra["trial"], k, ra[k], rb.get(k)))
        if clean(ra.get("note")) != clean(rb.get("note")): diffs.append((ra["trial"], "note", "", ""))
    ok = not diffs and len(A) == len(B); allok &= ok
    print(f"{t}: rows {len(A)}/{len(B)} {'IDENTICAL' if ok else 'DIFFER'}" + ("" if ok else f" first {diffs[:3]}"))
print("ALL IDENTICAL" if allok else "SOME DIFFER")
