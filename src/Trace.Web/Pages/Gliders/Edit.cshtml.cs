using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Gliders;

public class EditModel : PageModel
{
    private readonly FleetService fleet;
    private readonly TraceDbContext db;

    public EditModel(FleetService fleet, TraceDbContext db)
    {
        this.fleet = fleet;
        this.db = db;
    }

    [BindProperty]
    public int Id { get; set; }

    [BindProperty, Required, StringLength(10)]
    [Display(Name = "Comp number")]
    public string CompNo { get; set; } = string.Empty;

    [BindProperty, Required, StringLength(100)]
    public string Type { get; set; } = string.Empty;

    [BindProperty, StringLength(20)]
    public string? Registration { get; set; }

    [BindProperty, Range(1, 200)]
    public double Handicap { get; set; }

    // New-logger fields (used by the AddLogger handler).
    [BindProperty, StringLength(50)]
    public string? LoggerType { get; set; }

    [BindProperty, StringLength(50)]
    public string? LoggerId { get; set; }

    public int ClassId { get; private set; }
    public IReadOnlyList<Logger> Loggers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Glider? g = await fleet.GetAsync(id);
        if (g is null)
        {
            return NotFound();
        }

        Load(g);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Glider? g = await fleet.GetAsync(Id);
        if (g is null)
        {
            return NotFound();
        }

        if (await fleet.CompNoExistsAsync(g.CompetitionClassId, CompNo, excludeId: Id))
        {
            ModelState.AddModelError(nameof(CompNo),
                "A glider with this comp number already exists in this class.");
        }

        if (!ModelState.IsValid)
        {
            LoadDisplay(g);
            return Page();
        }

        g.CompNo = CompNo;
        g.Type = Type;
        g.Registration = Registration;
        g.Handicap = Handicap;
        await fleet.UpdateAsync(g);

        TempData["Flash"] = "Glider updated.";
        return RedirectToPage("Index", new { classId = g.CompetitionClassId });
    }

    public async Task<IActionResult> OnPostAddLoggerAsync()
    {
        Glider? g = await fleet.GetAsync(Id);
        if (g is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(LoggerId))
        {
            ModelState.AddModelError(nameof(LoggerId), "Logger id is required.");
            Load(g);
            return Page();
        }

        db.Loggers.Add(new Logger
        {
            GliderId = g.Id,
            Type = LoggerType ?? string.Empty,
            LoggerId = LoggerId.Trim(),
        });
        await db.SaveChangesAsync();

        TempData["Flash"] = "Logger added.";
        return RedirectToPage("Edit", new { id = g.Id });
    }

    public async Task<IActionResult> OnPostRemoveLoggerAsync(int loggerId)
    {
        Logger? l = await db.Loggers.FindAsync(loggerId);
        if (l is not null)
        {
            int gliderId = l.GliderId;
            db.Loggers.Remove(l);
            await db.SaveChangesAsync();
            TempData["Flash"] = "Logger removed.";
            return RedirectToPage("Edit", new { id = gliderId });
        }

        return RedirectToPage("Edit", new { id = Id });
    }

    private void Load(Glider g)
    {
        CompNo = g.CompNo;
        Type = g.Type;
        Registration = g.Registration;
        Handicap = g.Handicap;
        LoadDisplay(g);
    }

    /// <summary>
    /// Populates only the non-input state (id, class, logger list) so a redisplay
    /// after a validation failure keeps the values the user typed.
    /// </summary>
    private void LoadDisplay(Glider g)
    {
        Id = g.Id;
        ClassId = g.CompetitionClassId;
        Loggers = g.Loggers.OrderBy(l => l.LoggerId).ToList();
    }
}
