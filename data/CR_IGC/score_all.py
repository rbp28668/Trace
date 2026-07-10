"""Score Cloud Rally 2025 IGC traces against the published SoaringSpot results.

Usage:  python score_all.py [DAY1]

Traces live in data/CR_IGC/<DAY>/<flightId>_<compNumber>.igc (the flightId
prefix varies per day; the competition number follows the underscore). Each day
needs its per-handicap task files generated first, e.g. for DAY1:

  cd src/Trace.Planner && dotnet run -- \
    --fleet ../../data/entrants_cloud_rally_2025_short.csv \
    --course ../../data/cr2025_racer_task1.cup \
    --wind 265/33.3 --vref 90 --href 114 \
    --out ../../data/CR_IGC/DAY1/tasks

Fill in DAYS below (task course, wind, D_Ref, published finisher speeds) to
validate additional days.
"""
import subprocess, re, os, glob, sys, statistics

ROOT = "C:/Projects/Trace"
SCORER = ROOT + "/src/Trace.Scorer"
IGC_ROOT = ROOT + "/data/CR_IGC"

# Per-day config. Add entries as task sheets / results are transcribed.
DAYS = {
    "DAY1": {
        "dref": 221.93,   # published scoring distance (constant across finishers)
        "href": 114,
        # comp -> published daily scoring speed (km/h)
        "published": {
            "XA": 118.80, "700": 112.58, "909": 111.40, "841": 110.02, "HA": 108.69,
            "871": 107.60, "W8": 106.93, "JR": 104.52, "521": 102.84, "V11": 101.83,
        },
    },
}

def load_handicaps():
    hcap = {}
    for line in open(ROOT + "/data/entrants_cloud_rally_2025.csv"):
        p = line.strip().split(",")
        if p[0] == "CompNumber" or len(p) < 6:
            continue
        hcap[p[0]] = float(p[5])
    return hcap

def comp_of(path):
    # <flightId>_<comp>.igc  ->  comp
    return os.path.basename(path).split("_", 1)[1][:-4]

def task_file(day, h):
    return f"{IGC_ROOT}/{day}/tasks/task_h{h:g}".replace(".", "_") + ".cup"

def score(day):
    cfg = DAYS.get(day)
    if cfg is None:
        sys.exit(f"No config for {day}; add it to DAYS in score_all.py")
    hcap = load_handicaps()
    published = cfg["published"]

    rows = []
    for igc in sorted(glob.glob(f"{IGC_ROOT}/{day}/*_*.igc")):
        comp = comp_of(igc)
        h = hcap.get(comp)
        tf = task_file(day, h) if h else None
        if not tf or not os.path.exists(tf):
            rows.append((comp, h, "?", None))
            continue
        out = subprocess.run(
            ["dotnet", "run", "--no-build", "--project", SCORER, "--",
             "--task", tf, "--igc", igc, "--dref", str(cfg["dref"]),
             "--href", str(cfg["href"]), "--handicap", str(h)],
            capture_output=True, text=True).stdout
        m = re.search(r"scoring speed\s+([\d.]+)", out)
        speed = float(m.group(1)) if m else None
        stat = "FIN" if speed else ("LO" if "LANDED OUT" in out else "DNS")
        rows.append((comp, h, stat, speed))

    print(f"{day}:  {'Comp':5}{'H':>6}{'stat':>5}{'ours':>9}{'pub':>9}{'diff':>8}{'diff%':>7}")
    errs = []
    for comp, h, stat, speed in rows:
        ps = published.get(comp)
        if speed and ps:
            d = speed - ps
            errs.append(d)
            print(f"      {comp:5}{h:>6}{stat:>5}{speed:>9.2f}{ps:>9.2f}{d:>+8.2f}{100*d/ps:>+6.1f}%")
        else:
            print(f"      {comp:5}{str(h):>6}{stat:>5}"
                  f"{(f'{speed:.2f}' if speed else '-'):>9}{(f'{ps:.2f}' if ps else '-'):>9}")
    if errs:
        print(f"\n      n={len(errs)} mean={statistics.mean(errs):+.2f} km/h  "
              f"meanabs={statistics.mean(abs(e) for e in errs):.2f}  max={max(abs(e) for e in errs):.2f}")

if __name__ == "__main__":
    score(sys.argv[1] if len(sys.argv) > 1 else "DAY1")
