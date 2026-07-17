using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Days;

public class CreateModel : PageModel
{
    private readonly DayService days;
    private readonly CompetitionService competitions;

    public CreateModel(DayService days, CompetitionService competitions)
    {
        this.days = days;
        this.competitions = competitions;
    }

    [BindProperty]
    public int CompetitionId { get; set; }

    [BindProperty, Range(1, 60)]
    [Display(Name = "Day number")]
    public int DayNo { get; set; }

    [BindProperty, DataType(DataType.Date)]
    public DateOnly Date { get; set; }

    public Competition? Competition { get; private set; }

    public async Task<IActionResult> OnGetAsync(int competitionId)
    {
        Competition = await competitions.GetAsync(competitionId);
        if (Competition is null)
        {
            return NotFound();
        }

        CompetitionId = competitionId;
        DayNo = await days.NextDayNoAsync(competitionId);
        Date = Competition.StartDate;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Competition = await competitions.GetAsync(CompetitionId);
        if (Competition is null)
        {
            return NotFound();
        }

        if (await days.DayNoExistsAsync(CompetitionId, DayNo))
        {
            ModelState.AddModelError(nameof(DayNo), "This competition already has a day with that number.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await days.CreateAsync(new Day
        {
            CompetitionId = CompetitionId,
            DayNo = DayNo,
            Date = Date,
        });

        TempData["Flash"] = "Day added.";
        return RedirectToPage("Index", new { competitionId = CompetitionId });
    }
}
