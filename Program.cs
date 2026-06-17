using Trace;

int exitCode = 0;

foreach (string arg in args)
{
    if (arg.StartsWith('-') || arg.StartsWith('/'))
    {
        // Option flag — none defined yet.
        Console.Error.WriteLine($"Unknown option: {arg}");
        exitCode = 1;
        continue;
    }

    if (!File.Exists(arg))
    {
        Console.Error.WriteLine($"File not found: {arg}");
        exitCode = 1;
        continue;
    }

    try
    {
        var igc = new IGCFile();
        igc.Parse(arg);

        Console.WriteLine($"{arg}");
        Console.WriteLine($"  Glider:      {igc.GliderType} ({igc.Registration})");
        Console.WriteLine($"  Pilot:       {igc.P1}");
        Console.WriteLine($"  Date:        {igc.Date:yyyy-MM-dd}");
        Console.WriteLine($"  Trace points: {igc.Trace.Count}");

        if (igc.Task is { } task)
        {
            Console.WriteLine($"  Task:        #{task.TaskNumber}, {task.Points.Count} waypoint(s)" +
                (string.IsNullOrEmpty(task.Description) ? "" : $" — {task.Description}"));
            foreach (TaskPoint point in task.Points)
            {
                Console.WriteLine($"    {point.Northings,10:F5}, {point.Eastings,10:F5}  {point.Name}");
            }
        }
        else
        {
            Console.WriteLine("  Task:        (none declared)");
        }
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"Error reading {arg}: {e.Message}");
        exitCode = 1;
    }
}

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Trace [options] <file.igc> [<file.igc> ...]");
    exitCode = 1;
}

return exitCode;
