using Trace.Geometry;
using Trace.Model;

namespace Trace.Scoring;

/// <summary>Outcome of scoring a single flight.</summary>
public enum ScoreOutcome
{
    Finished,
    LandedOut,
    DidNotStart,
}

/// <summary>The result of scoring one IGC trace against a task (dht.md §4.2).</summary>
public class ScoreResult
{
    public required ScoreOutcome Outcome { get; init; }

    /// <summary>Index of the last achieved task point (start = 0).</summary>
    public int LastAchievedPoint { get; init; }

    /// <summary>Elapsed task time (Case A), start-exit to finish-entry.</summary>
    public TimeSpan TaskTime { get; init; }

    /// <summary>Scoring speed V_Score = D_Ref / T_Act (km/h), finishers only.</summary>
    public double ScoringSpeedKmh { get; init; }

    /// <summary>Achieved distance along the personalised path (km), land-outs.</summary>
    public double AchievedDistanceKm { get; init; }

    /// <summary>Final handicapped score distance D_Final = D_Ach·(H_Ref/H), land-outs.</summary>
    public double FinalScoreDistanceKm { get; init; }
}

/// <summary>
/// Scores an IGC trace against a personalised <see cref="ScoringTask"/>
/// (dht.md §4.2). Coordinates follow the codebase convention: latitude is
/// "northings" (north +ve) and longitude is "eastings" (east +ve).
/// </summary>
public class ScoringEngine
{
    /// <summary>
    /// The reference distance D_Ref (km) — the baseline course distance flown by
    /// the reference glider — used for the finisher scoring speed.
    /// </summary>
    public double ReferenceDistanceKm { get; init; }

    /// <summary>Reference handicap H_Ref, for land-out scaling.</summary>
    public double ReferenceHandicap { get; init; }

    /// <summary>This pilot's handicap H, for land-out scaling.</summary>
    public double Handicap { get; init; }

    public ScoreResult Score(ScoringTask task, IReadOnlyList<TracePoint> trace)
    {
        IReadOnlyList<ScoringPoint> pts = task.Points;

        // 1. Find start: the pilot may loiter in and out of the start zone during
        //    the pre-start climb, so the valid start is the LAST departure from the
        //    start before heading out on course. Anchor on the first arrival at the
        //    first turnpoint (far from the start) and take the last start crossing
        //    before it; this also avoids the return-to-start finish (start and
        //    finish often share an airfield). A line start is timed at the line
        //    crossing; a cylinder start at the zone exit.
        // Anchor the start search on the first arrival at the first turnpoint's
        // BARREL — a tight, direction-independent circle — not its wide observation
        // sector, which would make the start window depend on sector orientation.
        int firstTpEntry = pts.Count > 1 ? FindBarrelEntry(pts[1], trace, 0) : -1;
        ScoringPoint startPoint = pts[0];
        int startFixIndex = startPoint.IsLine && pts.Count > 1
            ? FindStartLineCrossing(startPoint, pts[1], trace, firstTpEntry)
            : FindStartExit(startPoint, trace, firstTpEntry);
        if (startFixIndex < 0)
        {
            return new ScoreResult { Outcome = ScoreOutcome.DidNotStart };
        }

        DateTime startTime = trace[startFixIndex].When;

        // 2. Walk the trace, capturing each zone entry IN ORDER: each point is
        //    searched only from the fix that achieved the previous point onward
        //    (searchFrom = entry). A point reached out of sequence does not count —
        //    e.g. entering Northampton South's sector before Rushden's earns no
        //    credit for Rushden. This matters because the wide sectors overlap.
        int achieved = 0;          // last achieved point index (start = 0)
        DateTime finishTime = startTime;
        int searchFrom = startFixIndex;

        for (int p = 1; p < pts.Count; p++)
        {
            int entry = FindZoneEntry(pts[p], trace, searchFrom, ZoneDirection(pts, p), EffectiveHalfAngle(pts, p));
            if (entry < 0)
            {
                break; // did not reach this point (in order)
            }

            achieved = p;
            searchFrom = entry;
            finishTime = trace[entry].When;
        }

        // 3a. Case A — finisher.
        if (achieved == pts.Count - 1)
        {
            TimeSpan elapsed = finishTime - startTime;
            double hours = elapsed.TotalHours;
            double speed = hours > 0 ? ReferenceDistanceKm / hours : 0.0;
            return new ScoreResult
            {
                Outcome = ScoreOutcome.Finished,
                LastAchievedPoint = achieved,
                TaskTime = elapsed,
                ScoringSpeedKmh = speed,
            };
        }

        // 3b. Case B — land-out.
        double achievedKm = AchievedDistanceKm(task, trace, achieved, searchFrom);
        double scale = Handicap > 0 ? ReferenceHandicap / Handicap : 1.0;
        return new ScoreResult
        {
            Outcome = ScoreOutcome.LandedOut,
            LastAchievedPoint = achieved,
            AchievedDistanceKm = achievedKm,
            FinalScoreDistanceKm = achievedKm * scale,
        };
    }

    // --- Zone transit detection -------------------------------------------

