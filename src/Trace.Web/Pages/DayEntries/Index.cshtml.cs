using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.DayEntries;

public class IndexModel : PageModel
{
    private readonly DayEntryService entries;
    private readonly ScoringService scoring;
    private readonly DayService days;
    private readonly ClassService classes;
    private readonly TaskService tasks;

    public IndexModel(DayEntryService entries, ScoringService scoring,
        DayService days, ClassService classes, TaskService tasks)
    {
        this.entries = entries;
        this.scoring = scoring;
        this.days = days;
        this.classes = classes;
        this.tasks = tasks;
    }

    public Day? Day { get; private set; }
    public CompetitionClass? Class { get; private set; }
    public CompetitionTask? ActiveTask { get; private set; }
    public IReadOnlyList<DayEntry> Entries { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int dayId, int classId)
    {
        if (!await LoadAsync(dayId, classId))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSeedAsync(int dayId, int classId)
    {
        int n = await entries.SeedFromCompetitionEntriesAsync(dayId, classId);
        TempData["Flash"] = $"Added {n} day entr{(n == 1 ? "y" : "ies")} from the class entries.";
        return RedirectToPage(new { dayId, classId });
    }

    public async Task<IActionResult> OnPostUploadAsync(int dayId, int classId, int entryId, IFormFile? igc)
    {
        if (igc is null || igc.Length == 0)
        {
            TempData["Flash"] = "Choose an IGC file to upload.";
            return RedirectToPage(new { dayId, classId });
        }

        await using Stream stream = igc.OpenReadStream();
        await scoring.UploadAsync(entryId, igc.FileName, stream, DateTime.UtcNow);
        TempData["Flash"] = "IGC uploaded.";
        return RedirectToPage(new { dayId, classId });
    }

    public async Task<IActionResult> OnPostScoreAsync(int dayId, int classId, int entryId)
    {
        try
        {
            var r = await scoring.ScoreAsync(entryId);
            TempData["Flash"] = r.Outcome switch
            {
                Trace.Scoring.ScoreOutcome.Finished =>
                    $"Finisher: {r.ScoringSpeedKmh:F2} km/h ({r.TaskTime:hh\\:mm\\:ss}).",
                Trace.Scoring.ScoreOutcome.LandedOut =>
                    $"Land-out: {r.FinalScoreDistanceKm:F1} km scored.",
                _ => "Did not start.",
            };
        }
        catch (InvalidOperationException ex)
        {
            TempData["Flash"] = "Could not score: " + ex.Message;
        }

        return RedirectToPage(new { dayId, classId });
    }

    public async Task<IActionResult> OnPostScoreAllAsync(int dayId, int classId)
    {
        var list = await entries.ListAsync(dayId, classId);
        int scored = 0, failed = 0;
        foreach (var e in list.Where(e => e.Flight?.IgcFile is not null))
        {
            try
            {
                await scoring.ScoreAsync(e.Id);
                scored++;
            }
            catch (InvalidOperationException)
            {
                failed++;
            }
        }

        TempData["Flash"] = $"Scored {scored} entr{(scored == 1 ? "y" : "ies")}" +
                            (failed > 0 ? $"; {failed} could not be scored." : ".");
        return RedirectToPage(new { dayId, classId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int dayId, int classId, int entryId)
    {
        await entries.DeleteAsync(entryId);
        TempData["Flash"] = "Entry removed.";
        return RedirectToPage(new { dayId, classId });
    }

    private async Task<bool> LoadAsync(int dayId, int classId)
    {
        Day = await days.GetAsync(dayId);
        Class = await classes.GetAsync(classId);
        if (Day is null || Class is null)
        {
            return false;
        }

        var dayTasks = await tasks.ListAsync(dayId, classId);
        ActiveTask = dayTasks.FirstOrDefault(t => t.Active);
        Entries = await entries.ListAsync(dayId, classId);
        return true;
    }
}
