using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Tasks;

public class CreateModel : PageModel
{
    private readonly TaskService tasks;
    private readonly DayService days;
    private readonly ClassService classes;

    public CreateModel(TaskService tasks, DayService days, ClassService classes)
    {
        this.tasks = tasks;
        this.days = days;
        this.classes = classes;
    }

    [BindProperty]
    public int DayId { get; set; }

    [BindProperty]
    public int ClassId { get; set; }

    [BindProperty, Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [BindProperty, StringLength(4)]
    [Display(Name = "Task type")]
    public string TaskType { get; set; } = "A";

    public Day? Day { get; private set; }
    public CompetitionClass? Class { get; private set; }

    public async Task<IActionResult> OnGetAsync(int dayId, int classId)
    {
        Day = await days.GetAsync(dayId);
        Class = await classes.GetAsync(classId);
        if (Day is null || Class is null)
        {
            return NotFound();
        }

        DayId = dayId;
        ClassId = classId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Day = await days.GetAsync(DayId);
        Class = await classes.GetAsync(ClassId);
        if (Day is null || Class is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var task = await tasks.CreateAsync(new CompetitionTask
        {
            DayId = DayId,
            CompetitionClassId = ClassId,
            Name = Name,
            TaskType = TaskType,
            Index = await tasks.NextIndexAsync(DayId, ClassId),
            Active = false,
        });

        TempData["Flash"] = "Task created. Add its turnpoints below.";
        return RedirectToPage("Edit", new { id = task.Id });
    }
}