    private static double DistanceKm(ScoringPoint p, TracePoint fix)
        => Geodesy.DistanceKm(p.Latitude, p.Longitude, fix.Northings, fix.Eastings);

    /// <summary>
    /// Index of the last fix inside the start zone before the trace leaves it for
    /// the final time ahead of committing to the course. Only exits before
    /// <paramref name="beforeIndex"/> (the first-turnpoint arrival) count, so
    /// pre-start climbing in and out of the zone is ignored and the last genuine
    /// exit is taken as the start moment. <paramref name="beforeIndex"/> &lt; 0
    /// (no turnpoint reached) falls back to the whole trace. Returns the first fix
    /// if the trace never registers inside the start zone, or -1 if empty.
    /// </summary>
    private static int FindStartExit(ScoringPoint start, IReadOnlyList<TracePoint> trace, int beforeIndex)
    {
        int limit = beforeIndex >= 0 ? beforeIndex : trace.Count;
        int lastExit = -1;
        bool wasInside = false;
        for (int i = 0; i < limit; i++)
        {
            bool inside = DistanceKm(start, trace[i]) <= start.RadiusKm;
            if (inside)
            {
                wasInside = true;
            }
            else if (wasInside)
            {
                lastExit = i - 1; // last in-zone fix before this exit
                wasInside = false;
            }
        }

        if (lastExit >= 0)
        {
            return lastExit;
        }

        return trace.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Index of the last fix before the trace crosses the start line heading onto
    /// course, considering only fixes before <paramref name="beforeIndex"/>. The
    /// start line passes through the start point perpendicular to the course to the
    /// next point (<paramref name="next"/>); a crossing is a fix pair whose
    /// along-course position steps from at/behind the line to ahead of it, within
    /// the line half-width (<see cref="ScoringPoint.RadiusKm"/>). Returns the fix on
    /// the behind side (the start moment), or -1 if no valid crossing is found.
    /// </summary>
    private static int FindStartLineCrossing(ScoringPoint start, ScoringPoint next,
        IReadOnlyList<TracePoint> trace, int beforeIndex)
    {
        int limit = beforeIndex >= 0 ? beforeIndex : trace.Count;
        double course = Geodesy.BearingDegrees(start.Latitude, start.Longitude,
            next.Latitude, next.Longitude);

        int lastCrossing = -1;
        double prevAlong = double.NaN;
        for (int i = 0; i < limit; i++)
        {
            (double along, double across) = Project(start, course, trace[i]);
            if (!double.IsNaN(prevAlong) && prevAlong <= 0 && along > 0 &&
                Math.Abs(across) <= start.RadiusKm)
            {
                lastCrossing = i - 1; // last fix on the pre-start side
            }

            prevAlong = along;
        }

        return lastCrossing;
    }

    /// <summary>
    /// Projects a fix onto the start-line frame: distance along the course
    /// direction (positive = past the line, onto course) and across it (absolute
    /// offset from the line centre), both in km.
    /// </summary>
    private static (double Along, double Across) Project(ScoringPoint start, double courseDeg, TracePoint fix)
    {
        double d = Geodesy.DistanceKm(start.Latitude, start.Longitude, fix.Northings, fix.Eastings);
        double bearing = Geodesy.BearingDegrees(start.Latitude, start.Longitude, fix.Northings, fix.Eastings);
        double delta = (bearing - courseDeg) * Math.PI / 180.0;
        return (d * Math.Cos(delta), d * Math.Sin(delta));
    }

    /// <summary>
    /// First fix at or after <paramref name="from"/> inside the point's observation
    /// zone: within the barrel circle, or within the wider sector (radius plus
    /// half-angle about <paramref name="zoneDirDeg"/>). A full-circle sector
    /// (half-angle ≥ 180°) reduces to a plain radius test.
    /// </summary>
    private static int FindZoneEntry(ScoringPoint point, IReadOnlyList<TracePoint> trace, int from,
        double zoneDirDeg, double halfAngleDeg)
    {
        for (int i = from; i < trace.Count; i++)
        {
            if (InZone(point, trace[i], zoneDirDeg, halfAngleDeg))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// True if the fix lies in the point's observation zone: inside the barrel
    /// circle, or inside the sector (within the sector radius and within
    /// <paramref name="halfAngleDeg"/> either side of <paramref name="zoneDirDeg"/>).
    /// </summary>
    private static bool InZone(ScoringPoint point, TracePoint fix, double zoneDirDeg, double halfAngleDeg)
    {
        double d = DistanceKm(point, fix);
        if (d <= point.RadiusKm)
        {
            return true; // inside the barrel (full circle)
        }

        double sector = point.SectorRadiusKm > 0.0 ? point.SectorRadiusKm : point.RadiusKm;
        if (d > sector)
        {
            return false;
        }

        if (halfAngleDeg >= 180.0)
        {
            return true; // full-circle sector
        }

        double bearing = Geodesy.BearingDegrees(point.Latitude, point.Longitude, fix.Northings, fix.Eastings);
        double diff = Math.Abs(((bearing - zoneDirDeg + 540.0) % 360.0) - 180.0);
        return diff <= halfAngleDeg;
    }

    /// <summary>
    /// Direction (deg true) the observation sector at point <paramref name="i"/>
    /// faces, per its <see cref="ZoneStyle"/>. A symmetric sector opens OUTWARD,
    /// away from the incoming and outgoing legs (the SeeYou convention): its
    /// direction is the reverse of the bisector of the bearings to the neighbours.
    /// Directional styles face the relevant neighbour. Returns 0 for a full-circle
    /// zone (direction unused).
    /// </summary>
    private static double ZoneDirection(IReadOnlyList<ScoringPoint> pts, int i)
    {
        ScoringPoint p = pts[i];
        double? toPrev = i > 0
            ? Geodesy.BearingDegrees(p.Latitude, p.Longitude, pts[i - 1].Latitude, pts[i - 1].Longitude)
            : null; // bearing FROM the point toward the previous point
        double? toNext = i < pts.Count - 1
            ? Geodesy.BearingDegrees(p.Latitude, p.Longitude, pts[i + 1].Latitude, pts[i + 1].Longitude)
            : null; // bearing FROM the point toward the next point

        switch (p.Style)
        {
            case ZoneStyle.ToNext:
                return toNext ?? toPrev ?? 0.0;
            case ZoneStyle.ToPrevious:
                return toPrev ?? toNext ?? 0.0;
            case ZoneStyle.ToStart:
                return Geodesy.BearingDegrees(p.Latitude, p.Longitude, pts[0].Latitude, pts[0].Longitude);
            case ZoneStyle.Symmetrical:
            default:
                if (toPrev is double a && toNext is double b)
                {
                    // Sector opens away from both legs: reverse the bisector of the
                    // bearings toward the two neighbours.
                    return Geodesy.Normalize360(Bisector(a, b) + 180.0);
                }

                return toPrev ?? toNext ?? 0.0;
        }
    }

    /// <summary>Sector half-angle to use at point <paramref name="i"/> (its own).</summary>
    private static double EffectiveHalfAngle(IReadOnlyList<ScoringPoint> pts, int i)
        => pts[i].SectorHalfAngleDeg;

    /// <summary>First fix at or after <paramref name="from"/> inside the point's barrel circle.</summary>
    private static int FindBarrelEntry(ScoringPoint point, IReadOnlyList<TracePoint> trace, int from)
    {
        for (int i = from; i < trace.Count; i++)
        {
            if (DistanceKm(point, trace[i]) <= point.RadiusKm)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Angular bisector (deg true) of two bearings.</summary>
    private static double Bisector(double a, double b)
    {
        double ar = a * Math.PI / 180.0;
        double br = b * Math.PI / 180.0;
        double x = Math.Cos(ar) + Math.Cos(br);
        double y = Math.Sin(ar) + Math.Sin(br);
        if (Math.Abs(x) < 1e-12 && Math.Abs(y) < 1e-12)
        {
            return a; // legs are exactly opposite; bisector undefined, fall back
        }

        return Geodesy.Normalize360(Math.Atan2(y, x) * 180.0 / Math.PI);
    }

    // --- Land-out achieved distance ---------------------------------------

    /// <summary>
    /// Achieved distance along the personalised path (dht.md §4.2 Case B): sum of
    /// completed legs (centre-to-centre, less barrel savings) plus the marking
    /// point — the closest point of approach on the uncompleted leg projected
    /// toward the next barrel edge.
    /// </summary>
    private double AchievedDistanceKm(ScoringTask task, IReadOnlyList<TracePoint> trace,
        int achieved, int lastFixIndex)
    {
        IReadOnlyList<ScoringPoint> pts = task.Points;
        double total = 0.0;

        // Completed legs: centre-to-centre between achieved task points.
        for (int p = 0; p < achieved; p++)
        {
            total += Geodesy.DistanceKm(
                pts[p].Latitude, pts[p].Longitude, pts[p + 1].Latitude, pts[p + 1].Longitude);
        }

        if (achieved >= pts.Count - 1)
        {
            return total; // completed (shouldn't reach here for a land-out)
        }

        // Uncompleted leg: from the last achieved point toward the next point.
        ScoringPoint from = pts[achieved];
        ScoringPoint to = pts[achieved + 1];
        double legLength = Geodesy.DistanceKm(from.Latitude, from.Longitude, to.Latitude, to.Longitude);

        // Closest approach of the remaining trace to the NEXT point gives the
        // furthest progress; distance made good along the leg = legLength minus
        // the remaining straight-line distance to the next point, clamped, and
        // credited up to the next barrel edge.
        double minRemaining = legLength;
        for (int i = lastFixIndex; i < trace.Count; i++)
        {
            double d = DistanceKm(to, trace[i]);
            if (d < minRemaining)
            {
                minRemaining = d;
            }
        }

        double madeGood = legLength - minRemaining;
        if (madeGood < 0)
        {
            madeGood = 0;
        }

        // Credit only up to the next barrel edge (cannot score beyond the zone).
        double maxOnLeg = Math.Max(0.0, legLength - to.RadiusKm);
        total += Math.Min(madeGood, maxOnLeg);
        return total;
    }
}
