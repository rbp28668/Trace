using Trace.Data.Entities;
using Trace.Model;
using CoreGlider = Trace.Model.Glider;
using CoreFleet = Trace.Model.Fleet;
using DataGlider = Trace.Data.Entities.Glider;

namespace Trace.Data.Mapping;

/// <summary>
/// Converts persistence entities into the immutable <c>Trace.Core</c> domain
/// types the planning/scoring engines consume, and builds the observation zones
/// needed to export results. The engines are only ever fed through this mapper —
/// pages and services never hand-construct domain objects.
/// </summary>
public static class PlanningMapper
{
    /// <summary>
    /// Builds a baseline <see cref="Course"/> from a task's turnpoints. Roles are
    /// assigned by position and the <c>IsCheckpoint</c> flag (start, finish,
    /// checkpoint, turnpoint), matching how <c>CourseReader</c> classifies points so
    /// the optimiser resizes only the variable turnpoints. Each point carries a
    /// source <see cref="ObservationZone"/> so exports can reproduce the zone
    /// geometry, changing only the barrel radius.
    /// </summary>
    public static Course ToCourse(CompetitionTask task)
    {
        List<Turnpoint> tps = task.Turnpoints.OrderBy(t => t.Index).ToList();
        if (tps.Count < 2)
        {
            throw new InvalidOperationException(
                "A task needs at least a start and a finish before it can be planned.");
        }

        var points = new List<CoursePoint>(tps.Count);
        for (int i = 0; i < tps.Count; i++)
        {
            Turnpoint tp = tps[i];
            bool isStart = i == 0;
            bool isFinish = i == tps.Count - 1;

            ObservationZone zone = ToZone(tp, i);
            double radiusKm = zone.BarrelRadiusMetres / 1000.0;

            CoursePointType type = isStart ? CoursePointType.Start
                : isFinish ? CoursePointType.Finish
                : tp.IsCheckpoint ? CoursePointType.Checkpoint
                : CoursePointType.Turnpoint;

            points.Add(new CoursePoint(tp.Waypoint, tp.Latitude, tp.Longitude,
                type, radiusKm, zone));
        }

        return new Course(points, task.Name);
    }

    /// <summary>Builds an <see cref="ObservationZone"/> from a stored turnpoint.</summary>
    public static ObservationZone ToZone(Turnpoint tp, int pointIndex) => new()
    {
        PointIndex = pointIndex,
        Style = (ZoneStyle)tp.Style,
        R1Metres = tp.Radius1 * 1000.0,
        A1Degrees = tp.Angle1,
        R2Metres = tp.Radius2 * 1000.0,
        A2Degrees = tp.Angle2,
        IsLine = tp.IsLine,
    };

    /// <summary>
    /// Builds a <see cref="CoreFleet"/> from a class's gliders and the competition's
    /// reference cruise speed at H=100.
    /// </summary>
    public static CoreFleet ToFleet(IEnumerable<DataGlider> gliders, double vRefCruKmh)
    {
        var core = gliders.Select(g =>
            new CoreGlider(g.Type, g.Registration ?? string.Empty, g.CompNo, g.Handicap));
        return new CoreFleet(core, vRefCruKmh);
    }
}
