# DHT Engine — Handover

Status of the Distance Handicap Task (DHT) engine and scoring system as of the
`feature/seeyou-task-reading` branch (4 commits ahead of `main`, pushed to
`origin`). All work is verified against real sample data in `data/`; the full
test suite is **75 passing**.

**Headline result:** the Scorer reproduces the published SoaringSpot daily results
for the real Cambridge Cloud Rally 2025 Task 1 — **all 10 comparable finishers
match to 0.01 km/h** (see §4 "Verified against real data" and §9). Getting there corrected the observation-
zone model and start detection; those learnings are the most important content of
this document (§5, §9).

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
    Airspace/      OpenAirReader, AirspaceVolume, AirspaceChecker, AirspaceLoader
    Io/            IGCFile, TracePoint, TraceList, Task, TaskPoint, Extension,
                   CupReader, CupWriter, CourseReader, CupTaskLayout, FleetReader
  Trace.Planner/   task-optimisation executable
  Trace.Scorer/    scoring executable (legacy IGC dump kept as --dump)
  Trace.Tests/     xUnit tests (75)
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
  [--airspace ../data/uk2026-06-11.txt --max-height 10000] --out ../data/plan_out

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

`data/cr2025_racer_task1.cup` is the real Task 1 (9 Aug 2025); the flown traces
are in `data/CR_IGC/DAY1..DAY7/` as `<flightId>_<compNumber>.igc`. To re-run the
end-to-end validation for DAY1:

```sh
# 1. Generate the per-handicap task files
dotnet run --project Trace.Planner -- \
  --fleet ../data/entrants_cloud_rally_2025_short.csv \
  --course ../data/cr2025_racer_task1.cup \
  --wind 265/33.3 --vref 90 --href 114 \
  --out ../data/CR_IGC/DAY1/tasks

# 2. Score every trace and diff against the published speeds
python ../data/CR_IGC/score_all.py DAY1     # -> all finishers match to 0.01 km/h
```

Two inputs here are **not** derivable from the course file and were reverse-
engineered from the task sheet — get them wrong and the numbers drift:
- **`--vref 90`** (H100 reference cruise airspeed) — see §9. The 130 default is
  wrong for this contest; 90 km/h reproduces the day's barrels to ~0.02 km.
- **`--dref 221.93`** (in `score_all.py`) — the published scoring distance, which
  is the reference glider's *effective* (corner-cut) distance, **not** the 234.92 km
  centre-to-centre task length.

## 4. What is implemented (by spec section)

| Area | Spec | Where | Notes |
|------|------|-------|-------|
| Great-circle distance/bearing | §3.1 | `Geodesy` | Haversine; spherical (WGS84 deferred) |
| Deflection/vertex angle, distance saved | §3.1–3.2 | `LegGeometry` | `2R·sin(Δφ/2)`, inverse, θ>150° reject |
| Airspeed scaling, wind triangle | §3.3 | `Windicap` | `√(Va²−Wx²)−Wh`, `|Wx|≤Va` guard, 0.4·Va override |
| D_Ref / T_Ref | §3.4 | `TaskMetrics`, `BarrelOptimizer` | |
| Barrel optimisation | §4.1 | `BarrelOptimizer` | bisection to ±5 s, equal allocation, [R_min,R_max] |
| Scoring — finishers | §4.2 A | `ScoringEngine` | T_Act, V_Score = D_Ref/T_Act |
| Scoring — land-outs | §4.2 B | `ScoringEngine` | CPA marking, D_Final = D_Ach·(H_Ref/H) |
| Observation zones | — | `ScoringEngine`, `ObservationZone` | sector (R1/A1) + barrel (R2); Style direction; in-order visiting |
| Start line | — | `ScoringEngine` | line-crossing timing; last start-zone exit before TP1 |
| Task read (SeeYou) | §5.1 | `CupReader`, `CourseReader`, `CupTaskLayout` | ??? takeoff/landing, ObsZone=0=Start, per-TP Rmin/Rmax in userdata |
| CUP export | §5.1 | `CupWriter`, `PlanWriter` | verbatim passthrough; only the barrel radius changes per handicap |
| Airspace guardrail | §5.2 | `AirspaceGuard` | hard error blocks publication |
| OpenAir read, multi-file | airspace.md | `OpenAirReader`, `AirspaceLoader` | AC/AN/AL/AH/DP/DC/V/DB |
| Point-in-airspace | airspace.md | `AirspaceChecker` | nearest-N cache, class filter |
| Zone-intersects-airspace | airspace.md | `AirspaceChecker` | max-height cut, class filter |

