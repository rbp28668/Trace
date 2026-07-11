"""Render each Cloud Rally 2025 DHT task as an SVG, drawing the observation
sectors and barrels exactly as Trace.Core ScoringEngine interprets them.

Both the waypoint coordinates AND the task geometry (names + ObsZone lines) are
parsed straight from each day's .cup file -- nothing is hand-transcribed -- so the
drawing cannot drift from the source task. Zone semantics drawn:

 - Start (Style=2, Line=1): a start line perpendicular to the first leg (R1 = half-width).
 - Turnpoint sector (Style=1, A1<180): a wedge of half-angle A1 about the sector
   direction. Symmetric (Style=1) opens OUTWARD -- the reverse of the inward
   bisector of the bearings to the two neighbours -- matching ScoringEngine and
   dht.md §4.2. Directional styles (2/3/4) face the named neighbour.
 - Barrel R2: the inner full circle used for distance (checkpoints: 0.5 km).
 - Finish (Style=3): a full ring of radius R1.

Run with no args to render every day under data/CR_IGC/_work; each course.cup
carries both the waypoint table and that day's Related Tasks block.
"""
import csv, math, os

WORK = r"C:\Projects\Trace\data\CR_IGC\_work"
OUT = r"C:\Projects\Trace\docs\tasks_svg"
DAYS = ["DAY1", "DAY2", "DAY3", "DAY4", "DAY6", "DAY7"]


def dm(s):
    """Parse a CUP DDMM.mmm[NSEW] coordinate to signed decimal degrees."""
    h = s[-1]; s = s[:-1]
    if h in "NS":
        d = int(s[:2]); m = float(s[2:])
    else:
        d = int(s[:3]); m = float(s[3:])
    v = d + m / 60.0
    return -v if h in "SW" else v


def parse_cup(path):
    """Return (coords: name->(lat,lon), task_names: [str], zones: [dict]).

    Reads the waypoint table up to '-----Related Tasks-----', then the first task
    line (dropping the leading ??? takeoff and trailing ??? landing) and its
    ObsZone lines.
    """
    coords = {}
    task_names = None
    zones = []
    with open(path, newline="", encoding="utf-8", errors="replace") as f:
        lines = f.read().splitlines()

    i = 0
    # Waypoint table (until the Related Tasks marker).
    for i, line in enumerate(lines):
        if line.startswith("-----"):
            break
        row = next(csv.reader([line]))
        if len(row) >= 5 and row[0] not in ("name", "Name"):
            try:
                coords[row[0]] = (dm(row[3]), dm(row[4]))
            except (ValueError, IndexError):
                pass

    # Related Tasks: first quoted task line, then ObsZone lines.
    for line in lines[i + 1:]:
        if line.startswith('"') and task_names is None:
            fields = next(csv.reader([line]))
            # Drop task name (fields[0]) and the ??? takeoff/landing placeholders.
            task_names = [n for n in fields[1:] if n and n != "???"]
        elif line.startswith("ObsZone="):
            zones.append(parse_zone(line))

    return coords, task_names, zones


def parse_zone(s):
    d = {}
    for tok in s.split(","):
        if "=" in tok:
            k, v = tok.split("=", 1)
            d[k] = v
    return {
        "index": int(d["ObsZone"]),
        "style": int(d.get("Style", "0")),
        "R1": float(d.get("R1", "0m").rstrip("m")) / 1000.0,
        "A1": float(d.get("A1", "180")),
        "R2": float(d.get("R2", "0m").rstrip("m")) / 1000.0,
        "line": d.get("Line", "0") == "1",
    }


def bearing(a, b):
    p1 = math.radians(a[0]); p2 = math.radians(b[0]); dl = math.radians(b[1] - a[1])
    y = math.sin(dl) * math.cos(p2)
    x = math.cos(p1) * math.sin(p2) - math.sin(p1) * math.cos(p2) * math.cos(dl)
    return (math.degrees(math.atan2(y, x)) + 360) % 360


def bisector(a, b):
    ar = math.radians(a); br = math.radians(b)
    x = math.cos(ar) + math.cos(br); y = math.sin(ar) + math.sin(br)
    if abs(x) < 1e-12 and abs(y) < 1e-12:
        return a
    return (math.degrees(math.atan2(y, x)) + 360) % 360


def zone_dir(z, i, C):
    """Sector bisector direction (deg true) for point i; needs geographic C."""
    style = z[i]["style"]
    to_prev = bearing(C[i], C[i - 1]) if i > 0 else None
    to_next = bearing(C[i], C[i + 1]) if i < len(C) - 1 else None
    if style == 2:  # ToNext
        return to_next if to_next is not None else to_prev
    if style == 3:  # ToPrevious
        return to_prev if to_prev is not None else to_next
    if style == 4:  # ToStart
        return bearing(C[i], C[0])
    if to_prev is not None and to_next is not None:  # Symmetric: OUTWARD
        return (bisector(to_prev, to_next) + 180.0) % 360.0
    return to_prev if to_prev is not None else to_next


