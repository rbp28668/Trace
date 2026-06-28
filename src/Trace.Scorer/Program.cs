// Trace.Scorer — DHT flight scoring engine (docs/implementation-plan.md §5).
//
// Usage:
//   Trace.Scorer --task <task_h93.cup> --igc <flight.igc> [--igc ...]
//                [--dref <km>] [--href <handicap>] [--handicap <H>]
//   Trace.Scorer --dump <flight.igc> [<flight.igc> ...]
//
// Scores each IGC trace against the personalised task (dht.md §4.2). With
// --dump it prints the legacy IGC summary instead.

using Trace;
using Trace.Scorer;
using Trace.Scoring;

try
{
    ScorerArgs parsed = ScorerArgs.Parse(args);

    if (parsed.Dump)
    {
        return IgcDump.Run(parsed.IgcPaths);
    }

    ScoringTask task = ScoringTask.FromCup(parsed.TaskPath!);

    int exit = 0;
    foreach (string igcPath in parsed.IgcPaths)
    {
        var igc = new IGCFile();
        igc.Parse(igcPath);

        var engine = new ScoringEngine
        {
            ReferenceDistanceKm = parsed.ReferenceDistanceKm,
            ReferenceHandicap = parsed.ReferenceHandicap,
            Handicap = parsed.Handicap,
        };
        ScoreResult result = engine.Score(task, igc.Trace);

        ScoreReport.Print(Console.Out, igcPath, igc, task, result);
    }

    return exit;
}
catch (ScorerArgsException e)
{
    Console.Error.WriteLine(e.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(ScorerArgs.Usage);
    return 2;
}
catch (Exception e)
{
    Console.Error.WriteLine($"Error: {e.Message}");
    return 1;
}
