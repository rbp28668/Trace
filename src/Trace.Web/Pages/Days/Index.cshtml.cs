using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Days;

public class IndexModel : PageModel
{
    private readonly DayService days;
    private readonly CompetitionService competitions;
    private readonly ClassService classes;

    public IndexModel(DayService days, CompetitionService competitions, ClassService classes)
    {
        this.days = days;
        this.competitions = competitions;
        this.classes = classes;
    }

    public Competition? Competition { get; private set; }
    public IReadOnlyList<Day> Days { get; private set; } = [];
    public IReadOnlyList<CompetitionClass> Classes { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int competitionId)
    {
        Competition = await competitions.GetAsync(competitionId);
        if (Competition is null)
        {
            return NotFound();
        }

        Days = await days.ListForCompetitionAsync(competitionId);
        Classes = await classes.ListForCompetitionAsync(competitionId);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int competitionId)
    {
        await days.DeleteAsync(id);
        TempData["Flash"] = "Day deleted.";
        return RedirectToPage(new { competitionId });
    }
}
