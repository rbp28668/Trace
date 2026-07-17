# Data Management Application — Plan

A .NET web application for managing competitions, competition classes, gliders,
pilots, task days and tasks, plus DHT task planning and scoring. It reuses the
existing `Trace.Core` domain and engines; the standalone `Trace.Planner` and
`Trace.Scorer` CLIs continue to work unchanged.

## 1. Guiding decisions

- **Reuse, don't fork.** The web app references `Trace.Core` for the domain
  value types (`Glider`, `Fleet`, `Course`, `ObservationZone`, `Task`) and calls
  the existing planning (`BarrelOptimizer`/`CourseGeometry`) and scoring
  (`ScoringEngine`) code in-process. The CLIs stay as thin shells over the same
  library.
- **Persistence via a DTO/entity layer, not the domain classes.** `Trace.Core`
  domain types are immutable (constructor-set, get-only), which is right for the
  engines but wrong for EF change-tracking and Razor model binding. So EF maps to
  a separate set of **persistence entities** (mutable POCOs). Mappers convert
  entities ⇆ `Trace.Core` domain objects when invoking the engines. This is the
  "dto layer" requested and keeps EF concerns out of the pure library.
- **Stack:** .NET 10, ASP.NET Core **Razor Pages** (no SPA), EF Core with
  **Npgsql** (PostgreSQL). Server-rendered pages + minimal progressive
  enhancement (plain JS/fetch only where needed, e.g. task diagram preview).
- **One competition active at a time, schema supports many.** A `Competition`
  root owns everything, so historical competitions are retained and a future
  multi-competition UI is a small change.
- **No authentication yet.** Trusted-LAN deployment. Structure the app so an
  ASP.NET Core Identity layer can be added later without reshaping pages.

## 2. Solution structure

Two new projects added to `src/Trace.sln`:

```
src/
  Trace.Core/         (existing) domain + engines + I/O — unchanged
  Trace.Planner/      (existing) CLI — unchanged
  Trace.Scorer/       (existing) CLI — unchanged
  Trace.Tests/        (existing) + tests for the new mapping/repo layer
  Trace.Data/   NEW    class library: EF entities, DbContext, migrations,
                       repositories, entity⇆domain mappers
  Trace.Web/    NEW    ASP.NET Core Razor Pages app; references Trace.Data + Trace.Core
```

`Trace.Web` depends on `Trace.Data` and `Trace.Core`. `Trace.Data` depends on
`Trace.Core` (for mapping and for the engines). The existing projects gain no new
dependencies.

## 3. Domain / persistence model (`Trace.Data/Entities`)

Derived from `docs/LogicalClasses.drawio`, plus a `Competition` root and the
fields the engines need (handicap anchor `VRefCru`, wind, barrel bounds). All
entities carry an `int Id` surrogate PK; natural keys get unique indexes.

- **Competition** — `Name`, `Site`, `StartDate`, `EndDate`, `IsActive`. Owns
  Classes and Days.
- **CompetitionClass** — `Name` (e.g. "Racing", "Club"), `VRefCruKmh` (that
  class's fleet anchor at H=100), FK→Competition. A class scopes its own fleet
  (gliders/pilots), its per-day tasks, and its reference cruise speed. *(VRefCru
  moved here from Competition — each class fleet has its own anchor; migration
  `MoveVRefCruToClass`.)*
- **Pilot** — `Name`, `AccountNo`. Shared across classes within a competition.
- **Glider** — `CompNo`, `Registration`, `Type`, `Handicap`, `ICAO`,
  FK→CompetitionClass. Mirrors `Trace.Core.Model.Glider` + identity/registration.
- **Logger** — `Type`, `LoggerId`, FK→Glider (backup loggers ⇒ collection).
- **CompetitionEntry** — links a Pilot to a Glider within a Class for the whole
  competition ("which pilot flies which glider(s) in which class"). Fields:
  FK→CompetitionClass, FK→Pilot, FK→Glider, optional crew/`P2`.
- **Day** — `DayNo`, `Date`, FK→Competition. A calendar competition day.
- **Task** — `Name`, `Index`, `Active`, `TaskType` (A/B/C), FK→Day,
  FK→CompetitionClass, ordered `Turnpoints`, plus planning inputs
  (`WindDirDeg`, `WindSpeedKmh`, `RefHandicap`) and outputs (`DRefKm`, `TRefSec`,
  per-handicap barrel radii — stored as child rows, see below).
  **Invariant (from the diagram note): at most one `Active` task per class per
  day (zero if scrubbed).** Enforced with a filtered unique index and in the
  service layer.
- **Turnpoint** — `Index`, `Waypoint` (name), `Latitude`, `Longitude`,
  `IsCheckpoint`, `IsLine`, `Style`, `DirectionType`, `Radius1`, `Angle1`,
  `Radius2`, `Angle2`, FK→Task. Maps to/from `ObservationZone` + `CoursePoint`.
- **DayEntry** — "which pilot is flying which aircraft in what class on a given
  day": FK→Day, FK→CompetitionClass, FK→Pilot, FK→Glider, FK→Task (the active
  task flown), and `Flight` (0..1).
