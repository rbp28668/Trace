import subprocess, re, os, glob, statistics

SCORER = "C:/Projects/Trace/src/Trace.Scorer"
IGC_DIR = "C:/Projects/Trace/data/CR_IGC"
TASK_DIR = IGC_DIR + "/tasks"
DREF = 221.93  # published scoring distance (constant across finishers)

hcap = {}
for line in open("C:/Projects/Trace/data/entrants_cloud_rally_2025.csv"):
    p = line.strip().split(",")
    if p[0] == "CompNumber" or len(p) < 6: continue
    hcap[p[0]] = float(p[5])

# published daily finishers: comp -> speed
published = {"XA":118.80,"700":112.58,"909":111.40,"841":110.02,"HA":108.69,
    "871":107.60,"W8":106.93,"JR":104.52,"521":102.84,"V11":101.83}

def task_file(h): return f"{TASK_DIR}/task_h{h:g}".replace(".","_")+".cup"

rows=[]
for igc in sorted(glob.glob(IGC_DIR+"/589_*.igc")):
    comp = os.path.basename(igc)[4:-4]
    h = hcap.get(comp)
    tf = task_file(h) if h else None
    if not tf or not os.path.exists(tf):
        rows.append((comp,h,"?",None)); continue
    out = subprocess.run(["dotnet","run","--no-build","--project",SCORER,"--",
        "--task",tf,"--igc",igc,"--dref",str(DREF),"--href","114","--handicap",str(h)],
        capture_output=True,text=True).stdout
    m = re.search(r"scoring speed\s+([\d.]+)", out)
    speed = float(m.group(1)) if m else None
    stat = "FIN" if speed else ("LO" if "LANDED OUT" in out else "DNS")
    rows.append((comp,h,stat,speed))

print(f"{'Comp':5}{'H':>6}{'stat':>5}{'ours':>9}{'pub':>9}{'diff':>8}{'diff%':>7}")
errs=[]
for comp,h,stat,speed in rows:
    ps = published.get(comp)
    if speed and ps:
        d=speed-ps; errs.append(d)
        print(f"{comp:5}{h:>6}{stat:>5}{speed:>9.2f}{ps:>9.2f}{d:>+8.2f}{100*d/ps:>+6.1f}%")
    else:
        print(f"{comp:5}{str(h):>6}{stat:>5}{(f'{speed:.2f}' if speed else '-'):>9}{(f'{ps:.2f}' if ps else '-'):>9}")
if errs:
    print(f"\nn={len(errs)} mean={statistics.mean(errs):+.2f} km/h  meanabs={statistics.mean(abs(e) for e in errs):.2f}  max={max(abs(e) for e in errs):.2f}")