### Verified against real data
- **End-to-end vs a real competition (the strongest evidence):** Planner sizes the
  Cloud Rally 2025 Task 1 barrels to within ~0.02 km of the published per-handicap
  table, and the Scorer reproduces all 10 comparable finishers' scoring speeds to
  **0.01 km/h** (`data/CR_IGC/DAY1`, `score_all.py`).
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
- **Spherical geodesy**, not WGS84/Vincenty. Accurate to ~0.3% (our distances read
  ~0.3% under SoaringSpot's WGS84; bearings match to 0.1°). A chosen deferral.
- **Equal ΔD allocation** across variable turnpoints (the spec default).
  Geometry-weighted allocation is not yet implemented.
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
2. **Validate the other contest days.** DAY2–DAY7 traces are committed but only
   DAY1 has a transcribed task + published table. Add each day's entry to
   `DAYS` in `score_all.py` (course, wind, D_Ref, published speeds) and generate
   its task; a good regression net and a check that the model generalises.
3. **Derive `D_Ref` and default `VRefCru` automatically** rather than passing them
   by hand. `D_Ref` is the reference glider's effective distance the Planner
   already computes; the Scorer could read it from the task/report instead of a
   `--dref` flag. `VRefCru` could be inferred or documented per contest.
4. **Iterative bearing re-derivation** in `BarrelOptimizer`: recompute leg
   bearings/ground speeds from actual barrel entry/exit points each iteration for
   sub-second precision on sharply-doglegged tasks.
5. **Optimiser feasibility report**: when a fleet/course can't converge, output
   the minimum extra turnpoints or the achievable handicap range, to guide
   task-setting.
6. **Geometry-weighted ΔD allocation**: distribute savings by turnpoint
   sharpness rather than equally, reducing the chance of hitting R_max.
7. **WGS84 geodesy** as an opt-in for certification-grade distances (would close
   the ~0.3% distance gap vs SoaringSpot).
8. **Richer scoring**: start-height/finish-height limits, speed-points vs
   distance-points formulae, devaluation, multiple start gates.
9. **Engine-noise / motor-glider handling** (ENL extension is parsed but unused).
10. **Airspace report polish**: dedupe stacked sub-volumes in the Scorer output
    (the Planner already does); optionally report closest-approach distance.
11. **CI**: a GitHub Actions workflow running `dotnet test`; add `.gitattributes`
    (`*.cs text eol=lf`) to silence the line-ending warnings.
12. **Packaging**: `dotnet publish` single-file binaries; a short user guide for
    task-setters and scorers.

Now **done** (were on this list): FAI sector observation zones (§5); fixed-size
control points — `CourseReader` classifies a sub-1 km barrel as `Checkpoint` and
carries per-TP Rmin/Rmax from waypoint userdata JSON.

## 7. Test coverage

`Trace.Tests` (75 tests): geodesy vs published values; leg-geometry round-trips
and shallow-turn boundary; wind-triangle cases and guards; optimiser convergence
(nil wind + crosswind), bounds, distance-ratio vs handicap, per-turnpoint bounds;
CUP/fleet/course parse and round-trip incl. the real 1,391-waypoint BGA file, the
SeeYou gold-standard task, ??? placeholders and userdata Rmin/Rmax; scoring
(finisher, start-zone timing, land-out scaling, sector-vs-barrel achievement,
in-order turnpoints); OpenAir parsing incl. the real UK file, point-in/zone-
intersection, height cut, class filter, multi-file load.

Gaps worth filling: an automated end-to-end Planner→Scorer integration test (plan
a task, fly a synthetic perfect trace, confirm the scoring speed) — currently done
manually via `score_all.py`; and a `Style=Fixed` (A12) sector-direction test.

## 8. Validation dataset (`data/`)

- `cr2025_racer_task1.cup` — the real Cloud Rally 2025 Task 1, hand-built from the
  BGA turnpoint database and the TIFF task sheet. Encodes the 10 km observation
  sectors (A1=135) with 5 km / 0.5 km barrels.
- `cr2025_racer_2.cup` — a second SeeYou-generated gold-standard task (with ???
  placeholders and sector+barrel zones) used for reader/round-trip tests.
- `CR2025_racer1.tif`, `CR2025_racer_3B.tif` — the printed task sheets (A4 @
  300 dpi). `racer1.tif` is Task 1: the turnpoint table, the per-handicap "Variable
  Barrel Sizes" table (the optimiser validation target) and Task Properties
  (windicapped, wind 18 kt/265°, sector 10 km/90°). Read with PIL.
- `CR_IGC/DAY1..DAY7/` — flown IGC traces, `<flightId>_<compNumber>.igc`. Only DAY1
  (Task 1, 9 Aug 2025) is currently transcribed for validation.
- `entrants_cloud_rally_2025.csv` (full) and `_short.csv` (the Planner's
  `Type,Registration,CompNumber,Handicap` schema).
- `score_all.py` — the validation harness (see §3 for how to run it).

## 9. Deriving VRefCru and D_Ref (worked example)

Both were reverse-engineered from the Cloud Rally Task 1 sheet; the method
generalises to any windicapped task where you have a reference glider's figures.

- **D_Ref = 221.93 km.** The published daily results show *every* finisher at the
  same 221.93 km distance regardless of handicap — the DHT signature (the handicap
  is in the geometry, so all finishers fly the same scored distance and raw speed
  *is* the score). That constant is D_Ref. It is the reference glider's effective
  corner-cut distance, below the 234.92 km centre-to-centre length.
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

- Branch `feature/seeyou-task-reading`, 4 commits ahead of `main`, pushed to
  `origin`. (Supersedes the earlier `refactor/core-library` work, now merged.)
- Commit sequence: SeeYou task reading + per-TP barrel bounds → observation-zone
  scoring (validated to 0.01 km/h) → add IGC traces → per-day harness.
- No open PR; create one at
  https://github.com/rbp28668/Trace/pull/new/feature/seeyou-task-reading