- **Flight** — `Start`, `Finish`, `Time`, `Speed`, `Distance`, `AirspaceValid`,
  `AirspaceChecked`, FK→DayEntry, FK→IGCFile.
- **IGCFile** — stored uploaded log: `FileName`, `ContentType`, raw bytes or a
  path, `UploadedUtc`, FK→Flight (or DayEntry).
- **BarrelRadius** (child of Task) — `Handicap`, `TurnpointIndex`, `RadiusKm`:
  the optimiser's per-handicap output, so a per-handicap `.cup` can be
  regenerated/exported.

### Mapping layer (`Trace.Data/Mapping`)
Pure functions converting persistence entities ⇆ `Trace.Core` domain:
`ToFleet(class)`, `ToCourse(task)`, `ToScoringTask(task, handicap)`, and back
(`ApplyPlanResult`, `ApplyScore`). Engines are invoked only through these, so the
web app never hand-rolls domain construction.

## 4. Data access (`Trace.Data`)

- `TraceDbContext : DbContext` — `DbSet` per entity; Fluent API in
  `OnModelCreating` for keys, indexes (the filtered "one active task" index),
  cascade rules, decimal precision for lat/long/handicap.
- **Repositories / services** — a thin service per aggregate
  (`CompetitionService`, `ClassService`, `FleetService`, `TaskService`,
  `ScoringService`) exposing task-oriented methods used by pages. Keeps EF queries
  out of page models and gives a seam for validation (e.g. the active-task rule).
- **Migrations** — EF Core migrations checked in; `dotnet ef database update`
  on startup in dev, explicit in prod.
- **Connection string** from configuration/user-secrets/env var
  (`ConnectionStrings:Trace`).

## 5. Import / export (reuse `Trace.Core.Io`)

- **Fleet import** — upload the fleet CSV (`Type, Registration, CompNumber,
  Handicap`) via `FleetReader`; preview + commit into a class's gliders. Also
  support the entrants CSV (`data/entrants_cloud_rally_2025.csv`).
- **Pilot import** — from entrants CSV (name/comp-no) where present.
- **Course/task import** — upload a `.cup` task via `CupReader` → create a `Task`
  with its `Turnpoint`s and `ObservationZone`s.
- **Export** — per-handicap `.cup` task files via `CupWriter`; task sheet/diagram
  via the existing `TaskDiagram`/rendering. Export the fleet CSV back out.

## 6. Web front end (`Trace.Web`, Razor Pages)

Page areas (folders under `Pages/`), each a list + create/edit/delete:

1. **Competitions & classes hub** (`/Competitions`) — the single screen for basic
   competition and class management: inline add/edit/delete/activate of
   competitions, and inline add/edit/delete of the classes within each (including
   per-class `VRefCruKmh`). No separate Competition Create/Edit or Classes pages —
   everything is on this one hub. The dashboard (`/`) shows the active competition
   and its classes and links here. *(Both bound view models — `CompetitionInput`,
   `ClassInput` — post on every submit, so each handler calls `ValidateOnly(model,
   prefix)` to validate just the model it uses.)*
3. **Fleet** (`/Gliders?classId=`) — list/create/edit gliders; CSV import
   with preview; attach loggers; edit handicaps.
4. **Pilots** — CRUD; CSV import; assign pilots to gliders via
   **CompetitionEntry** editor.
5. **Days** — list competition days; add a day (DayNo/Date).
6. **Tasks** (`/Tasks?dayId=&classId=`) — list the A/B/C tasks for
   a class on a day; create/edit a task (import `.cup` or build turnpoints);
   mark one **Active**; run the **Planner** to compute per-handicap barrels
   (`DRef`/`TRef`) and preview the task diagram; export `.cup`.
