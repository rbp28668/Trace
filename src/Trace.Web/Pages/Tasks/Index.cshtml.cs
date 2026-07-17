using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Tasks;

public class IndexModel : PageModel
{
    private readonly TaskService tasks;
    private readonly DayService days;
    private readonly ClassService classes;

    public IndexModel(TaskService tasks, DayService days, ClassService classes)
    {
        this.tasks = tasks;
        this.days = days;
        this.classes = classes;
    }

    public Day? Day { get; private set; }
    public CompetitionClass? Class { get; private set; }
    public IReadOnlyList<CompetitionTask> Tasks { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int dayId, int classId)
    {
        Day = await days.GetAsync(dayId);
        Class = await classes.GetAsync(classId);
        if (Day is null || Class is null)
        {
            return NotFound();
        }

        Tasks = await tasks.ListAsync(dayId, classId);
        return Page();
    }

    public async Task<IActionResult> OnPostActivateAsync(int id, int dayId, int classId)
    {
        await tasks.SetActiveAsync(id);
        TempData["Flash"] = "Task activated.";
        return RedirectToPage(new { dayId, classId });
    }

    public async Task<IActionResult> OnPostScrubAsync(int dayId, int classId)
    {
        await tasks.ScrubAsync(dayId, classId);
        TempData["Flash"] = "Day scrubbed for this class (no active task).";
        return RedirectToPage(new { dayId, classId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int dayId, int classId)
    {
        await tasks.DeleteAsync(id);
        TempData["Flash"] = "Task deleted.";
        return RedirectToPage(new { dayId, classId });
    }
}
