using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Entries;

public class CreateModel : PageModel
{
    private readonly EntryService entries;
    private readonly ClassService classes;
    private readonly PilotService pilots;

    public CreateModel(EntryService entries, ClassService classes, PilotService pilots)
    {
        this.entries = entries;
        this.classes = classes;
        this.pilots = pilots;
    }

    [BindProperty]
    public int ClassId { get; set; }

    [BindProperty, Range(1, int.MaxValue, ErrorMessage = "Choose a glider.")]
    public int GliderId { get; set; }

    [BindProperty, Range(1, int.MaxValue, ErrorMessage = "Choose a pilot.")]
    public int PilotId { get; set; }

    [BindProperty]
    public int? P2PilotId { get; set; }

    public CompetitionClass? Class { get; private set; }
    public SelectList GliderOptions { get; private set; } = default!;
    public SelectList PilotOptions { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int classId)
    {
        Class = await classes.GetAsync(classId);
        if (Class is null)
        {
            return NotFound();
        }

        ClassId = classId;
        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Class = await classes.GetAsync(ClassId);
        if (Class is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        await entries.CreateAsync(new CompetitionEntry
        {
            CompetitionClassId = ClassId,
            GliderId = GliderId,
            PilotId = PilotId,
            P2PilotId = P2PilotId,
        });

        TempData["Flash"] = "Entry created.";
        return RedirectToPage("Index", new { classId = ClassId });
    }

    private async System.Threading.Tasks.Task LoadOptionsAsync()
    {
        var gliders = await entries.AvailableGlidersAsync(ClassId);
        GliderOptions = new SelectList(
            gliders.Select(g => new { g.Id, Label = $"{g.CompNo} — {g.Type}" }),
            "Id", "Label");

        var allPilots = await pilots.ListAsync();
        PilotOptions = new SelectList(allPilots, "Id", "Name");
    }
}
