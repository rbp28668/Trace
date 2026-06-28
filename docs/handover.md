# DHT Engine — Handover

Status of the Distance Handicap Task (DHT) engine and scoring system as of the
`refactor/core-library` branch (7 commits ahead of `main`). All work is verified
against the real sample data in `data/`; the full test suite is **67 passing**.

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
                   CupReader, CupWriter, CourseReader, FleetReader
  Trace.Planner/   task-optimisation executable
  Trace.Scorer/    scoring executable (legacy IGC dump kept as --dump)
  Trace.Tests/     xUnit tests (67)
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
| CUP export | §5.1 | `CupWriter`, `PlanWriter` | standard ObsZone lines, one file per handicap |
| Airspace guardrail | §5.2 | `AirspaceGuard` | hard error blocks publication |
| OpenAir read, multi-file | airspace.md | `OpenAirReader`, `AirspaceLoader` | AC/AN/AL/AH/DP/DC/V/DB |
| Point-in-airspace | airspace.md | `AirspaceChecker` | nearest-N cache, class filter |
| Zone-intersects-airspace | airspace.md | `AirspaceChecker` | max-height cut, class filter |

### Verified against real data
- Planner: 4-glider fleet on a Lasham triangle converges all gliders to T_Ref
  within ±5 s; output `.cup` re-parses as valid CUP.
- Planner guardrail: blocks publication when sample barrels clip London TMA /
  Cotswold / Daventry CTAs in `data/uk2026-06-11.txt`.
- Scorer: `--dump` and zone-transit detection on the 9,521-fix `659VF4L1.igc`;
  infringement scan reports cleanly.

## 5. Known limitations / decisions made

- **Scorer handles cylinder zones only.** Transit detection is distance ≤ radius.
  DHT tasks are all cylinders, but arbitrary *declared* IGC tasks often use FAI
  **sectors** (bearing-limited quadrants) — not yet supported.
- **Fixed leg bearings in the optimiser.** Legs are treated centre-to-centre; the
  marginal bearing change as barrels grow is absorbed by the convergence loop
  rather than re-derived each iteration (dht.md §4.1 allows either).
- **Spherical geodesy**, not WGS84/Vincenty. Accurate to ~0.3%, ample at task
  scale; a chosen deferral.
- **Equal ΔD allocation** across variable turnpoints (the spec default).
  Geometry-weighted allocation is not yet implemented.
- **A wide handicap spread can be geometrically infeasible** on a course with few
  or shallow turnpoints (one 90° corner at R_max≈12 km saves only ~16 km). The
  optimiser flags this as non-convergence rather than failing silently — it is
  inherent to the method, not a bug.
- **Handicaps are supplied per glider** in the fleet CSV (BGA scheme assumed);
  there is no built-in handicap-list lookup. `HRef` defaults to the highest in
  the fleet, overridable with `--href`.
- **"SUA" airspace label** from dht.md §5.2 was not confirmed for UK/Europe; the
  guardrail uses CTR/TMA/CTA/A/C/D as controlled classes (see `AirspaceLoader`).
- Build emits harmless `LF→CRLF` warnings (no `.gitattributes`); see below.

## 6. Suggested enhancements

Roughly in priority order.

1. **FAI sector observation zones** in the Scorer (and `CupReader`): honour
   `Style`/`A1`/`A2`/`A12` so declared sector tasks score correctly, not just
   cylinders.
2. **Iterative bearing re-derivation** in `BarrelOptimizer`: recompute leg
   bearings/ground speeds from actual barrel entry/exit points each iteration for
   sub-second precision on sharply-doglegged tasks.
3. **Optimiser feasibility report**: when a fleet/course can't converge, output
   the minimum extra turnpoints or the achievable handicap range, to guide
   task-setting.
4. **Geometry-weighted ΔD allocation**: distribute savings by turnpoint
   sharpness rather than equally, reducing the chance of hitting R_max.
5. **Fixed-size control points.** A turnpoint that all gliders must round at the
   same fixed radius (a mandatory routing/safety point that does not vary with
   handicap), e.g. to keep the fleet clear of a hazard or funnel it through a
   gap. The plumbing is mostly present — `CoursePointType.Checkpoint` exists and
   `BarrelOptimizer` already excludes it from variable sizing (keeps its default
   radius) — but it is **not yet reachable from input**: `CourseReader` only ever
   emits Start/Turnpoint/Finish. To finish it: let the course `.cup` mark a point
   as a control point (e.g. a naming convention or a per-point flag), have
   `CourseReader` assign `Checkpoint`, carry the fixed radius into `PlanWriter`'s
   per-handicap output, and treat it as a normal (fixed) cylinder in the Scorer.
6. **WGS84 geodesy** as an opt-in for certification-grade distances.
7. **Richer scoring**: start-height/finish-height limits, speed-points vs
   distance-points formulae, devaluation, multiple start gates.
8. **Engine-noise / motor-glider handling** (ENL extension is parsed but unused).
9. **Airspace report polish**: dedupe stacked sub-volumes in the Scorer output
   (the Planner already does); optionally report closest-approach distance.
10. **CI**: a GitHub Actions workflow running `dotnet test`; add `.gitattributes`
    (`*.cs text eol=lf`) to silence the line-ending warnings.
11. **Packaging**: `dotnet publish` single-file binaries; a short user guide for
    task-setters and scorers.

## 7. Test coverage

`Trace.Tests` (67 tests): geodesy vs published values; leg-geometry round-trips
and shallow-turn boundary; wind-triangle cases and guards; optimiser convergence
(nil wind + crosswind), bounds, distance-ratio vs handicap; CUP/fleet/course
parse and round-trip incl. the real 1,391-waypoint BGA file; scoring (finisher,
start-zone timing, land-out scaling); OpenAir parsing incl. the real UK file,
point-in/zone-intersection, height cut, class filter, multi-file load.

Gaps worth filling: an end-to-end Planner→Scorer integration test (plan a task,
fly a synthetic perfect trace, confirm the scoring speed), and sector-zone tests
once sectors land.

## 8. Branch / git state

- Branch `refactor/core-library`, 7 commits ahead of `main`, pushed to `origin`.
- PR not yet opened: https://github.com/rbp28668/Trace/pull/new/refactor/core-library
- Commit sequence: refactor → geometry/wind → CUP I/O → Planner → Scorer →
  airspace → docs.
