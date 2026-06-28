# Implementation Plan — DHT Engine & Scoring System

This plan implements the requirements in [`dht.md`](dht.md) and
[`airspace.md`](airspace.md). It favours **two separate executables** — a
**Planner** (task optimisation) and a **Scorer** (flight evaluation) — sharing a
single **core class library**.

## 1. Guiding decisions

- **Separation:** Planning and scoring are distinct workflows run at different
  times by different people (a task-setter before the day; a scorer after).
  Keeping them as separate programs keeps each CLI small and avoids coupling task
  generation to log analysis. All domain logic they share lives in the library.
- **Reuse the existing primitives.** `IGCFile`, `TracePoint`, `TraceList`,
  `Task`, `TaskPoint`, `Extension` already parse/write IGC and are well
  documented. They move into the shared library largely unchanged.
- **Language/runtime:** stay on .NET 10, C#, `Nullable` + `ImplicitUsings`
  enabled, `namespace Trace.*` — matching `src/Trace.csproj`.
- **Units & coordinates:** internally normalise to **km** and **km/h** (per
  `dht.md` §2.3) and decimal degrees. Reuse the existing convention from
  `TracePoint`/`TaskPoint` (north +ve, east +ve, west −ve). All angles in degrees
  externally, radians internally for trig.

## 2. Solution structure

Convert the current single project into a solution of four projects:

```
src/
  Trace.Core/         class library — all shared domain + I/O + math
  Trace.Planner/      exe — DHT task optimisation (dht.md §3, §4.1, §5.1, §5.2)
  Trace.Scorer/       exe — flight scoring (dht.md §4.2)
  Trace.Tests/        xUnit test project covering Trace.Core
Trace.sln             references all four
```

The existing `src/Program.cs` IGC-dump utility is retained as a `--dump`
diagnostic mode inside `Trace.Scorer` (it already reads IGC + declared task).

## 3. `Trace.Core` — shared library

Organised into folders by concern. Existing classes marked *(move)*.

### 3.1 Domain models (`Model/`)
- `Glider` — `Type`, `Registration`, `CompNumber`, `Handicap` (H). Handicap is
  supplied per glider (BGA scheme); reference cruise speed is **derived**
  (`dht.md` §2.1), not stored.
- `Fleet` — collection of `Glider`, plus the engine parameter `VRefCru` (anchor
  at H=100).
- `Course` / `Turnpoint` — the baseline task geometry (`dht.md` §2.2): ordered
  waypoints with `Type` (Start/Turnpoint/Finish) and a baseline/default radius.
  Distinct from the IGC `Task` declaration, which stays as-is for log parsing.
- `ObservationZone` — zone geometry for a turnpoint: cylinder radius, or
  line/sector; the unit the Planner sizes and the Scorer tests against.
- `WindProfile` — `Direction` ω (deg true, FROM) and `Speed` W (km/h).
- `HandicapTask` — the optimiser output: per-handicap set of `ObservationZone`
  radii + `DRef`, `TRef`. Serialises to one `.cup` per handicap.

### 3.2 Geodesy & geometry (`Geometry/`)
- `Geodesy` — great-circle distance and true bearing (`dht.md` §3.1), spherical
  law of cosines with a WGS84 option; helpers for point-at-distance/bearing.
- `LegGeometry` — track deflection angle Δφᵢ and internal vertex angle θᵢ
  (§3.1); distance saved `D_saved = 2R·sin(Δφ/2)` (§3.2); inverse
  `R = D_saved / (2·cos(θ/2))` with the θ>150° shallow-turn rejection.
- `Cpa` — closest point of approach of a trace to a leg line, and projection onto
  the leg toward the next barrel edge (needed for land-out marking, §4.2 Case B).

### 3.3 Windicapping engine (`Wind/`)
- `Windicap` — airspeed scaling `Va(H)=VRefCru·H/100` (§3.3.1); wind angle
  γ=α−ω; crosswind/headwind components; leg ground speed
  `Vg = √(Va²−Wx²) − Wh` (§3.3.4) **with the `|Wx| ≤ Va` domain guard**; and the
  `W ≥ 0.4·Va` wind-override check (§5.2).
- `TaskMetrics` — `DRef`, `TRef = Σ Lk/Vg,k(HRef)` (§3.4) for the reference
  glider, and per-glider simulated duration given a set of radii.

### 3.4 File I/O (`Io/`)
- `IGCFile`, `TracePoint`, `TraceList`, `Task`, `TaskPoint`, `Extension` *(move)*.
- `CupReader` — parse SeeYou `.CUP` waypoints + task blocks (we already have
  `data/BGA TPs 2026-06-10.cup`); maps `ObsZone/Style/R1/A1/R2/A2/A12` lines to
  `ObservationZone`.
- `CupWriter` — emit a **standard** `.CUP` task file per handicap (`dht.md` §5.1),
  one fixed-radius `ObsZone` line per turnpoint, filename carrying the handicap
  (e.g. `task_h93.cup`). No invented fields.
- `FleetReader` — parse the fleet CSV (`Type, Registration, CompNumber,
  Handicap`) into `Fleet`/`Glider`.
- `HandicapList` *(optional)* — load a BGA handicap table keyed by glider type,
  used only to default/validate a handicap when a fleet row omits it; supplied
  values always win.

### 3.5 Airspace (`Airspace/`) — implements `airspace.md`
- `OpenAirReader` — parse OpenAir files (read-only); support loading **multiple**
  files (base + competition-specific). Classes/heights retained.
