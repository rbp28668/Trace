using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Entries;

public class IndexModel : PageModel
{
    private readonly EntryService entries;
    private readonly ClassService classes;

    public IndexModel(EntryService entries, ClassService classes)
    {
        this.entries = entries;
        this.classes = classes;
    }

    public CompetitionClass? Class { get; private set; }
    public IReadOnlyList<CompetitionEntry> Entries { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int classId)
    {
        Class = await classes.GetAsync(classId);
        if (Class is null)
        {
            return NotFound();
        }

        Entries = await entries.ListForClassAsync(classId);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int classId)
    {
        await entries.DeleteAsync(id);
        TempData["Flash"] = "Entry removed.";
        return RedirectToPage(new { classId });
    }
}
