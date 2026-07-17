using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Services;

namespace Trace.Web.Pages.Days;

public class EditModel : PageModel
{
    private readonly DayService days;

    public EditModel(DayService days) => this.days = days;

    [BindProperty]
    public int Id { get; set; }

    [BindProperty]
    public int CompetitionId { get; set; }

    [BindProperty, Range(1, 60)]
    [Display(Name = "Day number")]
    public int DayNo { get; set; }

    [BindProperty, DataType(DataType.Date)]
    public DateOnly Date { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var d = await days.GetAsync(id);
        if (d is null)
        {
            return NotFound();
        }

        Id = d.Id;
        CompetitionId = d.CompetitionId;
        DayNo = d.DayNo;
        Date = d.Date;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await days.DayNoExistsAsync(CompetitionId, DayNo, excludeId: Id))
        {
            ModelState.AddModelError(nameof(DayNo), "This competition already has a day with that number.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var d = await days.GetAsync(Id);
        if (d is null)
        {
            return NotFound();
        }

        d.DayNo = DayNo;
        d.Date = Date;
        await days.UpdateAsync(d);

        TempData["Flash"] = "Day updated.";
        return RedirectToPage("Index", new { competitionId = CompetitionId });
    }
}
