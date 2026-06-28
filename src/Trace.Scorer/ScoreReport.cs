using Trace;
using Trace.Scoring;

namespace Trace.Scorer;

/// <summary>Prints a human-readable scoring result for one flight.</summary>
public static class ScoreReport
{
    public static void Print(TextWriter os, string igcPath, IGCFile igc, ScoringTask task, ScoreResult result)
    {
        string name = Path.GetFileName(igcPath);
        os.WriteLine($"{name}  {igc.GliderType} ({igc.Registration})  pilot: {igc.P1}");
        os.WriteLine($"  Task: {task.Description}  ({task.Points.Count} points)");

        switch (result.Outcome)
        {
            case ScoreOutcome.Finished:
                os.WriteLine($"  FINISHED  time {Format(result.TaskTime)}  " +
                    $"scoring speed {result.ScoringSpeedKmh:F2} km/h");
                break;

            case ScoreOutcome.LandedOut:
                ScoringPoint last = task.Points[result.LastAchievedPoint];
                os.WriteLine($"  LANDED OUT after '{last.Name}' (point {result.LastAchievedPoint})");
                os.WriteLine($"  achieved {result.AchievedDistanceKm:F1} km  " +
                    $"score distance {result.FinalScoreDistanceKm:F1} km");
                break;

            case ScoreOutcome.DidNotStart:
                os.WriteLine("  DID NOT START (no start-zone exit detected)");
                break;
        }

        os.WriteLine();
    }

    public static void PrintInfringements(TextWriter os, IReadOnlyList<InfringementScan.Infringement> infringements)
    {
        if (infringements.Count == 0)
        {
            os.WriteLine("  Airspace: no infringements.");
        }
        else
        {
            os.WriteLine($"  Airspace: {infringements.Count} infringement(s):");
            foreach (InfringementScan.Infringement inf in infringements)
            {
                os.WriteLine($"    {inf.AirspaceClass} {inf.AirspaceName}: " +
                    $"{inf.Enter:HH:mm:ss}–{inf.Exit:HH:mm:ss} ({inf.FixCount} fixes)");
            }
        }

        os.WriteLine();
    }

    private static string Format(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
}
