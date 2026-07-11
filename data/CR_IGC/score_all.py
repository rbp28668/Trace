"""Validate the DHT Planner+Scorer against the real Cloud Rally 2025 results.

Usage:  python score_all.py [DAY1 DAY2 ...]   (default: all days in validation.json)

For each day it:
  1. builds a course .cup = BGA waypoint table + extra waypoints + that day's
     task block (from data/CR2025_racer_tasks.cup);
  2. runs the Planner (per-day wind / vref) to size per-handicap barrels;
  3. scores every IGC trace in data/CR_IGC/<DAY>/ against its handicap's task,
     using the day's published D_Ref;
  4. diffs our scoring speed against the published SoaringSpot speed.

All per-day facts (wind, D_Ref, published results, turnpoint types) come from
validation.json; task geometry (sectors + barrels) comes from the .cup task set.
VREF is the H100 reference cruise airspeed — a calibration input, ~90 for this
contest (see docs/dht.md §2.1); overridable per day in DAY_VREF below.
"""
import subprocess, re, os, glob, sys, json, statistics

ROOT = "C:/Projects/Trace"
PLANNER = ROOT + "/src/Trace.Planner"
SCORER = ROOT + "/src/Trace.Scorer"
DATA = ROOT + "/data"
IGC_ROOT = DATA + "/CR_IGC"
WORK = IGC_ROOT + "/_work"           # generated courses + per-handicap tasks

FLEET = DATA + "/entrants_cloud_rally_2025_short.csv"
BGA = DATA + "/BGA TPs 2026-06-10.cup"
EXTRA = IGC_ROOT + "/extra_waypoints.cup"
TASKSET = DATA + "/CR2025_racer_tasks.cup"
VALIDATION = IGC_ROOT + "/validation.json"

HREF = 114
VREF = 90                            # H100 reference cruise airspeed (km/h)
DAY_VREF = {}                        # per-day overrides if ever needed
SPEED_TOL = 1.0                      # km/h; flag finishers differing by more

def load_handicaps():
    h = {}
    for line in open(DATA + "/entrants_cloud_rally_2025.csv"):
        p = line.strip().split(",")
        if p[0] != "CompNumber" and len(p) >= 6:
            h[p[0]] = float(p[5])
    return h

def waypoint_lines():
    """BGA waypoint table (up to -----Related Tasks-----) plus the extra TPs."""
    out = []
    with open(BGA, encoding="utf-8", errors="replace") as f:
        for line in f:
            if line.startswith("-----"):
                break
            out.append(line.rstrip("\n"))
    with open(EXTRA, encoding="utf-8", errors="replace") as f:
        for i, line in enumerate(f):
            if i == 0:
                continue  # skip the extra file's header row
            if line.strip():
                out.append(line.rstrip("\n"))
    return out

def task_blocks():
    """Parse CR2025_racer_tasks.cup into {DAYn: [task line, Options, ObsZone...]}."""
    blocks = {}
    cur = None
    with open(TASKSET, encoding="utf-8", errors="replace") as f:
        for line in f:
            s = line.rstrip("\n")
            if s.startswith('"DAY'):
                name = s.split(",", 1)[0].strip().strip('"')
                cur = name
                blocks[cur] = [s]
            elif cur and (s.startswith("Options") or s.startswith("ObsZone")):
                blocks[cur].append(s)
    return blocks

def build_course(day, block, wps):
    path = f"{WORK}/{day}/course.cup"
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        for w in wps:
            f.write(w + "\n")
        f.write("-----Related Tasks-----\n")
        for b in block:
            f.write(b + "\n")
    return path

def run(cmd):
    return subprocess.run(cmd, capture_output=True, text=True)

def plan(day, course, cfg):
    out_dir = f"{WORK}/{day}/tasks"
    os.makedirs(out_dir, exist_ok=True)
    vref = DAY_VREF.get(day, VREF)
    r = run(["dotnet", "run", "--no-build", "--project", PLANNER, "--",
             "--fleet", FLEET, "--course", course,
             "--wind", f"{cfg['wind_dir']}/{cfg['wind_kmh']:.2f}",
             "--vref", str(vref), "--href", str(HREF), "--out", out_dir])
    if r.returncode != 0:
        print(f"  PLANNER FAILED for {day}:\n{r.stderr.strip()[:400]}")
        return None
    return out_dir

def task_file(task_dir, h):
    return f"{task_dir}/task_h{h:g}".replace(".", "_") + ".cup"

def score_day(day, cfg, hcap, blocks, wps):
    course = build_course(day, blocks[day], wps)
    task_dir = plan(day, course, cfg)
    if task_dir is None:
        return
    published = cfg["results"]

    rows = []
    for igc in sorted(glob.glob(f"{IGC_ROOT}/{day}/*_*.igc")):
        comp = os.path.basename(igc).split("_", 1)[1][:-4]
        h = hcap.get(comp)
        tf = task_file(task_dir, h) if h else None
        if not tf or not os.path.exists(tf):
            rows.append((comp, h, "?", None)); continue
        o = run(["dotnet", "run", "--no-build", "--project", SCORER, "--",
                 "--task", tf, "--igc", igc, "--dref", str(cfg["dref"]),
                 "--href", str(HREF), "--handicap", str(h)]).stdout
        m = re.search(r"scoring speed\s+([\d.]+)", o)
        speed = float(m.group(1)) if m else None
        stat = "FIN" if speed else ("LO" if "LANDED OUT" in o else "DNS")
        rows.append((comp, h, stat, speed))

    print(f"\n=== {day} ({cfg['date']})  wind {cfg['wind_dir']}/{cfg['wind_kmh']:.1f}  "
          f"D_Ref {cfg['dref']}  vref {DAY_VREF.get(day, VREF)} ===")
    print(f"  {'Comp':5}{'H':>6}{'stat':>5}{'ours':>9}{'pub':>9}{'diff':>8}")
    errs = []
    for comp, h, stat, speed in rows:
        pr = published.get(comp) or {}
        ps = pr.get("speed")
        if speed and ps:
            d = speed - ps; errs.append(d)
            flag = "  <-- off" if abs(d) > SPEED_TOL else ""
            print(f"  {comp:5}{h:>6}{stat:>5}{speed:>9.2f}{ps:>9.2f}{d:>+8.2f}{flag}")
        elif speed or ps:
            print(f"  {comp:5}{str(h):>6}{stat:>5}"
                  f"{(f'{speed:.2f}' if speed else '-'):>9}{(f'{ps:.2f}' if ps else '-'):>9}")
    if errs:
        print(f"  finishers matched: {len(errs)}  meanabs={statistics.mean(abs(e) for e in errs):.2f}"
              f"  max={max(abs(e) for e in errs):.2f} km/h")
    return errs

def main():
    v = json.load(open(VALIDATION))
    days = [a for a in sys.argv[1:]] or [d for d in v if not d.startswith("_")]
    hcap = load_handicaps()
    blocks = task_blocks()
    wps = waypoint_lines()
    all_errs = []
    for day in days:
        cfg = v.get(day)
        if not cfg:
            print(f"skip {day}: not in validation.json"); continue
        if day not in blocks:
            print(f"skip {day}: no task block in {os.path.basename(TASKSET)}"); continue
        e = score_day(day, cfg, hcap, blocks, wps)
        if e:
            all_errs.extend(e)
    if all_errs:
        print(f"\nOVERALL: {len(all_errs)} finisher comparisons  "
              f"meanabs={statistics.mean(abs(e) for e in all_errs):.2f}  "
              f"max={max(abs(e) for e in all_errs):.2f} km/h")

if __name__ == "__main__":
    main()
