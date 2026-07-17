using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Waypoints;

/// <summary>
/// Manages a competition's waypoint list: upload a SeeYou .cup file to (re)load
/// it, and view the loaded waypoints. Task turnpoints are constrained to this
/// list.
/// </summary>
public class IndexModel : PageModel
{
    private readonly CompetitionService competitions;
    private readonly WaypointService waypoints;

    public IndexModel(CompetitionService competitions, WaypointService waypoints)
    {
        this.competitions = competitions;
        this.waypoints = waypoints;
    }

    [BindProperty]
    public int CompetitionId { get; set; }

    [BindProperty]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public string? Cup { get; set; }

    public Competition? Competition { get; private set; }
    public IReadOnlyList<CompetitionWaypoint> Waypoints { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int competitionId)
    {
        if (!await LoadAsync(competitionId))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (!await LoadAsync(CompetitionId))
        {
            return NotFound();
        }

        string cup = await ReadInputAsync();
        if (string.IsNullOrWhiteSpace(cup))
        {
            ModelState.AddModelError(string.Empty, "Upload a .cup file or paste its text.");
            return Page();
        }

        int n;
        try
        {
            n = await waypoints.ReplaceFromCupAsync(CompetitionId, cup);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Could not parse the .cup file: {ex.Message}");
            return Page();
        }

        if (n == 0)
        {
            ModelState.AddModelError(string.Empty, "No waypoints found in the .cup file.");
            return Page();
        }

        TempData["Flash"] = $"Loaded {n} waypoint(s).";
        return RedirectToPage(new { competitionId = CompetitionId });
    }

    public async Task<IActionResult> OnPostClearAsync()
    {
        if (Competition is null && !await LoadAsync(CompetitionId))
        {
            return NotFound();
        }

        await waypoints.ClearForCompetitionAsync(CompetitionId);
        TempData["Flash"] = "Waypoints cleared.";
        return RedirectToPage(new { competitionId = CompetitionId });
    }

    private async Task<bool> LoadAsync(int competitionId)
    {
        Competition = await competitions.GetAsync(competitionId);
        if (Competition is null)
        {
            return false;
        }

        CompetitionId = competitionId;
        Waypoints = await waypoints.ListForCompetitionAsync(competitionId);
        return true;
    }

    private async Task<string> ReadInputAsync()
    {
        if (Upload is { Length: > 0 })
        {
            using var reader = new StreamReader(Upload.OpenReadStream(), Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        return Cup ?? string.Empty;
    }
}
