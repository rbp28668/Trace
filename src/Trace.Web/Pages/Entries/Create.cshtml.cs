using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Entries;

public class CreateModel : PageModel
{
    private readonly EntryService entries;
    private readonly FleetService fleet;
    private readonly ClassService classes;
    private readonly PilotService pilots;

    public CreateModel(EntryService entries, FleetService fleet,
        ClassService classes, PilotService pilots)
    {
        this.entries = entries;
        this.fleet = fleet;
        this.classes = classes;
        this.pilots = pilots;
    }

    /// <summary>Number of ordered pilot slots offered on the form (P1 is required).</summary>
    public const int PilotSlots = 4;

    [BindProperty]
    public int ClassId { get; set; }

    [BindProperty, Required, StringLength(10)]
    [Display(Name = "Comp number")]
    public string CompNo { get; set; } = string.Empty;

    [BindProperty, Required, StringLength(100)]
    public string Type { get; set; } = string.Empty;

    [BindProperty, StringLength(20)]
    public string? Registration { get; set; }

    [BindProperty, Range(1, 200)]
    public double Handicap { get; set; } = 100;

    /// <summary>Ordered pilot ids, one per slot; slot 0 is the primary (P1).</summary>
    [BindProperty]
    public int?[] PilotIds { get; set; } = new int?[PilotSlots];

    public CompetitionClass? Class { get; private set; }
    public IReadOnlyList<Pilot> Pilots { get; private set; } = [];

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

        IReadOnlyList<int> roster = SelectedPilotIds();
        if (roster.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Choose at least the primary pilot.");
        }

        if (await fleet.CompNoExistsAsync(ClassId, CompNo))
        {
            ModelState.AddModelError(nameof(CompNo),
                "A glider with this comp number already exists in this class.");
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        CompetitionEntry entry = await entries.CreateAsync(new CompetitionEntry
        {
            CompetitionClassId = ClassId,
            Glider = new Glider
            {
                CompetitionClassId = ClassId,
                CompNo = CompNo,
                Type = Type,
                Registration = Registration,
                Handicap = Handicap,
            },
            Pilots = roster
                .Select((pilotId, order) => new EntryPilot { PilotId = pilotId, Order = order })
                .ToList(),
        });

        TempData["Flash"] = "Entry created.";
        return RedirectToPage("Edit", new { id = entry.Id });
    }

    /// <summary>Distinct, in-order pilot ids from the slots, dropping blanks/dupes.</summary>
    private IReadOnlyList<int> SelectedPilotIds()
    {
        var seen = new HashSet<int>();
        var ordered = new List<int>();
        foreach (int? id in PilotIds)
        {
            if (id is int pid && pid > 0 && seen.Add(pid))
            {
                ordered.Add(pid);
            }
        }

        return ordered;
    }

    private async System.Threading.Tasks.Task LoadOptionsAsync()
    {
        Pilots = await pilots.ListAsync();
    }
}
