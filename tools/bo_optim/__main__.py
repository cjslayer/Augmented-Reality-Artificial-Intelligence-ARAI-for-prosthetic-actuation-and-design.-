"""python -m tools.bo_optim run     # full campaign (resumable: finished candidates are read back from records.jsonl)
python -m tools.bo_optim report  # regenerate REPORT.md / best_so_far.csv / best_so_far.svg / candidates.csv"""
import sys

from .campaign import Campaign, StopCampaign
from .report import write_report


def main(argv):
    cmd = argv[0] if argv else "run"
    if cmd == "run":
        c = Campaign()
        try:
            c.run_all()
        except StopCampaign as e:
            c.log("STOP: " + str(e))
            return 2
        write_report()
        return 0
    if cmd == "report":
        print(write_report()); return 0
    print(__doc__); return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