7. **Day entries & scoring** — per day/class: who flew what; upload IGC per
   entry; run the **Scorer** to compute finisher speed / land-out distance;
   show results table. (Later phase — scaffold now, wire engine after tasks.)

UI conventions: shared `_Layout` with a competition context header; server-side
validation via data annotations + service checks; Bootstrap (bundled with the
Razor Pages template) for tables/forms; TempData flash messages; no SPA.

## 7. Testing (`Trace.Tests`)

- **Mapping round-trips** — entity → `Fleet`/`Course`/`ScoringTask` → entity
  loses nothing material.
- **Active-task invariant** — service rejects a second active task for the same
  class/day; filtered index enforced (integration test against a test DB, e.g.
  Testcontainers Postgres or a throwaway schema).
- **Import** — fleet CSV and `.cup` import produce the expected entities
  (reuse `data/sample_fleet.csv`, `data/cr2025_racer_task1.cup`).
- **Planner/Scorer integration** — a class + course through the mapping layer
  reproduces the CLI's per-handicap barrels and scores (cross-check against the
  values already validated in memory: Cloud Rally 2025 Task 1).

## 8. Phased milestones

1. **Scaffold** ✅ *(done)* — added `Trace.Data` (classlib) + `Trace.Web`
   (Razor Pages `webapp`) to `Trace.sln`. EF Core 10.0.10 + Npgsql
   10.0.3 in `Trace.Data`; `EntityFrameworkCore.Design` in both `Trace.Data` and
   `Trace.Web` (startup project). Empty `TraceDbContext` registered in
   `Program.cs` with connection string `ConnectionStrings:Trace`; design-time
   `TraceDbContextFactory` added. Full solution builds; `dotnet ef` discovers the
   context. `dotnet-ef` global tool updated to 10.0.10.
2. **Model + migrations** ✅ *(done)* — entities §3 under
   `Trace.Data/Entities` (the task entity is named `CompetitionTask` to avoid
   clashing with `System.Threading.Tasks.Task`/`Trace.Task`), full
   `OnModelCreating` fluent config (natural-key unique indexes, cascade rules,
   and the two filtered unique indexes: one active competition, one active task
   per class per day). `InitialCreate` migration generated and applied to the
   `trace` database on local Postgres.
3. **Competitions + Classes** ✅ *(done)* — `CompetitionService` (atomic
   single-active transition) + `ClassService`; dashboard (`/`) showing the active
   competition + its classes; nav + TempData flash in `_Layout`. Verified
   end-to-end against Postgres: create→activate→list→dashboard flow works and the
   single-active invariant holds. (Note: bare `async Task` returns must be fully
   qualified — see the `Trace.Task` collision.) **Later consolidated** (see the
   restructure note below) into a single `/Competitions` hub doing inline
   competition + class CRUD; the standalone `Competitions/Create|Edit` and
   `Pages/Classes/*` pages were removed. Nav's Competitions link is right-aligned.
4. **Fleet + Pilots** ✅ *(done)* — `FleetService`, `FleetImportService`,
   `PilotService`, `EntryService`. Razor Pages: Gliders (`/Gliders?classId=`)
   list/create/edit with logger add/remove; CSV import (`/Gliders/Import`) with a
   preview→commit step handling both the `FleetReader` layout and the entrants
   layout (`CompNumber,Pilot,Club,Type,Class,Handicap`), auto-creating pilots and
   linking entries; Pilots (`/Pilots`) CRUD; Entries (`/Entries?classId=`) pairing
   pilot↔glider with an available-gliders dropdown that excludes already-entered
   gliders. Class list links to Fleet/Entries; Pilots added to nav. Verified
   end-to-end against Postgres: imported the real Cloud Rally entrants CSV (27
   gliders), pilot auto-creation + entry linkage, and the glider-exclusion rule.
5. **Days + Tasks** ✅ *(done)* — `DayService`, `TaskService` (atomic
   single-active-task transition + `ScrubAsync` + `ReplaceTurnpointsAsync`),
   `TaskImportService` (static; parses `.cup` via `CupReader` + `CupTaskLayout.Trim`
   so ObsZone indices align, maps to `Turnpoint` rows, flags small interior zones
   as checkpoints — mirrors `CourseReader`). Razor Pages: Days
   (`/Days?competitionId=`) CRUD with per-class task links; Tasks
   (`/Tasks?dayId=&classId=`) list with activate/scrub/delete, manual Create, JS
   turnpoint editor on Edit (add/remove rows, planning-input fields, D_Ref/T_Ref
   readout), and `.cup` Import (preview→commit). Verified end-to-end against
   Postgres: imported Cloud Rally Task 1 (6 turnpoints, correct R1/R2), and the
   one-active-task-per-class-per-day rule holds across activate/switch/scrub.
