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

    public ImportModel(TaskService tasks, DayService days, ClassService classes)
    {
        this.tasks = tasks;
        this.days = days;
        this.classes = classes;
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
