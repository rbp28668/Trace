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

        // 1. Find start: the last fix inside the start zone before leaving it
        //    heading onto the course. We take the first exit from the start zone.
        int startFixIndex = FindStartExit(pts[0], trace);
        if (startFixIndex < 0)
        {
            return new ScoreResult { Outcome = ScoreOutcome.DidNotStart };
        }

        DateTime startTime = trace[startFixIndex].When;

        // 2. Walk the trace, capturing each subsequent zone entry in order.
        int achieved = 0;          // last achieved point index (start = 0)
        DateTime finishTime = startTime;
        int searchFrom = startFixIndex;

        for (int p = 1; p < pts.Count; p++)
        {
            int entry = FindZoneEntry(pts[p], trace, searchFrom);
            if (entry < 0)
            {
                break; // did not reach this point
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
    /// First fix at which the trace leaves the start zone (was inside, then
    /// outside). Returns the last in-zone fix index (the start moment), or the
    /// first fix if the trace never registers inside the start zone.
    /// </summary>
    private static int FindStartExit(ScoringPoint start, IReadOnlyList<TracePoint> trace)
    {
        bool wasInside = false;
        for (int i = 0; i < trace.Count; i++)
        {
            bool inside = DistanceKm(start, trace[i]) <= start.RadiusKm;
            if (inside)
            {
                wasInside = true;
            }
            else if (wasInside)
            {
                return i - 1; // last fix inside the zone = start time
            }
        }

        return trace.Count > 0 ? 0 : -1;
    }

    /// <summary>First fix at or after <paramref name="from"/> inside the point's zone.</summary>
    private static int FindZoneEntry(ScoringPoint point, IReadOnlyList<TracePoint> trace, int from)
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