- `Airspace` — geometry of one zone with class and vertical limits.
- `AirspaceChecker` — stateful checker per `airspace.md`:
  - **Point-in-airspace** for trace infringement, optimised by caching the
    nearest N zones across consecutive (geographically close) fixes; filterable
    by airspace class.
  - **Zone-intersects-airspace** for validating barrels, with a max-height cut so
    airspace above the zone ceiling is ignored.

## 4. `Trace.Planner` — task optimisation program

CLI: `Trace.Planner --fleet fleet.csv --course course.cup --wind 270/30 [--vref 130] [--href 120] [--airspace base.txt,comp.txt] --out outdir/`

Fleet CSV columns: `Type, Registration, CompNumber, Handicap`. `--href` defaults
to the highest handicap in the fleet.

Pipeline (maps directly to `dht.md` §3–§5):
1. Load fleet, course, wind, optional airspace.
2. Compute leg geometry (distances, bearings, deflection/vertex angles).
3. Reject shallow turnpoints (θ>150°); identify variable turnpoints.
4. Reference metrics: assign `RMin` to the reference glider, compute `DRef`,
   `TRef`.
5. For each lower-H glider: deficit `ΔD = DRef − DTarget(H)`; allocate ΔD across
   variable turnpoints; **iterative convergence** (bisection/Newton-Raphson) on
   each radius until simulated duration matches `TRef` within ±5 s, clamped to
   `[RMin, RMax]`.
6. Guardrails (§5.2): wind-override warning/downgrade; **airspace
   zone-intersection** check on the largest barrels → hard error blocks
   publication.
7. Emit one standard `.CUP` task file per handicap + a summary report
   (radii, DRef, TRef, warnings).

## 5. `Trace.Scorer` — scoring program

CLI: `Trace.Scorer --task task_h93.cup --igc flight.igc [--airspace base.txt] [--dump]`

Pipeline (maps to `dht.md` §4.2):
1. Load the per-handicap task (barrel radii) and the pilot's IGC trace
   (`IGCFile`).
2. Detect zone transits in order: start-line exit, each turnpoint cylinder
   intercept at its personalised radius, finish.
3. **Case A (finisher):** `T_Act` = finish-entry − start-exit;
   `V_Score = DRef / T_Act`. No post-hoc handicap division.
4. **Case B (land-out):** last achieved `TPj`; CPA marking point on the
   uncompleted leg; `D_Ach` along the personalised path;
   `D_Final = D_Ach · (HRef/H)`.
5. Optional airspace infringement scan over the trace (point-in-airspace).
6. Output per-pilot result (finisher/land-out, time, speed/distance, score)
   and any infringements; `--dump` reproduces the current IGC summary.

## 6. Testing strategy (`Trace.Tests`, xUnit)

- **Geodesy:** known great-circle distances/bearings vs reference values.
- **Geometry:** `D_saved` ↔ radius round-trip; θ=180° divergence; shallow-turn
  rejection boundary.
- **Windicap:** head/tail/crosswind ground-speed cases; `Va−W` at γ=0; domain
  guard and override threshold.
- **Optimiser:** synthetic 2–3 leg task converges so all gliders' simulated
  durations equal `TRef` within ±5 s; bounds clamping honoured.
- **CUP round-trip:** read `data/BGA TPs 2026-06-10.cup`; write/re-read a task
  file with no loss; output parses as valid standard CUP.
- **Scoring:** craft IGC traces (reuse `data/*.igc`) for a clean finish and a
  land-out; assert `V_Score` and `D_Final`.
- **Airspace:** point inside/outside known UK zones from `data/uk2026-06-11.txt`;
  barrel intersection with height cut; class filtering; multi-file load.

## 7. Phased milestones

1. **Refactor:** carve `Trace.Core` out of `src/`, add the four-project
   solution, move IGC classes, green build + a smoke test. *(no behaviour change)*
2. **Geometry + wind core** with unit tests (§3.1–§3.4).
3. **CUP reader/writer** + handicap list loading.
4. **Planner** end-to-end (no airspace) producing per-handicap `.cup` files.
5. **Scorer** end-to-end for Case A and Case B.
6. **Airspace** (OpenAir reader + checkers) wired into both guardrails and
   infringement scanning.
7. Hardening: error handling, reports, edge cases, docs.

## 8. Resolved decisions

- **Handicap scheme — BGA (baseline 100).** Handicaps are not looked up from a
  built-in table; instead the fleet input **supplies the handicap per glider
  directly**. Each fleet row is `type, registration, competition number,
  handicap` (see §3.1 `Glider` and §4 fleet CSV). A BGA handicap list may still
  ship as a convenience default, but the supplied value always wins.
- **Reference handicap `HRef` — Planner option, default highest in fleet.**
  `--href <value>` overrides; with no flag the Planner uses the highest H entered.
- **Fleet/course input — CSV fleet + `.cup` course.** Fleet CSV columns:
  `Type, Registration, CompNumber, Handicap`. Course is a standard SeeYou `.cup`
  task (reuses `CupReader`).
- **Geodesy — spherical now, WGS84 later.** Ship the spherical law of cosines
  (fast, adequate at task scale, keeps the convergence loop cheap); add an opt-in
  WGS84/Vincenty path later for certification-grade output.

## 9. Remaining items to confirm later

- **ΔD allocation:** spec default is equal split across variable turnpoints;
  consider geometry-weighted allocation as a later option.
- **`SUA` airspace label:** confirm correct class names for the target region
  (UK/Europe use CTR/TMA/CTA).
