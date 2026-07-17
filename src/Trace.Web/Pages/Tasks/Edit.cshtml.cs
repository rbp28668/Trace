using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Tasks;

/// <summary>A single editable turnpoint row.</summary>
public class TurnpointRow
{
    public string Waypoint { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius1 { get; set; }
    public double Angle1 { get; set; } = 180;
    public double Radius2 { get; set; }
    public bool IsLine { get; set; }
    public bool IsCheckpoint { get; set; }
    public int Style { get; set; } = 1;
}

public class EditModel : PageModel
{
    private readonly TaskService tasks;
    private readonly PlanningService planning;

    public EditModel(TaskService tasks, PlanningService planning)
    {
        this.tasks = tasks;
        this.planning = planning;
    }

    [BindProperty]
    public int Id { get; set; }

    [BindProperty, Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [BindProperty, StringLength(4)]
    public string TaskType { get; set; } = "A";

    [BindProperty]
    [Display(Name = "Wind direction (° true, FROM)")]
    public double? WindDirDeg { get; set; }

    [BindProperty]
    [Display(Name = "Wind speed (km/h)")]
    public double? WindSpeedKmh { get; set; }

    [BindProperty]
    [Display(Name = "Reference handicap")]
    public double? RefHandicap { get; set; }

    [BindProperty]
    public List<TurnpointRow> Turnpoints { get; set; } = new();

    public int DayId { get; private set; }
    public int ClassId { get; private set; }
    public double? DRefKm { get; private set; }
    public double? TRefSec { get; private set; }
    public IReadOnlyList<double> PlannedHandicaps { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var t = await tasks.GetAsync(id);
        if (t is null)
        {
            return NotFound();
        }

        Load(t);
        PlannedHandicaps = await planning.PlannedHandicapsAsync(t.Id);
        Turnpoints = t.Turnpoints
            .OrderBy(tp => tp.Index)
            .Select(tp => new TurnpointRow
            {
                Waypoint = tp.Waypoint,
                Latitude = tp.Latitude,
                Longitude = tp.Longitude,
                Radius1 = tp.Radius1,
                Angle1 = tp.Angle1,
                Radius2 = tp.Radius2,
                IsLine = tp.IsLine,
                IsCheckpoint = tp.IsCheckpoint,
                Style = tp.Style,
            })
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var t = await tasks.GetAsync(Id);
        if (t is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            LoadDisplay(t);
            return Page();
        }

        t.Name = Name;
        t.TaskType = TaskType;
        t.WindDirDeg = WindDirDeg;
        t.WindSpeedKmh = WindSpeedKmh;
        t.RefHandicap = RefHandicap;
        await tasks.UpdateAsync(t);

        // Drop blank rows (no waypoint name) then replace the set wholesale.
        var kept = Turnpoints
            .Where(r => !string.IsNullOrWhiteSpace(r.Waypoint))
            .Select(r => new Turnpoint
            {
                Waypoint = r.Waypoint.Trim(),
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                Radius1 = r.Radius1,
                Angle1 = r.Angle1,
                Radius2 = r.Radius2,
                IsLine = r.IsLine,
                IsCheckpoint = r.IsCheckpoint,
                Style = r.Style,
                DirectionType = r.Style,
            })
            .ToList();

        await tasks.ReplaceTurnpointsAsync(t.Id, kept);

        TempData["Flash"] = "Task saved.";
        return RedirectToPage("Index", new { dayId = t.DayId, classId = t.CompetitionClassId });
    }

    public async Task<IActionResult> OnPostPlanAsync()
    {
        try
        {
            PlanResult result = await planning.PlanAsync(Id);
            var msg = $"Planned {result.HandicapCount} handicap(s): " +
                      $"D_Ref = {result.ReferenceDistanceKm:F1} km, " +
                      $"T_Ref = {result.ReferenceDurationHours * 3600:F0} s (H{result.ReferenceHandicap:0.#}).";
            if (result.Warnings.Count > 0)
            {
                msg += " Warnings: " + string.Join("; ", result.Warnings);
            }

            TempData["Flash"] = msg;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Flash"] = "Could not plan: " + ex.Message;
        }

        return RedirectToPage("Edit", new { id = Id });
    }

    public async Task<IActionResult> OnGetDiagramAsync(int id, int? handicap100)
    {
        double? handicap = handicap100 is int h ? h / 100.0 : null;
        string? svg = await planning.RenderDiagramAsync(id, handicap);
        if (svg is null)
        {
            return NotFound();
        }

        return Content(svg, "image/svg+xml");
    }

    public async Task<IActionResult> OnPostExportAsync(int handicap100)
    {
        // Handicap is passed ×100 as an int to keep the route value exact.
        double handicap = handicap100 / 100.0;
        string? cup = await planning.ExportCupAsync(Id, handicap);
        if (cup is null)
        {
            TempData["Flash"] = "No plan to export — run Plan first.";
            return RedirectToPage("Edit", new { id = Id });
        }

        string file = $"task_{Id}_h{handicap.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_')}.cup";
        return File(System.Text.Encoding.UTF8.GetBytes(cup), "text/plain", file);
    }

    private void Load(CompetitionTask t)
    {
        Name = t.Name;
        TaskType = t.TaskType;
        WindDirDeg = t.WindDirDeg;
        WindSpeedKmh = t.WindSpeedKmh;
        RefHandicap = t.RefHandicap;
        LoadDisplay(t);
    }

    /// <summary>Non-input state, so a redisplay after validation keeps typed values.</summary>
    private void LoadDisplay(CompetitionTask t)
    {
        Id = t.Id;
        DayId = t.DayId;
        ClassId = t.CompetitionClassId;
        DRefKm = t.DRefKm;
        TRefSec = t.TRefSec;
    }
}
