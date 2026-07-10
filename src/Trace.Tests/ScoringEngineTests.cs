using Trace;
using Trace.Geometry;
using Trace.Model;
using Trace.Scoring;
using Xunit;

namespace Trace.Tests;

public class ScoringEngineTests
{
    // Task: Start (52.0,-1.0) -> TP1 (52.4,-1.0) -> Finish (52.0,-1.0),
    // an out-and-back due north then south. Start line 5 km, TP 0.5 km,
    // finish 3 km.
    private static ScoringTask MakeTask() => new(
        new[]
        {
            new ScoringPoint("Start", 52.0, -1.0, CoursePointType.Start, 5.0),
            new ScoringPoint("TP1", 52.4, -1.0, CoursePointType.Turnpoint, 0.5),
            new ScoringPoint("Finish", 52.0, -1.0, CoursePointType.Finish, 3.0),
        });

    private static TracePoint Fix(DateTime t, double lat, double lon)
        => new(t, eastings: lon, northings: lat, altGps: 1000f, altBaro: 1000f);

    /// <summary>Builds a straight trace between two points at a fixed speed.</summary>
    private static IEnumerable<TracePoint> Leg(DateTime start, double lat1, double lon1,
        double lat2, double lon2, int steps, double secondsPerStep)
    {
        for (int i = 0; i <= steps; i++)
        {
            double f = (double)i / steps;
            double lat = lat1 + (lat2 - lat1) * f;
            double lon = lon1 + (lon2 - lon1) * f;
            yield return Fix(start.AddSeconds(i * secondsPerStep), lat, lon);
        }
    }

