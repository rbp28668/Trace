using System.Globalization;

namespace Trace.Scorer;

/// <summary>Thrown when the command line is invalid.</summary>
public class ScorerArgsException : Exception
{
    public ScorerArgsException(string message) : base(message) { }
}

/// <summary>Parsed Trace.Scorer command-line arguments.</summary>
public class ScorerArgs
{
    public bool Dump { get; init; }
    public string? TaskPath { get; init; }
    public required IReadOnlyList<string> IgcPaths { get; init; }
    public double ReferenceDistanceKm { get; init; }
    public double ReferenceHandicap { get; init; } = 100.0;
    public double Handicap { get; init; } = 100.0;
    public IReadOnlyList<string> AirspacePaths { get; init; } = Array.Empty<string>();

    public const string Usage =
        "Usage: Trace.Scorer --task <task.cup> --igc <flight.igc> [--igc ...]\n" +
        "                    [--dref <km>] [--href <handicap>] [--handicap <H>]\n" +
        "                    [--airspace <a.txt,b.txt>]\n" +
        "       Trace.Scorer --dump <flight.igc> [<flight.igc> ...]\n" +
        "\n" +
        "  --task     personalised task .cup file (with barrel radii)\n" +
        "  --dref     reference distance D_Ref in km (for finisher scoring speed)\n" +
        "  --href     reference handicap H_Ref (default 100)\n" +
        "  --handicap this pilot's handicap H (default 100)\n" +
        "  --airspace comma-separated OpenAir files; scan the trace for infringements\n" +
        "  --dump     print the IGC summary instead of scoring";

    public static ScorerArgs Parse(string[] args)
    {
        bool dump = false;
        string? task = null;
        var igc = new List<string>();
        double dref = 0.0, href = 100.0, handicap = 100.0;
        var airspace = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "--dump": dump = true; break;
                case "--task": task = Next(args, ref i, a); break;
                case "--igc": igc.Add(Next(args, ref i, a)); break;
                case "--dref": dref = ParseDouble(Next(args, ref i, a), a); break;
                case "--href": href = ParseDouble(Next(args, ref i, a), a); break;
                case "--handicap": handicap = ParseDouble(Next(args, ref i, a), a); break;
                case "--airspace":
                    airspace.AddRange(Next(args, ref i, a)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                default:
                    if (a.StartsWith('-'))
                    {
                        throw new ScorerArgsException($"Unknown argument: {a}");
                    }

                    // Bare path: treat as an IGC file (convenient with --dump).
                    igc.Add(a);
                    break;
            }
        }

        if (igc.Count == 0)
        {
            throw new ScorerArgsException("No IGC files supplied.");
        }

        if (!dump && task is null)
        {
            throw new ScorerArgsException("Missing required --task (or use --dump).");
        }

        return new ScorerArgs
        {
            Dump = dump,
            TaskPath = task,
            IgcPaths = igc,
            ReferenceDistanceKm = dref,
            ReferenceHandicap = href,
            Handicap = handicap,
            AirspacePaths = airspace,
        };
    }

    private static string Next(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            throw new ScorerArgsException($"{flag} requires a value.");
        }

        return args[++i];
    }

    private static double ParseDouble(string text, string flag)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
            ? v
            : throw new ScorerArgsException($"{flag} expects a number, got '{text}'.");
}
