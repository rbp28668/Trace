using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Trace.Data.Services;

namespace Trace.Web.Pages.Entries;

public class EditModel : PageModel
{
    private readonly EntryService entries;
    private readonly PilotService pilots;

    public EditModel(EntryService entries, PilotService pilots)
    {
        this.entries = entries;
        this.pilots = pilots;
    }

    [BindProperty]
    public int Id { get; set; }

    [BindProperty]
    public int ClassId { get; set; }

    [BindProperty, Range(1, int.MaxValue, ErrorMessage = "Choose a glider.")]
    public int GliderId { get; set; }

    [BindProperty, Range(1, int.MaxValue, ErrorMessage = "Choose a pilot.")]
    public int PilotId { get; set; }

    [BindProperty]
    public int? P2PilotId { get; set; }

    public SelectList GliderOptions { get; private set; } = default!;
    public SelectList PilotOptions { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var en = await entries.GetAsync(id);
        if (en is null)
        {
            return NotFound();
        }

        Id = en.Id;
        ClassId = en.CompetitionClassId;
        GliderId = en.GliderId;
        PilotId = en.PilotId;
        P2PilotId = en.P2PilotId;
        await LoadOptionsAsync(en.GliderId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var en = await entries.GetAsync(Id);
        if (en is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(GliderId);
            return Page();
        }

        en.GliderId = GliderId;
        en.PilotId = PilotId;
        en.P2PilotId = P2PilotId;
        await entries.UpdateAsync(en);

        TempData["Flash"] = "Entry updated.";
        return RedirectToPage("Index", new { classId = ClassId });
    }

    private async System.Threading.Tasks.Task LoadOptionsAsync(int currentGliderId)
    {
        var gliders = await entries.AvailableGlidersAsync(ClassId, includeGliderId: currentGliderId);
        GliderOptions = new SelectList(
            gliders.Select(g => new { g.Id, Label = $"{g.CompNo} — {g.Type}" }),
            "Id", "Label");

        var allPilots = await pilots.ListAsync();
        PilotOptions = new SelectList(allPilots, "Id", "Name");
    }
}