    [Fact]
    public void FinisherGetsScoringSpeedFromReferenceDistance()
    {
        ScoringTask task = MakeTask();
        var trace = new List<TracePoint>();
        var t0 = new DateTime(1970, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Start centre -> TP1 centre -> Finish centre. ~44.5 km each way.
        trace.AddRange(Leg(t0, 52.0, -1.0, 52.4, -1.0, 50, 30));        // out
        DateTime tMid = trace[^1].When;
        trace.AddRange(Leg(tMid, 52.4, -1.0, 52.0, -1.0, 50, 30));      // back

        var engine = new ScoringEngine { ReferenceDistanceKm = 89.0 };
        ScoreResult r = engine.Score(task, trace);

        Assert.Equal(ScoreOutcome.Finished, r.Outcome);
        Assert.True(r.TaskTime.TotalSeconds > 0);
        // V_Score = D_Ref / T_Act.
        double expected = 89.0 / r.TaskTime.TotalHours;
        Assert.Equal(expected, r.ScoringSpeedKmh, 6);
    }

    [Fact]
    public void StartTimeIsExitFromStartZoneNotFirstFix()
    {
        ScoringTask task = MakeTask();
        var trace = new List<TracePoint>();
        var t0 = new DateTime(1970, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Loiter inside the 5 km start zone for 5 minutes before starting.
        for (int i = 0; i < 10; i++)
        {
            trace.Add(Fix(t0.AddSeconds(i * 30), 52.01, -1.0)); // ~1.1 km north, inside
        }

        DateTime loiterEnd = trace[^1].When;
        trace.AddRange(Leg(loiterEnd, 52.01, -1.0, 52.4, -1.0, 50, 30));
        trace.AddRange(Leg(trace[^1].When, 52.4, -1.0, 52.0, -1.0, 50, 30));

        var engine = new ScoringEngine { ReferenceDistanceKm = 89.0 };
        ScoreResult r = engine.Score(task, trace);

        Assert.Equal(ScoreOutcome.Finished, r.Outcome);
        // Task time must exclude the loiter: well under the full trace span.
        double fullSpan = (trace[^1].When - trace[0].When).TotalSeconds;
        Assert.True(r.TaskTime.TotalSeconds < fullSpan - 250,
            $"task time {r.TaskTime.TotalSeconds}s should exclude ~270s loiter");
    }

    [Fact]
    public void LandOutScoresAchievedDistanceScaledByHandicap()
    {
        ScoringTask task = MakeTask();
        var trace = new List<TracePoint>();
        var t0 = new DateTime(1970, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Start, fly only halfway to TP1, then stop (land out).
        trace.AddRange(Leg(t0, 52.0, -1.0, 52.2, -1.0, 30, 30));

        var engine = new ScoringEngine
        {
            ReferenceDistanceKm = 89.0,
            ReferenceHandicap = 114.0,
            Handicap = 93.0,
        };
        ScoreResult r = engine.Score(task, trace);

        Assert.Equal(ScoreOutcome.LandedOut, r.Outcome);
        Assert.Equal(0, r.LastAchievedPoint); // only the start achieved
        Assert.True(r.AchievedDistanceKm > 0);
        // D_Final = D_Ach * (H_Ref / H), and H_Ref > H so it scales up.
        Assert.Equal(r.AchievedDistanceKm * (114.0 / 93.0), r.FinalScoreDistanceKm, 6);
        Assert.True(r.FinalScoreDistanceKm > r.AchievedDistanceKm);
    }

    [Fact]
    public void AchievedDistanceIsAboutHalfTheFirstLeg()
    {
        ScoringTask task = MakeTask();
        var trace = new List<TracePoint>();
        var t0 = new DateTime(1970, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        trace.AddRange(Leg(t0, 52.0, -1.0, 52.2, -1.0, 30, 30)); // halfway to TP1

        var engine = new ScoringEngine { ReferenceHandicap = 100.0, Handicap = 100.0 };
        ScoreResult r = engine.Score(task, trace);

        double legLength = Geodesy.DistanceKm(52.0, -1.0, 52.4, -1.0);
        // Reached ~52.2 -> about half the leg, credited toward the barrel edge.
        Assert.InRange(r.AchievedDistanceKm, legLength * 0.40, legLength * 0.55);
    }

    // A dogleg Start -> TP1 -> Finish where the pilot rounds TP1 well outside the
    // small barrel but inside the wide observation sector. The sector opens away
    // from the two legs; a pilot passing on that outer side is achieved by sector,
    // not barrel. Used to check both radius and angle gate the sector.
    private static ScoringTask DoglegTask(double sectorRadiusKm, double sectorHalfAngleDeg) => new(
        new[]
        {
            new ScoringPoint("Start", 52.0, -1.0, CoursePointType.Start, 3.0),
            new ScoringPoint("TP1", 52.3, -0.6, CoursePointType.Turnpoint, 0.5,
                SectorRadiusKm: sectorRadiusKm, SectorHalfAngleDeg: sectorHalfAngleDeg,
                Style: ZoneStyle.Symmetrical),
            new ScoringPoint("Finish", 52.0, -0.2, CoursePointType.Finish, 3.0),
        });

    [Fact]
    public void TurnpointAchievedViaWideSectorOutsideBarrel()
    {
        var t0 = new DateTime(1970, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var trace = new List<TracePoint>();
        // Fly Start -> a point well NE of TP1 (min ~2.5 km from TP1, outside the
        // 0.5 km barrel and the 1 km sector, on the sector's outward side) -> Finish.
        trace.AddRange(Leg(t0, 52.0, -1.0, 52.55, -0.35, 40, 20));            // out, past TP1 to its NE
        trace.AddRange(Leg(trace[^1].When, 52.55, -0.35, 52.0, -0.2, 40, 20)); // in to finish

        // Wide sector (radius 10 km, half-angle 135°): the pilot passes within the
        // sector though never within the 0.5 km barrel, so TP1 is achieved -> finisher.
        var engine = new ScoringEngine { ReferenceDistanceKm = 80.0 };
        Assert.Equal(ScoreOutcome.Finished, engine.Score(DoglegTask(10.0, 135.0), trace).Outcome);

        // Same wide angle but a tiny 1 km sector radius: the trace stays ~2.5 km
        // out, beyond both barrel and sector, so TP1 is not achieved -> land-out.
        // (Proves the sector RADIUS gates achievement, not just the angle.)
        Assert.Equal(ScoreOutcome.LandedOut, engine.Score(DoglegTask(1.0, 135.0), trace).Outcome);
    }

    [Fact]
    public void TurnpointsMustBeVisitedInOrder()
    {
        // Task Start -> TP1(N) -> TP2(E) -> Finish. A trace that visits TP2 before
        // TP1 must not be credited for TP1 (it was only reached out of sequence),
        // so it cannot finish.
        var task = new ScoringTask(new[]
        {
            new ScoringPoint("Start", 52.0, -1.0, CoursePointType.Start, 3.0),
            new ScoringPoint("TP1", 52.4, -1.0, CoursePointType.Turnpoint, 1.0),
            new ScoringPoint("TP2", 52.4, -0.4, CoursePointType.Turnpoint, 1.0),
            new ScoringPoint("Finish", 52.0, -0.4, CoursePointType.Finish, 3.0),
        });

        var t0 = new DateTime(1970, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var trace = new List<TracePoint>();
        // Start -> TP2 (out of order) -> TP1 -> Finish.
        trace.AddRange(Leg(t0, 52.0, -1.0, 52.4, -0.4, 40, 20));               // to TP2 first
        trace.AddRange(Leg(trace[^1].When, 52.4, -0.4, 52.4, -1.0, 40, 20));   // back to TP1
        trace.AddRange(Leg(trace[^1].When, 52.4, -1.0, 52.0, -0.4, 40, 20));   // to finish

        var engine = new ScoringEngine { ReferenceDistanceKm = 80.0, ReferenceHandicap = 100.0, Handicap = 100.0 };
        ScoreResult r = engine.Score(task, trace);

        // TP1 was reached only after TP2, so in order the pilot achieved just TP1
        // (found on the way back) but not TP2 before it -> not a finisher.
        Assert.NotEqual(ScoreOutcome.Finished, r.Outcome);
    }
}
