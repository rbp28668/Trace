// Trace.Planner — DHT task optimisation engine (docs/implementation-plan.md §4).
//
// Usage:
//   Trace.Planner --fleet fleet.csv --course course.cup --wind 270/30
//                 [--vref 130] [--href 120] --out outdir/
//
// Emits one standard .CUP task file per handicap plus a summary report.

using Trace.Io;
using Trace.Model;
using Trace.Planning;
using Trace.Planner;

try
{
    PlannerArgs parsed = PlannerArgs.Parse(args);

    Fleet fleet = FleetReader.Read(parsed.FleetPath, parsed.VRefCru);
    Course course = CourseReader.Read(parsed.CoursePath);
    var geometry = new CourseGeometry(course);

    double href = parsed.ReferenceHandicap ?? fleet.MaxHandicap;

    var optimizer = new BarrelOptimizer(new OptimizerOptions())
    {
        WindSpeed = parsed.WindSpeed,
        WindDirection = parsed.WindDirection,
    };
    FleetPlan plan = optimizer.Optimize(course, geometry, fleet, href);

    Directory.CreateDirectory(parsed.OutDir);
    PlanWriter.WriteAll(parsed.OutDir, course, plan);
    PlanReport.Print(Console.Out, course, plan, parsed);

    return 0;
}
catch (PlannerArgsException e)
{
    Console.Error.WriteLine(e.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(PlannerArgs.Usage);
    return 2;
}
catch (Exception e)
{
    Console.Error.WriteLine($"Error: {e.Message}");
    return 1;
}