6. **Planner integration** ✅ *(done)* — `PlanningMapper` (entity→`Course`/`Fleet`
   + `Turnpoint`→`ObservationZone`); `PlanningService.PlanAsync` runs
   `BarrelOptimizer`/`CourseGeometry` in-process, stores `DRefKm`/`TRefSec` +
   per-(handicap,turnpoint) `BarrelRadius` rows, and `ExportCupAsync` re-emits a
   standard `.cup` per handicap via `CupWriter` (variable turnpoints resized with
   `ObservationZone.WithBarrel`, start/finish/checkpoint zones untouched). Tasks
   Edit page gained a **Run planner** button (D_Ref/T_Ref readout) and per-handicap
   `.cup` **Export** downloads. Verified against Postgres on Cloud Rally Task 1:
   D_Ref = 231.0 km; reference H114 gets R_min (500 m) barrels, H95 expands to
   ~7059 m, the shallow turnpoint stays fixed — matching the CLI's DHT behaviour.
   **Task-diagram SVG preview** ✅ *(wired)* — `PlanningService.RenderDiagramAsync`
   calls `TaskDiagram.Render` (Core) on the mapped `Course`; a `Diagram` GET handler
   on Tasks/Edit returns `image/svg+xml`, embedded via an `<img>` with a handicap
   selector that swaps between the as-set barrels and each planned handicap's
   optimised barrels. Verified: as-set shows 0.5/5.0 km barrels, H95 shows 8.8 km,
   empty tasks 404 gracefully.
7. **Day entries + Scorer integration** ✅ *(done)* — `IgcStorage` (files on disk
   under a configurable root `Igc:RootPath`, tree `class_{id}/day_{id}/`; DB stores
   the relative path + metadata); `DayEntryService` (seed day entries from class
   entries, list with flights); `ScoringService` (upload → store; score by
   re-emitting the planned per-handicap `.cup` via `PlanningService.ExportCupAsync`,
   parsing it with `ScoringTask.FromCup`, and running `ScoringEngine` in-process).
   DayEntries page (`/DayEntries?dayId=&classId=`): seed, per-entry IGC upload,
   Score / Score-all, results (finisher speed / land-out distance). Verified
   end-to-end against Postgres with real Cloud Rally DAY1 logs: with VRefCru=90 and
   wind 265/33.3 the app reproduces the validated H95 barrel (8811 m) and scores
   4X/909/700 as finishers, matching the standalone CLI (integration confirmed by
   scoring the app's exported cup through `Trace.Scorer` — identical result). Fixed
   a real bug found during verification: the turnpoint editor now round-trips zone
   `Style` (start-line/finish) via a hidden field instead of resetting it to 1.

   *Known nuance (not a scoring bug):* the app's `D_Ref` (231 km, geometric at
   R_min) differs from the validated run's 221.93 km, so app speeds are a few km/h
   higher than published. This is a `D_Ref`-derivation difference in the shared
   engine, independent of the data-app work.
8. **Hardening** ✅ *(done)* — friendly duplicate-key validation on
   competition/class names and day numbers (`NameExistsAsync`/`DayNoExistsAsync`
   surfaced as inline `ModelState` errors instead of 500s); custom Error page with
   404/403/500 messaging via `UseStatusCodePagesWithReExecute` +
   `UseDeveloperExceptionPage` in dev; startup `db.Database.Migrate()` so a fresh
   deployment self-provisions; leftover template Privacy page repurposed as About,
   footer/nav updated; explicit Identity seam comment in `Program.cs`
   (register Identity + `UseAuthentication()` before `UseAuthorization()`). Verified
   in Production mode: startup migrate, 404 page, duplicate-name validation, and the
   84 `Trace.Tests` still pass.

### Post-milestone restructure ✅ *(done)*
Following review feedback:
- **Single management hub.** Competitions and their classes are now created,
  edited and deleted only on `/Competitions` (inline forms per card). Removed the
  standalone `Competitions/Create`, `Competitions/Edit` and the whole
  `Pages/Classes/*` folder; repointed all links/breadcrumbs (dashboard,
  Days/Entries/Gliders) at the hub.
