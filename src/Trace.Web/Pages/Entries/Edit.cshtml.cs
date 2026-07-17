using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Entries;

public class EditModel : PageModel
{
    private readonly EntryService entries;
    private readonly FleetService fleet;
    private readonly PilotService pilots;
    private readonly TraceDbContext db;

    public EditModel(EntryService entries, FleetService fleet,
        PilotService pilots, TraceDbContext db)
    {
        this.entries = entries;
        this.fleet = fleet;
        this.pilots = pilots;
        this.db = db;
    }

    /// <summary>Number of ordered pilot slots offered on the form (P1 is required).</summary>
    public const int PilotSlots = 4;

    [BindProperty]
    public int Id { get; set; }

    [BindProperty]
    public int ClassId { get; set; }

    [BindProperty]
    public int GliderId { get; set; }

    [BindProperty, Required, StringLength(10)]
    [Display(Name = "Comp number")]
    public string CompNo { get; set; } = string.Empty;

    [BindProperty, Required, StringLength(100)]
    public string Type { get; set; } = string.Empty;

    [BindProperty, StringLength(20)]
    public string? Registration { get; set; }

    [BindProperty, Range(1, 200)]
    public double Handicap { get; set; }

    /// <summary>Ordered pilot ids, one per slot; slot 0 is the primary (P1).</summary>
    [BindProperty]
    public int?[] PilotIds { get; set; } = new int?[PilotSlots];

    // New-logger fields (used by the AddLogger handler).
    [BindProperty, StringLength(50)]
    public string? LoggerType { get; set; }

    [BindProperty, StringLength(50)]
    public string? NewLoggerId { get; set; }

    public IReadOnlyList<Pilot> Pilots { get; private set; } = [];
    public IReadOnlyList<Logger> Loggers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        CompetitionEntry? en = await entries.GetAsync(id);
        if (en is null)
        {
            return NotFound();
        }

        Bind(en);
        BindRoster(en);
        await LoadOptionsAsync(en);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        CompetitionEntry? en = await entries.GetAsync(Id);
        if (en is null)
        {
            return NotFound();
        }

        IReadOnlyList<int> roster = SelectedPilotIds();
        if (roster.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Choose at least the primary pilot.");
        }

        if (await fleet.CompNoExistsAsync(en.CompetitionClassId, CompNo, excludeId: en.GliderId))
        {
            ModelState.AddModelError(nameof(CompNo),
                "A glider with this comp number already exists in this class.");
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(en);
            return Page();
        }

        en.Glider!.CompNo = CompNo;
        en.Glider.Type = Type;
        en.Glider.Registration = Registration;
        en.Glider.Handicap = Handicap;
        await fleet.UpdateAsync(en.Glider);
        await entries.SetPilotsAsync(en.Id, roster);

        TempData["Flash"] = "Entry updated.";
        return RedirectToPage("Index", new { classId = en.CompetitionClassId });
    }

    public async Task<IActionResult> OnPostAddLoggerAsync()
    {
        CompetitionEntry? en = await entries.GetAsync(Id);
        if (en is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(NewLoggerId))
        {
            ModelState.AddModelError(nameof(NewLoggerId), "Logger id is required.");
            Bind(en);
            BindRoster(en);
            await LoadOptionsAsync(en);
            return Page();
        }

        db.Loggers.Add(new Logger
        {
            GliderId = en.GliderId,
            Type = LoggerType ?? string.Empty,
            LoggerId = NewLoggerId.Trim(),
        });
        await db.SaveChangesAsync();

        TempData["Flash"] = "Logger added.";
        return RedirectToPage("Edit", new { id = en.Id });
    }

    public async Task<IActionResult> OnPostRemoveLoggerAsync(int loggerId)
    {
        Logger? l = await db.Loggers.FindAsync(loggerId);
        if (l is not null)
        {
            db.Loggers.Remove(l);
            await db.SaveChangesAsync();
            TempData["Flash"] = "Logger removed.";
        }

        return RedirectToPage("Edit", new { id = Id });
    }

    private void Bind(CompetitionEntry en)
    {
        Id = en.Id;
        ClassId = en.CompetitionClassId;
        GliderId = en.GliderId;
        CompNo = en.Glider!.CompNo;
        Type = en.Glider.Type;
        Registration = en.Glider.Registration;
        Handicap = en.Glider.Handicap;
    }

    /// <summary>Fills the ordered pilot slots from the entry's saved roster.</summary>
    private void BindRoster(CompetitionEntry en)
    {
        PilotIds = new int?[PilotSlots];
        var roster = en.Pilots.OrderBy(p => p.Order).Select(p => p.PilotId).ToList();
        for (int i = 0; i < PilotSlots && i < roster.Count; i++)
        {
            PilotIds[i] = roster[i];
        }
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

    private async System.Threading.Tasks.Task LoadOptionsAsync(CompetitionEntry en)
    {
        Pilots = await pilots.ListAsync();
        Loggers = en.Glider!.Loggers.OrderBy(l => l.LoggerId).ToList();
    }
}
