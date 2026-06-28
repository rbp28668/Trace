using System.Globalization;

namespace Trace.Planner;

/// <summary>Thrown when the command line is invalid.</summary>
public class PlannerArgsException : Exception
{
    public PlannerArgsException(string message) : base(message) { }
}

/// <summary>Parsed Trace.Planner command-line arguments.</summary>
public class PlannerArgs
{
    public required string FleetPath { get; init; }
    public required string CoursePath { get; init; }
    public required string OutDir { get; init; }
    public double WindSpeed { get; init; }
    public double WindDirection { get; init; }
    public double VRefCru { get; init; } = 130.0;
    public double? ReferenceHandicap { get; init; }

    public const string Usage =
        "Usage: Trace.Planner --fleet <fleet.csv> --course <course.cup> --wind <dir>/<speed>\n" +
        "                     [--vref <kmh>] [--href <handicap>] --out <dir>\n" +
        "\n" +
        "  --wind 270/30   wind FROM 270° true at 30 km/h\n" +
        "  --vref 130      cruise airspeed of a notional H=100 glider (km/h, default 130)\n" +
        "  --href 120      reference handicap (default: highest in fleet)";

    public static PlannerArgs Parse(string[] args)
    {
        string? fleet = null, course = null, outDir = null, wind = null;
        double vref = 130.0;
        double? href = null;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "--fleet": fleet = Next(args, ref i, a); break;
                case "--course": course = Next(args, ref i, a); break;
                case "--out": outDir = Next(args, ref i, a); break;
                case "--wind": wind = Next(args, ref i, a); break;
                case "--vref": vref = ParseDouble(Next(args, ref i, a), a); break;
                case "--href": href = ParseDouble(Next(args, ref i, a), a); break;
                default:
                    throw new PlannerArgsException($"Unknown argument: {a}");
            }
        }

        if (fleet is null) throw new PlannerArgsException("Missing required --fleet.");
        if (course is null) throw new PlannerArgsException("Missing required --course.");
        if (outDir is null) throw new PlannerArgsException("Missing required --out.");

        (double dir, double speed) = ParseWind(wind);

        return new PlannerArgs
        {
            FleetPath = fleet,
            CoursePath = course,
            OutDir = outDir,
            WindDirection = dir,
            WindSpeed = speed,
            VRefCru = vref,
            ReferenceHandicap = href,
        };
    }

    private static string Next(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            throw new PlannerArgsException($"{flag} requires a value.");
        }

        return args[++i];
    }

    private static double ParseDouble(string text, string flag)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
            ? v
            : throw new PlannerArgsException($"{flag} expects a number, got '{text}'.");

    /// <summary>Parses <c>dir/speed</c> (e.g. <c>270/30</c>); absent wind is nil.</summary>
    private static (double Dir, double Speed) ParseWind(string? wind)
    {
        if (string.IsNullOrWhiteSpace(wind))
        {
            return (0.0, 0.0);
        }

        string[] parts = wind.Split('/');
        if (parts.Length != 2)
        {
            throw new PlannerArgsException($"--wind expects <dir>/<speed>, got '{wind}'.");
        }

        return (ParseDouble(parts[0], "--wind direction"), ParseDouble(parts[1], "--wind speed"));
    }
}
