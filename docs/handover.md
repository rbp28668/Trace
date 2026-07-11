# DHT Engine — Handover

Status of the Distance Handicap Task (DHT) engine and scoring system as of the
`feature/task-diagrams-outward-sectors` branch (pushed to `origin`). All work is
verified against real sample data in `data/`; the full test suite is
**84 passing**.

**Headline result:** the Scorer reproduces the published SoaringSpot daily results
for the real Cambridge Cloud Rally 2025 across all six flown days — **75 finisher
comparisons match to a mean 0.01 km/h (max 0.07)** (see §4 "Verified against real
data" and §9). Getting there corrected the observation-zone model and start
detection; those learnings are the most important content of this document
(§5, §9).

## 1. What this is

Two command-line tools over a shared library that implement the requirements in
[`dht.md`](dht.md) and [`airspace.md`](airspace.md):

- **Planner** — sizes turnpoint observation-zone "barrels" per glider handicap so
  that, flying to their polars, all gliders finish together. Emits one standard
  SeeYou `.CUP` task file per handicap.
- **Scorer** — evaluates flown IGC traces against a personalised task: scoring
  speed for finishers, handicapped achieved distance for land-outs, plus an
  airspace-infringement scan.

## 2. Solution layout

```
src/
  Trace.Core/      shared library — domain, geometry, wind, file I/O, airspace
    Model/         Glider, Fleet, Course, Waypoint, ObservationZone, CupTask
    Geometry/      Geodesy, LegGeometry, Polygon
    Wind/          Windicap, TaskMetrics
    Planning/      CourseGeometry, BarrelOptimizer
    Scoring/       ScoringTask, ScoringEngine
    Rendering/     TaskDiagram (SVG task diagram)
    Airspace/      OpenAirReader, AirspaceVolume, AirspaceChecker, AirspaceLoader
    Io/            IGCFile, TracePoint, TraceList, Task, TaskPoint, Extension,
                   CupReader, CupWriter, CourseReader, CupTaskLayout, FleetReader
  Trace.Planner/   task-optimisation executable
  Trace.Scorer/    scoring executable (legacy IGC dump kept as --dump)
  Trace.Tests/     xUnit tests (84)
  Trace.sln
```

.NET 10, C#, `Nullable` + `ImplicitUsings` enabled. Internally: distances in km,
speeds km/h, angles degrees (radians only inside trig). Coordinate convention
inherited from the original IGC code: latitude = "northings" (north +ve),
longitude = "eastings" (east +ve, west −ve).

## 3. How to run

```sh
# Build & test
cd src && dotnet build Trace.sln && dotnet test Trace.sln

# Plan a task (one .cup per handicap into out/)
dotnet run --project Trace.Planner -- \
  --fleet ../data/sample_fleet.csv --course ../data/sample_course.cup \
  --wind 270/20 --vref 130 [--href 120] \
  [--airspace ../data/uk2026-06-11.txt --max-height 10000] \
  [--svg ../data/plan_out/task.svg] --out ../data/plan_out

# Score a flight against a personalised task
dotnet run --project Trace.Scorer -- \
  --task task_h93.cup --igc flight.igc \
  --dref 117.5 --href 114 --handicap 93 [--airspace ../data/uk2026-06-11.txt]

# Legacy IGC summary
dotnet run --project Trace.Scorer -- --dump flight.igc
```

The Planner exits **3** and writes nothing if any barrel penetrates controlled
airspace (dht.md §5.2).

### Reproducing the real Cloud Rally 2025 validation

The six flown days' tasks are in `data/CR2025_racer_tasks.cup`; the flown traces
are in `data/CR_IGC/DAY1..DAY7/` as `<flightId>_<compNumber>.igc`. The harness
`data/CR_IGC/score_all.py` builds each day's course, runs the Planner, scores
every trace and diffs against the published speeds — all per-day facts (wind,
D_Ref, published results) come from `data/CR_IGC/validation.json`:

```sh
python ../data/CR_IGC/score_all.py            # all days
python ../data/CR_IGC/score_all.py DAY1 DAY7  # selected days
# -> OVERALL: 75 finisher comparisons  meanabs=0.01  max=0.07 km/h
```

Two inputs here are **not** derivable from the course file and were reverse-
engineered from the task sheet — get them wrong and the numbers drift:
- **`vref = 90`** (H100 reference cruise airspeed) — see §9. The 130 default is
  wrong for this contest; 90 km/h reproduces the day's barrels closely.
- **`D_Ref`** (per day in `validation.json`) — the published scoring distance,
  the reference glider's *effective* (corner-cut) distance, **not** the
  centre-to-centre task length. See §9 for how the Planner computes its own D_Ref.

## 4. What is implemented (by spec section)

| Area | Spec | Where | Notes |
|------|------|-------|-------|
| Geodesic distance/bearing | §3.1 | `Geodesy` | WGS84 ellipsoid (Vincenty inverse/direct); reproduces task-sheet legs to 0.1 km |
| Deflection/vertex angle, distance saved | §3.1–3.2 | `LegGeometry` | `2R·sin(Δφ/2)`, inverse, θ>150° reject |
| Airspeed scaling, wind triangle | §3.3 | `Windicap` | `√(Va²−Wx²)−Wh`, `|Wx|≤Va` guard, 0.4·Va override |
| D_Ref / T_Ref | §3.4 | `TaskMetrics`, `BarrelOptimizer` | D_Ref = reference glider's effective (corner-cut) distance at R_min; see §9 |
| Barrel optimisation | §4.1 | `BarrelOptimizer` | bisection on a single radius R to ±5 s; one uniform R per handicap across all variable turnpoints; [R_min,R_max] |
| Scoring — finishers | §4.2 A | `ScoringEngine` | T_Act, V_Score = D_Ref/T_Act |
| Scoring — land-outs | §4.2 B | `ScoringEngine` | CPA marking, D_Final = D_Ach·(H_Ref/H) |
| Observation zones | — | `ScoringEngine`, `ObservationZone` | sector (R1/A1) + barrel (R2); Style direction (symmetric opens outward); in-order visiting |
| Start line | — | `ScoringEngine` | line-crossing timing; last start-zone exit before TP1 |
| Task read (SeeYou) | §5.1 | `CupReader`, `CourseReader`, `CupTaskLayout` | ??? takeoff/landing, ObsZone=0=Start, per-TP Rmin/Rmax in userdata |
| CUP export | §5.1 | `CupWriter`, `PlanWriter` | verbatim passthrough; only the barrel radius changes per handicap |
| Task diagram (SVG) | — | `TaskDiagram`, Planner `--svg` | fleet-overview: legs, sectors, barrels (source-CUP R2), start line, finish ring |
| Variable Barrel Sizes table | — | `PlanReport` | task-sheet format: Handicap \| Radius \| Act Dist \| Hcp Dist, per distinct handicap descending, 1 dp |
| Airspace guardrail | §5.2 | `AirspaceGuard` | hard error blocks publication |
| OpenAir read, multi-file | airspace.md | `OpenAirReader`, `AirspaceLoader` | AC/AN/AL/AH/DP/DC/V/DB |
| Point-in-airspace | airspace.md | `AirspaceChecker` | nearest-N cache, class filter |
| Zone-intersects-airspace | airspace.md | `AirspaceChecker` | max-height cut, class filter |

### Verified against real data
- **End-to-end vs a real competition (the strongest evidence):** Planner sizes the
  Cloud Rally 2025 barrels to match the published per-handicap tables (Task 1
  radii reproduce to 0.1 km), and the Scorer reproduces the finisher scoring
  speeds across all six flown days — **75 finisher comparisons, mean 0.01 km/h,
  max 0.07** (`data/CR_IGC/`, `score_all.py`).
- Planner: 4-glider fleet on a Lasham triangle converges all gliders to T_Ref
  within ±5 s; output `.cup` re-parses as valid CUP.
- Planner guardrail: blocks publication when sample barrels clip London TMA /
  Cotswold / Daventry CTAs in `data/uk2026-06-11.txt`.
- Scorer: `--dump` and zone-transit detection on the 9,521-fix `659VF4L1.igc`;
  infringement scan reports cleanly.
- SeeYou gold-standard task read: `data/cr2025_racer_2.cup` (??? placeholders,
  sector+barrel zones) parses, classifies and round-trips barrels-only.

## 5. Key learnings (read before touching scoring or CUP I/O)

These were hard-won during the Cloud Rally validation and are easy to get wrong.

- **Observation zone ≠ barrel.** A SeeYou turnpoint has two nested shapes:
  `R1/A1` is the wide **observation sector** (did-you-round-it), `R2/A2` is the
  inner **barrel** (a full circle, used only for the DHT distance/corner-cut).
  A point is *achieved* if a fix is inside the barrel **or** the sector; the
  optimiser only ever sizes the barrel. `ObservationZone.BarrelRadiusMetres` = R2
  when present, else R1 (a plain cylinder). **Do not read R1 as the barrel** — an
  earlier version did and mis-scored every pilot who rounded wide.
- **Angle is a half-angle** either side of the zone direction: `A1=45` → a 90°
  sector, `A1=180` → a full circle. Both the sector radius **and** the half-angle
  gate whether a fix is in the sector.
- **Symmetric sectors open *outward*** — away from both the inbound and outbound
  legs (the reverse of the bisector of the bearings to the two neighbours). Facing
  it inward silently lets a pilot clip the sector on the way home.
- **Turnpoints must be achieved in order.** `ScoringEngine` advances its search
  cursor past each achieved fix, so a later point can't be credited from an
  earlier fix. With the wide overlapping sectors this is essential, not cosmetic.
- **The start is a LINE** (Style=2, `Line=1`), timed at the line crossing — not the
  cylinder exit (~2 min late). And the valid start is the **last** departure from
  the start zone before the first turnpoint: pilots climb in and out of the start
  zone pre-start, and taking the first exit counted that climb as task time.
- **`VRefCru` and `D_Ref` are free inputs**, not in the course file. `VRefCru` (the
  H100 cruise airspeed) sets wind sensitivity — for Cloud Rally it is ~90 km/h,
  derived from the reference glider's air-vs-ground distance on the task sheet, and
  the 130 default is wrong there. The scoring `D_Ref` is the reference glider's
  *effective* (corner-cut) distance (221.93 km), **not** the centre-to-centre
  length (234.92 km). See §9 for the derivation.
- **Trust the task sheet (TIFF), not the SoaringSpot web page** for what was flown:
  the web task is a later "v2" re-task and disagrees with the day's actual sheet.

## 5b. Remaining limitations / decisions made

- **Fixed leg bearings in the optimiser.** Legs are treated centre-to-centre; the
  marginal bearing change as barrels grow is absorbed by the convergence loop
  rather than re-derived each iteration (dht.md §4.1 allows either).
- **Uniform barrel radius per handicap.** Every variable turnpoint gets the *same*
  radius R for a given handicap (the DHT convention and how the task sheets are
  built); the optimiser bisects that single R against T_Ref. The distance saved
  therefore varies with each turn's geometry — an acute turn saves more than an
  obtuse one, since the saving is `2R·sin(Δφ/2)`. (An earlier version instead
  equalised distance-saved per turnpoint, which forced *different* radii; that was
  wrong.)
- **A wide handicap spread can be geometrically infeasible** on a course with few
  or shallow turnpoints (one 90° corner at R_max≈12 km saves only ~16 km). The
  optimiser flags this as non-convergence rather than failing silently — it is
  inherent to the method, not a bug.
- **Handicaps are supplied per glider** in the fleet CSV (BGA scheme assumed);
  there is no built-in handicap-list lookup. `HRef` defaults to the highest in
  the fleet, overridable with `--href`. Note the Planner needs the *short*-schema
  fleet CSV (`Type,Registration,CompNumber,Handicap`); the full entrants CSV has a
  different column order and silently reads zero gliders.
- **"SUA" airspace label** from dht.md §5.2 was not confirmed for UK/Europe; the
  guardrail uses CTR/TMA/CTA/A/C/D as controlled classes (see `AirspaceLoader`).
- Build emits harmless `LF→CRLF` warnings (no `.gitattributes`); see below.

## 6. Suggested enhancements

Roughly in priority order.

1. **`Style=Fixed` (A12) sector direction** is not yet handled in the Scorer's
   `ZoneDirection` (only Symmetrical / ToNext / ToPrevious / ToStart). Cloud Rally
   didn't use it, but declared FAI tasks might.
2. **Derive `D_Ref` and default `VRefCru` automatically** rather than passing them
   by hand. `D_Ref` is the reference glider's effective distance the Planner
   already computes (§9); the Scorer could read it from the task/report instead of
   a `--dref` flag. `VRefCru` could be inferred or documented per contest.
4. **Iterative bearing re-derivation** in `BarrelOptimizer`: recompute leg
   bearings/ground speeds from actual barrel entry/exit points each iteration for
   sub-second precision on sharply-doglegged tasks.
5. **Optimiser feasibility report**: when a fleet/course can't converge, output
   the minimum extra turnpoints or the achievable handicap range, to guide
   task-setting.
6. **Richer scoring**: start-height/finish-height limits, speed-points vs
   distance-points formulae, devaluation, multiple start gates.
7. **Engine-noise / motor-glider handling** (ENL extension is parsed but unused).
8. **Airspace report polish**: dedupe stacked sub-volumes in the Scorer output
   (the Planner already does); optionally report closest-approach distance.
9. **CI**: a GitHub Actions workflow running `dotnet test`; add `.gitattributes`
   (`*.cs text eol=lf`) to silence the line-ending warnings.
10. **Packaging**: `dotnet publish` single-file binaries; a short user guide for
    task-setters and scorers.

Now **done** (were on this list): FAI sector observation zones (§5); fixed-size
control points — `CourseReader` classifies a sub-1 km barrel as `Checkpoint` and
carries per-TP Rmin/Rmax from waypoint userdata JSON; **WGS84 geodesy** (Vincenty,
closed the ~0.3% distance gap); **symmetric sectors open outward**; **uniform
barrel radius per handicap** with the task-sheet Variable Barrel Sizes table; and
an **SVG task diagram** (`--svg`).

## 7. Test coverage

`Trace.Tests` (84 tests): geodesy vs published WGS84 values (Land's End → John
o' Groats geodesic + bearing) and the five DAY1 legs pinned to the task sheet;
leg-geometry round-trips and shallow-turn boundary; wind-triangle cases and
guards; optimiser convergence (nil wind + crosswind), bounds, distance-ratio vs
handicap, per-turnpoint bounds; CUP/fleet/course parse and round-trip incl. the
real 1,391-waypoint BGA file, the SeeYou gold-standard task, ??? placeholders and
userdata Rmin/Rmax; scoring (finisher, start-zone timing, land-out scaling,
sector-vs-barrel achievement, in-order turnpoints); task-diagram SVG (well-formed
output, outward symmetric sector, supplied-radius override); OpenAir parsing incl.
the real UK file, point-in/zone-intersection, height cut, class filter, multi-file
load.

Gaps worth filling: an automated end-to-end Planner→Scorer integration test (plan
a task, fly a synthetic perfect trace, confirm the scoring speed) — currently done
manually via `score_all.py`; and a `Style=Fixed` (A12) sector-direction test.

## 8. Validation dataset (`data/`)

- `CR2025_racer_tasks.cup` — the six flown Cloud Rally 2025 day tasks (DAY1–7,
  DAY5 not flown), with 10/20 km observation sectors and 5 km / 0.5 km barrels.
  This is the authoritative task geometry; parse zones from here rather than
  re-transcribing.
- `cr2025_racer_task1.cup`, `cr2025_racer_2.cup` — earlier single-task builds kept
  for reader/round-trip tests (??? placeholders and sector+barrel zones).
- `CR2025_racer1.tif`, `CR2025_racer_3B.tif` — the printed task sheets (A4 @
  300 dpi); PNG extracts are in `docs/task_sheets/`. `racer1` is Task 1: the
  turnpoint table, the per-handicap "Variable Barrel Sizes" table (Handicap |
  Radius | Act Dist | Hcp Dist — the optimiser/report validation target) and Task
  Properties (windicapped, wind 18 kt/265°, sector 10 km/90°). Read the TIFFs with
  PIL.
- `CR_IGC/DAY1..DAY7/` — flown IGC traces, `<flightId>_<compNumber>.igc`. All six
  flown days are validated; per-day facts live in `CR_IGC/validation.json`.
- `CR_IGC/_work/DAY*/course.cup` — generated per-day courses (BGA waypoints + that
  day's task block) that the SVG generator and harness read.
- `entrants_cloud_rally_2025.csv` (full) and `_short.csv` (the Planner's
  `Type,Registration,CompNumber,Handicap` schema).
- `score_all.py` — the validation harness (see §3 for how to run it).
- `docs/task_sheets/` — PNG extracts of the scanned sheets (incl. the cropped
  barrel table); `docs/tasks_svg/` — the SVG task diagrams + their generator.

## 9. How D_Ref is calculated, and deriving VRefCru

### D_Ref — the reference glider's effective (corner-cut) distance

`FleetPlan.ReferenceDistanceKm` is computed by `BarrelOptimizer.Optimize`
(line ~76) as `EffectiveDistanceKm(course, geometry, refRadii)`, where `refRadii`
puts the barrel at **R_min (0.5 km) at every variable turnpoint** — i.e. the
distance the highest-handicap reference glider physically flies.

The formula (`BarrelOptimizer.EffectiveLegDistances`):

```
D_Ref = Σ (centre-to-centre leg length)  −  Σ (corner-cut saving at each interior point)
```

The corner-cut at an interior point i with barrel R and track deflection Δφ_i is
`2·R·sin(Δφ_i/2)` (dht.md §3.2), split as `R·sin(Δφ/2)` onto each adjacent leg.
Note this sum runs over **all** interior points (including fixed checkpoints,
which keep their default barrel), not only the variable turnpoints — a checkpoint
is physically rounded too, so it shortens the flown path.

Worked example, Cloud Rally DAY1 (WGS84 distances):

| | km |
|---|---|
| centre-to-centre total (5 legs) | 234.92 |
| − corner cut at R_min across the 4 interior points (all shallow, 147–173°) | 3.92 |
| **= D_Ref** | **231.00** |

> **Note on the published scoring D_Ref.** The scorer's `score_all.py` uses the
> *published* daily constant (`221.93` for Task 1) — the distance SoaringSpot shows
> every finisher at, regardless of handicap (the DHT signature). This differs from
> the Planner's computed 231.0: the published value reflects the actual task as
> flown/scored on the day (a slightly different reference radius / task-sheet
> figure), whereas the Planner recomputes from the course geometry at R_min. Both
> are "the reference glider's effective distance"; the scorer takes the published
> one as ground truth so scoring speed matching does not depend on the Planner.
- **VRefCru ≈ 90 km/h.** The sheet gives the reference glider (H114) an air-mass
  distance ("Act Dist") of 256.4 km over a 230.3 km ground path. air/ground =
  256.4/230.3 = 1.113; solving the wind triangle (wind 18 kt/265°) for the airspeed
  that yields that ratio gives Va(H114) ≈ 103 km/h, hence VRefCru = Va/1.14 ≈ 90.
  Feeding `--vref 90` reproduces the whole per-handicap barrel table to ~0.02 km;
  the 130 default gives a systematic ~10 % undersize.
- A useful cross-check: the sheet's "Hcp Dist" column ≈ 225 km constant across all
  handicaps. That is *time* equalisation (Hcp Dist = VRefCru·T), the same criterion
  the engine uses — not a different distance-equalisation scheme.

## 10. Branch / git state

- Branch `feature/task-diagrams-outward-sectors`, pushed to `origin`.
- Recent commit sequence on this branch:
  1. Symmetric sectors open outward; add task-diagram SVG renderer + Planner `--svg`.
  2. Uniform barrel radius per handicap; emit the task-sheet Variable Barrel Sizes table.
  3. Switch `Geodesy` to WGS84 (Vincenty) — closes the ~0.3% distance gap.
  4. (this) handover doc update.
- No open PR; create one at
  https://github.com/rbp28668/Trace/pull/new/feature/task-diagrams-outward-sectors
