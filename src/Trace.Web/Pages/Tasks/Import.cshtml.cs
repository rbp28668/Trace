using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Tasks;

public class ImportModel : PageModel
{
    private readonly TaskService tasks;
    private readonly DayService days;
    private readonly ClassService classes;
    private readonly WaypointService waypoints;

    public ImportModel(TaskService tasks, DayService days, ClassService classes,
        WaypointService waypoints)
    {
        this.tasks = tasks;
        this.days = days;
        this.classes = classes;
        this.waypoints = waypoints;
    }

    [BindProperty]
    public int DayId { get; set; }

    [BindProperty]
    public int ClassId { get; set; }

    [BindProperty]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public string? Cup { get; set; }

    [BindProperty, StringLength(4)]
    public string TaskType { get; set; } = "A";

    /// <summary>Parsed task carried between preview and commit as JSON.</summary>
    [BindProperty]
    public string? TaskJson { get; set; }

    public Day? Day { get; private set; }
    public CompetitionClass? Class { get; private set; }
    public ParsedTask? Preview { get; private set; }

    /// <summary>Turnpoint names in the preview that aren't in the competition's waypoint list.</summary>
    public IReadOnlyList<string> UnmatchedNames { get; private set; } = [];

    /// <summary>True once the competition has a waypoint list loaded.</summary>
    public bool HasWaypoints { get; private set; }

    public async Task<IActionResult> OnGetAsync(int dayId, int classId)
    {
        if (!await LoadContextAsync(dayId, classId))
        {
            return NotFound();
        }

        DayId = dayId;
        ClassId = classId;
        return Page();
    }

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        if (!await LoadContextAsync(DayId, ClassId))
        {
            return NotFound();
        }

        string cup = await ReadInputAsync();
        if (string.IsNullOrWhiteSpace(cup))
        {
            ModelState.AddModelError(string.Empty, "Upload a .cup file or paste its text.");
            return Page();
        }

        List<ParsedTask> parsed;
        try
        {
            parsed = TaskImportService.Parse(cup);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Could not parse the .cup file: {ex.Message}");
            return Page();
        }

        if (parsed.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "No task block found in the .cup file.");
            return Page();
        }

        // Import the first task block; multi-task files are rare here.
        Preview = parsed[0];

        // Constrain to the competition's waypoint list: resolve each turnpoint's
        // coordinates from the matching waypoint, and flag any that don't match.
        UnmatchedNames = await ResolveAgainstWaypointsAsync(Preview);

        TaskJson = JsonSerializer.Serialize(Preview);
        return Page();
    }

    public async Task<IActionResult> OnPostCommitAsync()
    {
        if (!await LoadContextAsync(DayId, ClassId))
        {
            return NotFound();
        }

        ParsedTask? parsed = string.IsNullOrEmpty(TaskJson)
            ? null
            : JsonSerializer.Deserialize<ParsedTask>(TaskJson);
        if (parsed is null)
        {
            ModelState.AddModelError(string.Empty, "Nothing to import.");
            return Page();
        }

        // Re-resolve against the waypoint list (the source of truth for coords),
        // and refuse to import a task with names not in the competition's list.
        Preview = parsed;
        UnmatchedNames = await ResolveAgainstWaypointsAsync(parsed);
        if (!HasWaypoints)
        {
            ModelState.AddModelError(string.Empty,
                "This competition has no waypoints loaded. Load a .cup file on the Waypoints page first.");
            TaskJson = JsonSerializer.Serialize(parsed);
            return Page();
        }

        if (UnmatchedNames.Count > 0)
        {
            ModelState.AddModelError(string.Empty,
                "Some turnpoints are not in the competition's waypoint list: " +
                string.Join(", ", UnmatchedNames) +
                ". Add them to the waypoint file or edit the task after import.");
            TaskJson = JsonSerializer.Serialize(parsed);
            return Page();
        }

        var task = await tasks.CreateAsync(new CompetitionTask
        {
            DayId = DayId,
            CompetitionClassId = ClassId,
            Name = string.IsNullOrWhiteSpace(parsed.Description) ? "Imported task" : parsed.Description,
            TaskType = TaskType,
            Index = await tasks.NextIndexAsync(DayId, ClassId),
            Active = false,
        });

        await tasks.ReplaceTurnpointsAsync(task.Id, parsed.Turnpoints);

        TempData["Flash"] = $"Imported task with {parsed.Turnpoints.Count} turnpoint(s).";
        return RedirectToPage("Edit", new { id = task.Id });
    }

    private async Task<bool> LoadContextAsync(int dayId, int classId)
    {
        Day = await days.GetAsync(dayId);
        Class = await classes.GetAsync(classId);
        return Day is not null && Class is not null;
    }

    /// <summary>
    /// Rewrites each parsed turnpoint's coordinates from the matching competition
    /// waypoint (by name, case-insensitive) and returns the distinct names that
    /// had no match. Sets <see cref="HasWaypoints"/>.
    /// </summary>
    private async Task<IReadOnlyList<string>> ResolveAgainstWaypointsAsync(ParsedTask parsed)
    {
        List<CompetitionWaypoint> list =
            await waypoints.ListForCompetitionAsync(Class!.CompetitionId);
        HasWaypoints = list.Count > 0;

        var byName = list.ToDictionary(w => w.Name, StringComparer.OrdinalIgnoreCase);
        var unmatched = new List<string>();
        foreach (Turnpoint tp in parsed.Turnpoints)
        {
            if (byName.TryGetValue(tp.Waypoint, out CompetitionWaypoint? wp))
            {
                tp.Waypoint = wp.Name; // canonicalise casing
                tp.Latitude = wp.Latitude;
                tp.Longitude = wp.Longitude;
            }
            else if (!unmatched.Contains(tp.Waypoint, StringComparer.OrdinalIgnoreCase))
            {
                unmatched.Add(tp.Waypoint);
            }
        }

        return unmatched;
    }

    private async Task<string> ReadInputAsync()
    {
        if (Upload is { Length: > 0 })
        {
            using var reader = new StreamReader(Upload.OpenReadStream(), Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        return Cup ?? string.Empty;
    }
}