- **`VRefCruKmh` moved from Competition → CompetitionClass** (migration
  `MoveVRefCruToClass`) so each class fleet has its own H=100 anchor; the hub's
  class forms edit it and `PlanningService` reads it from the class. *Existing
  class rows default to 0 on upgrade — set each class's VRefCru once after
  migrating (Cloud Rally validation needs 90).*
- **Nav.** The Competitions link is right-aligned in the navbar.
- Re-verified end-to-end: hub CRUD works, and planning still reproduces the
  validated H95 barrel (8811 m) reading VRefCru from the class; 84 tests pass.

### Data-model & task-sheet round (branch `feature/data-management-app`) ✅ *(done)*
Feature work after the initial app commit; migrations applied to the dev DB and
all 84 `Trace.Tests` still pass. Verified end-to-end in the running app.

- **Entries reshaped: glider + ordered pilot roster.** The old "Fleet" concept
  (a standalone `Gliders` page) is gone. A `CompetitionEntry` is now a glider plus
  an **ordered roster of one or more pilots** (join entity `EntryPilot`, `Order` 0
  = primary/P1); `DayEntry` gained an optional `P2PilotId` so a day flight can be
  flown by 1 (max 2) of the roster. The entry Create/Edit screen owns the glider
  fields, the pilot roster, and loggers (the retired Fleet pages folded in).
  Deleting an entry also deletes its glider unless a `DayEntry` still references it.
  Migration `EntryPilotRosterAndDayP2` (copies old primary→order 0, P2→order 1
  before dropping the columns). Files: `CompetitionEntry`, `EntryPilot`, `DayEntry`,
  `Pilot` entities; `EntryService.SetPilotsAsync`; `Pages/Entries/*`.
- **Turnpoint editor: A2 column.** `Tasks/Edit` had A1 but no A2, and `TurnpointRow`
  didn't carry `Angle2` — so every save silently zeroed the DB column. Added the
  A2 (°) column through the whole path (row model, load, save, add-row JS) and to
  the `Tasks/Import` preview. The CUP importer already mapped A2 correctly; the loss
  was editor-only.
- **Per-competition waypoint list.** New `CompetitionWaypoint` entity + `Waypoints`
  page per competition (upload a `.cup`, parsed by the existing `CupReader`,
  de-duped by name; migration `CompetitionWaypoints`). The task editor's Waypoint
  field is now a **strict dropdown** constrained to that list, with read-only
  lat/lon resolved from the chosen waypoint on save (JS fills them live). The CUP
  **task** importer also resolves/validates turnpoint names against the list and
  refuses to import off-list names. Files: `WaypointService`, `Pages/Waypoints/*`,
  `Pages/Tasks/Edit`, `Pages/Tasks/Import`.
- **Diagram "Refresh from edits".** A button on `Tasks/Edit` re-renders the task
  diagram from the current (unsaved) form state via a POST handler
  (`OnPostDiagramPreviewAsync` → `PlanningService.RenderDiagramForTurnpoints`, no
  DB write), resolving coords from the waypoint list exactly like the save path.
- **Task sheet export (.docx).** New `TaskSheetDocx` (OpenXML;
  `DocumentFormat.OpenXml` added to `Trace.Data`) reproduces the DHT task-sheet
  layout from `data/CR2025_racer1.tif`: header, points table
  (Code/Name/Lat/Lon/Course/Dist/Type/Radius with WGS84 bearings & leg distances),
  the "Variable Barrel Sizes" table, and a Task Properties block. Notes / ATC
  frequencies / licensee are **editable placeholders** (no schema change). Gated on
  the task being **planned** (stored barrels); button on `Tasks/Edit` next to the
  `.cup` exports. `PlanningService` refactored so `ComputePlanAsync` is shared by
  `PlanAsync` and the sheet, so the barrel table always matches the stored plan.

## 9. Open items to confirm later

- **IGC storage** — DB bytea vs. filesystem path (large logs). Default: filesystem
  path with a configurable data root; DB stores metadata.
- **Handicap source** — supplied per glider (as today) vs. a shipped BGA lookup to
  default missing values. Default: supplied wins, optional lookup later.
- **Auth** — add ASP.NET Core Identity (roles: organiser/scorer/read-only) when
  the app leaves the trusted LAN.
