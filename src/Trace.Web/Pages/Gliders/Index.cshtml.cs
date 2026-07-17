using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Gliders;

public class IndexModel : PageModel
{
    private readonly FleetService fleet;
    private readonly ClassService classes;

    public IndexModel(FleetService fleet, ClassService classes)
    {
        this.fleet = fleet;
        this.classes = classes;
    }

    public CompetitionClass? Class { get; private set; }
    public IReadOnlyList<Glider> Gliders { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int classId)
    {
        Class = await classes.GetAsync(classId);
        if (Class is null)
        {
            return NotFound();
        }

        Gliders = await fleet.ListForClassAsync(classId);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int classId)
    {
        await fleet.DeleteAsync(id);
        TempData["Flash"] = "Glider deleted.";
        return RedirectToPage(new { classId });
    }
}
