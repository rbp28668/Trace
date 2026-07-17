using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;
using Trace.Data.Mapping;
using Trace.Io;
using Trace.Model;
using Trace.Planning;
using Trace.Rendering;
using DataGlider = Trace.Data.Entities.Glider;

namespace Trace.Data.Services;

/// <summary>Outcome of a planning run, surfaced to the page.</summary>
public class PlanResult
{
    public double ReferenceHandicap { get; init; }
    public double ReferenceDistanceKm { get; init; }
    public double ReferenceDurationHours { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int HandicapCount { get; init; }
}

/// <summary>
/// Runs the DHT barrel optimiser (<c>Trace.Core</c>) for a task and persists the
/// per-handicap barrel radii plus reference distance/time. Entities are converted
/// to domain objects via <see cref="PlanningMapper"/>; the engine is never fed
/// hand-built domain types.
/// </summary>
public class PlanningService
{
    private readonly TraceDbContext db;

    public PlanningService(TraceDbContext db) => this.db = db;

    /// <summary>
    /// Optimises the task's barrels against its class fleet and the competition's
    /// reference cruise speed, storing <c>DRefKm</c>/<c>TRefSec</c> and one
    /// <see cref="BarrelRadius"/> row per (handicap, turnpoint).
    /// </summary>
    public async Task<PlanResult> PlanAsync(int taskId)
    {
        CompetitionTask task = await LoadTaskAsync(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        double vRefCru = await db.CompetitionClasses
            .Where(cc => cc.Id == task.CompetitionClassId)
            .Select(cc => cc.VRefCruKmh)
            .FirstAsync();
        List<DataGlider> gliders = await db.Gliders
            .Where(g => g.CompetitionClassId == task.CompetitionClassId)
            .ToListAsync();
        if (gliders.Count == 0)
        {
            throw new InvalidOperationException(
                "The class has no gliders; import or add a fleet before planning.");
        }

        Course course = PlanningMapper.ToCourse(task);
        var geometry = new CourseGeometry(course);
        Fleet fleet = PlanningMapper.ToFleet(gliders, vRefCru);
        double href = task.RefHandicap ?? fleet.MaxHandicap;

        var optimizer = new BarrelOptimizer(new OptimizerOptions())
        {
            WindSpeed = task.WindSpeedKmh ?? 0.0,
            WindDirection = task.WindDirDeg ?? 0.0,
        };
        FleetPlan plan = optimizer.Optimize(course, geometry, fleet, href);

        // Persist reference metrics and the per-handicap radii. One row per
        // distinct handicap × turnpoint index (gliders sharing a handicap share
        // the same barrels).
        task.RefHandicap = href;
        task.DRefKm = plan.ReferenceDistanceKm;
        task.TRefSec = plan.ReferenceDurationHours * 3600.0;

        List<BarrelRadius> old = await db.BarrelRadii
            .Where(b => b.CompetitionTaskId == taskId)
            .ToListAsync();
        db.BarrelRadii.RemoveRange(old);

        var warnings = new List<string>();
        foreach (var group in plan.Plans.GroupBy(p => p.Glider.Handicap))
        {
            GliderPlan representative = group.First();
            for (int i = 0; i < representative.RadiiKm.Count; i++)
            {
                db.BarrelRadii.Add(new BarrelRadius
                {
                    CompetitionTaskId = taskId,
                    Handicap = group.Key,
                    TurnpointIndex = i,
                    RadiusKm = representative.RadiiKm[i],
                });
            }

            warnings.AddRange(representative.Warnings.Select(w => $"H{group.Key}: {w}"));
        }

        await db.SaveChangesAsync();

        return new PlanResult
        {
            ReferenceHandicap = href,
            ReferenceDistanceKm = plan.ReferenceDistanceKm,
            ReferenceDurationHours = plan.ReferenceDurationHours,
            Warnings = warnings,
            HandicapCount = plan.Plans.Select(p => p.Glider.Handicap).Distinct().Count(),
        };
    }

    /// <summary>Distinct handicaps with stored barrels for a task (descending).</summary>
    public async Task<List<double>> PlannedHandicapsAsync(int taskId) =>
        (await db.BarrelRadii
            .Where(b => b.CompetitionTaskId == taskId)
            .Select(b => b.Handicap)
            .Distinct()
            .ToListAsync())
        .OrderByDescending(h => h)
        .ToList();

    /// <summary>
    /// Exports the stored plan for one handicap as a standard .CUP task, reusing
    /// each turnpoint's source zone and substituting the optimised barrel radius on
    /// the variable turnpoints. Returns null if the task has not been planned.
    /// </summary>
    public async Task<string?> ExportCupAsync(int taskId, double handicap)
    {
        CompetitionTask? task = await LoadTaskAsync(taskId);
        if (task is null)
        {
            return null;
        }

        List<BarrelRadius> radii = await db.BarrelRadii
            .Where(b => b.CompetitionTaskId == taskId && b.Handicap == handicap)
            .ToListAsync();
        if (radii.Count == 0)
        {
            return null;
        }

        var radiusByIndex = radii.ToDictionary(r => r.TurnpointIndex, r => r.RadiusKm);
        List<Turnpoint> tps = task.Turnpoints.OrderBy(t => t.Index).ToList();

        // Build the waypoint table and task line from the stored turnpoints.
        var waypoints = new List<Waypoint>();
        var names = new List<string>();
        var zones = new List<ObservationZone>();
        for (int i = 0; i < tps.Count; i++)
        {
            Turnpoint tp = tps[i];
            bool isStart = i == 0;
            bool isFinish = i == tps.Count - 1;
            int style = isStart || isFinish ? 4 : 1;
            waypoints.Add(new Waypoint(tp.Waypoint, ShortCode(tp.Waypoint),
                tp.Latitude, tp.Longitude, style));
            names.Add(tp.Waypoint);

            ObservationZone zone = PlanningMapper.ToZone(tp, i);
            bool isTurnpoint = !isStart && !isFinish && !tp.IsCheckpoint;
            if (isTurnpoint && radiusByIndex.TryGetValue(i, out double rKm))
            {
                zone = zone.WithBarrel(rKm * 1000.0);
            }

            zones.Add(zone);
        }

        string desc = $"{task.Name} H{handicap.ToString("0.#", CultureInfo.InvariantCulture)}".Trim();
        var cupTask = new CupTask(desc, names, zones);

        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        new CupWriter().Write(sw, waypoints, cupTask);
        return sb.ToString();
    }

    /// <summary>
    /// Renders the task as an SVG diagram. When <paramref name="handicap"/> is given
    /// and that handicap has been planned, the per-turnpoint optimised barrels are
    /// drawn; otherwise the imported source-zone barrels are used (the task as set).
    /// Returns null if the task doesn't exist or has too few points.
    /// </summary>
    public async Task<string?> RenderDiagramAsync(int taskId, double? handicap = null)
    {
        CompetitionTask? task = await LoadTaskAsync(taskId);
        if (task is null || task.Turnpoints.Count < 2)
        {
            return null;
        }

        Course course = PlanningMapper.ToCourse(task);

        IReadOnlyList<double>? radii = null;
        if (handicap is double h)
        {
            List<BarrelRadius> rows = await db.BarrelRadii
                .Where(b => b.CompetitionTaskId == taskId && b.Handicap == h)
                .ToListAsync();
            if (rows.Count > 0)
            {
                var byIndex = rows.ToDictionary(r => r.TurnpointIndex, r => r.RadiusKm);
                radii = Enumerable.Range(0, course.Points.Count)
                    .Select(i => byIndex.TryGetValue(i, out double r) ? r : 0.0)
                    .ToList();
            }
        }

        string title = handicap is double hh
            ? $"{task.Name} — H{hh.ToString("0.#", CultureInfo.InvariantCulture)}"
            : task.Name;
        return TaskDiagram.Render(course, title, radii);
    }

    private Task<CompetitionTask?> LoadTaskAsync(int taskId) =>
        db.Tasks
            .Include(t => t.Turnpoints)
            .Include(t => t.Day)
                .ThenInclude(d => d!.Competition)
            .FirstOrDefaultAsync(t => t.Id == taskId);

    private static string ShortCode(string name)
    {
        string upper = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return upper.Length <= 6 ? upper : upper[..6];
    }
}