def make_svg(day, coords, nm, z):
    C = [coords[n] for n in nm]
    lat0 = sum(c[0] for c in C) / len(C)
    lon0 = sum(c[1] for c in C) / len(C)
    kmlat = 111.19
    kmlon = 111.19 * math.cos(math.radians(lat0))
    XY = [((lo - lon0) * kmlon, (la - lat0) * kmlat) for la, lo in C]
    xs = []; ys = []
    for i, (x, y) in enumerate(XY):
        r = max(z[i]["R1"], z[i]["R2"])
        xs += [x - r, x + r]; ys += [y - r, y + r]
    x0, x1, y0, y1 = min(xs) - 5, max(xs) + 5, min(ys) - 5, max(ys) + 5
    W = x1 - x0; Ht = y1 - y0
    scale = 950.0 / max(W, Ht)
    sx = lambda x: (x - x0) * scale
    sy = lambda y: (y1 - y) * scale
    svgw = W * scale; svgh = Ht * scale
    E = []
    lp = " ".join(("M" if i == 0 else "L") + f"{sx(x):.1f},{sy(y):.1f}" for i, (x, y) in enumerate(XY))
    E.append(f'<path d="{lp}" fill="none" stroke="#999" stroke-width="1.5" stroke-dasharray="6 4"/>')
    for i, (x, y) in enumerate(XY):
        p = z[i]; cx = sx(x); cy = sy(y)
        R1 = p["R1"] * scale; R2 = p["R2"] * scale
        d = zone_dir(z, i, C)
        if p["line"]:
            hw = p["R1"] * scale
            a1 = math.radians(90 - (d + 90)); a2 = math.radians(90 - (d - 90))
            E.append(f'<line x1="{cx + hw * math.cos(a1):.1f}" y1="{cy - hw * math.sin(a1):.1f}" x2="{cx + hw * math.cos(a2):.1f}" y2="{cy - hw * math.sin(a2):.1f}" stroke="#0a7" stroke-width="3"/>')
            E.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="3.5" fill="#0a7"/>')
            E.append(f'<text x="{cx + 8:.1f}" y="{cy - 8:.1f}" font-size="14" font-weight="bold" fill="#053">{i}. {nm[i]}</text>')
            E.append(f'<text x="{cx + 8:.1f}" y="{cy + 10:.1f}" font-size="11" fill="#555">START line &#177;{p["R1"]:.0f}km</text>')
            continue
        A1 = p["A1"]
        if A1 < 180 and R1 > 0:
            # Sample the wedge as an explicit polygon from bearing d-A1 to d+A1
            # (bearing: 0=N, clockwise). Screen x = R*sin(bearing), y = -R*cos.
            def pt(bb):
                r = math.radians(bb)
                return (cx + R1 * math.sin(r), cy - R1 * math.cos(r))
            n = max(2, int(2 * A1 / 3))
            arc = [pt(d - A1 + (2 * A1) * k / n) for k in range(n + 1)]
            poly = f"M{cx:.1f},{cy:.1f} " + " ".join(f"L{px:.1f},{py:.1f}" for px, py in arc) + " Z"
            E.append(f'<path d="{poly}" fill="#3a7bd5" fill-opacity="0.15" stroke="#3a7bd5" stroke-width="1"/>')
        elif R1 > 0 and p["R2"] == 0:
            E.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="{R1:.1f}" fill="#e07b39" fill-opacity="0.15" stroke="#e07b39" stroke-width="1.5"/>')
        if R2 > 0:
            E.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="{R2:.1f}" fill="#e07b39" fill-opacity="0.30" stroke="#e07b39" stroke-width="1.5"/>')
        E.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="3.5" fill="#111"/>')
        sub = []
        if A1 < 180:
            sub.append(f'sector {p["R1"]:.0f}km/{A1:.0f}&#176;')
        if p["R2"] > 0:
            sub.append(f'barrel {p["R2"]:.1f}km')
        if p["style"] == 3:
            sub.append(f'finish ring {p["R1"]:.0f}km')
        E.append(f'<text x="{cx + 8:.1f}" y="{cy - 8:.1f}" font-size="14" font-weight="bold" fill="#111">{i}. {nm[i]}</text>')
        if sub:
            E.append(f'<text x="{cx + 8:.1f}" y="{cy + 10:.1f}" font-size="11" fill="#555">{"; ".join(sub)}</text>')
    E.append(f'<text x="18" y="32" font-size="20" font-weight="bold">{day}</text>')
    bar = 10 * scale
    E.append(f'<line x1="18" y1="{svgh - 20:.0f}" x2="{18 + bar:.0f}" y2="{svgh - 20:.0f}" stroke="#000" stroke-width="2"/>')
    E.append(f'<text x="18" y="{svgh - 26:.0f}" font-size="12">10 km</text>')
    E.append(f'<text x="{svgw - 44:.0f}" y="26" font-size="13">N &#8593;</text>')
    body = "\n".join(E)
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{svgw:.0f}" height="{svgh:.0f}" '
            f'viewBox="0 0 {svgw:.0f} {svgh:.0f}" font-family="sans-serif">\n'
            f'<rect width="100%" height="100%" fill="#fbfbf9"/>\n{body}\n</svg>\n')


def main():
    os.makedirs(OUT, exist_ok=True)
    for day in DAYS:
        coords, nm, z = parse_cup(os.path.join(WORK, day, "course.cup"))
        if not nm or len(z) != len(nm):
            print(day, f"ZONE/NAME MISMATCH: {len(nm or [])} names, {len(z)} zones")
            continue
        missing = [n for n in nm if n not in coords]
        if missing:
            print(day, "MISSING COORDS", missing)
            continue
        with open(os.path.join(OUT, f"{day}.svg"), "w", encoding="utf-8") as f:
            f.write(make_svg(day, coords, nm, z))
        # Report the parsed geometry so it can be eyeballed against the .cup.
        desc = ", ".join(f'{nm[i]}[{"cp" if 0 < z[i]["R2"] <= 0.5 else "tp" if z[i]["R2"] > 0 else "S/F"}]'
                         for i in range(len(nm)))
        print(f"wrote {day}: {desc}")


if __name__ == "__main__":
    main()
