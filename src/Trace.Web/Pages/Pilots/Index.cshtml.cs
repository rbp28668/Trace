using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Pilots;

public class IndexModel : PageModel
{
    private readonly PilotService pilots;

    public IndexModel(PilotService pilots) => this.pilots = pilots;

    public IReadOnlyList<Pilot> Pilots { get; private set; } = [];

    public async System.Threading.Tasks.Task OnGetAsync()
    {
        Pilots = await pilots.ListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await pilots.DeleteAsync(id);
        TempData["Flash"] = "Pilot deleted.";
        return RedirectToPage();
    }
}
