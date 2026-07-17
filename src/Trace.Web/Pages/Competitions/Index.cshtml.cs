using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Competitions;

/// <summary>
/// The single hub for basic competition and class management: list, add, edit,
/// delete and activate competitions, and add/edit/delete the classes within each
/// (including per-class V_RefCru). Days, fleet, tasks and scoring live on their
/// own screens reached from here.
/// </summary>
public class IndexModel : PageModel
{
    private readonly CompetitionService competitions;
    private readonly ClassService classes;

    public IndexModel(CompetitionService competitions, ClassService classes)
    {
        this.competitions = competitions;
        this.classes = classes;
    }

    public IReadOnlyList<Competition> Competitions { get; private set; } = [];

    /// <summary>Competition id whose inline edit form is open (0 = new-competition form).</summary>
    [BindProperty(SupportsGet = true)]
    public int? EditCompetition { get; set; }

    /// <summary>Class id whose inline edit form is open (0 = new-class form for AddClassTo).</summary>
    [BindProperty(SupportsGet = true)]
    public int? EditClass { get; set; }

    /// <summary>Competition id the new-class form is being shown for.</summary>
    [BindProperty(SupportsGet = true)]
    public int? AddClassTo { get; set; }

    [BindProperty]
    public CompetitionInput Competition { get; set; } = new();

    [BindProperty]
    public ClassInput Class { get; set; } = new();

    public async System.Threading.Tasks.Task OnGetAsync()
    {
        Competitions = await competitions.ListWithClassesAsync();
    }

    // --- Competition handlers ---------------------------------------------

    public async Task<IActionResult> OnPostCreateCompetitionAsync()
    {
        ValidateOnly(Competition, nameof(Competition));
        if (await competitions.NameExistsAsync(Competition.Name))
        {
            ModelState.AddModelError("Competition.Name", "A competition with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return await ReloadAsync(editCompetition: 0);
        }

        await competitions.CreateAsync(new Competition
        {
            Name = Competition.Name,
            Site = Competition.Site,
            StartDate = Competition.StartDate,
            EndDate = Competition.EndDate,
            IsActive = Competition.MakeActive,
        });
        TempData["Flash"] = "Competition created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateCompetitionAsync(int id)
    {
        ValidateOnly(Competition, nameof(Competition));
        if (await competitions.NameExistsAsync(Competition.Name, excludeId: id))
        {
            ModelState.AddModelError("Competition.Name", "A competition with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return await ReloadAsync(editCompetition: id);
        }

        var c = await competitions.GetAsync(id);
        if (c is null)
        {
            return NotFound();
        }

        c.Name = Competition.Name;
        c.Site = Competition.Site;
        c.StartDate = Competition.StartDate;
        c.EndDate = Competition.EndDate;
        await competitions.UpdateAsync(c);

        if (Competition.MakeActive && !c.IsActive)
        {
            await competitions.SetActiveAsync(c.Id);
        }

        TempData["Flash"] = "Competition updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivateAsync(int id)
    {
        await competitions.SetActiveAsync(id);
        TempData["Flash"] = "Active competition changed.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteCompetitionAsync(int id)
    {
        await competitions.DeleteAsync(id);
        TempData["Flash"] = "Competition deleted.";
        return RedirectToPage();
    }

    // --- Class handlers ----------------------------------------------------

    public async Task<IActionResult> OnPostCreateClassAsync(int competitionId)
    {
        ValidateOnly(Class, nameof(Class));
        if (await classes.NameExistsAsync(competitionId, Class.Name))
        {
            ModelState.AddModelError("Class.Name", "This competition already has a class with that name.");
        }

        if (!ModelState.IsValid)
        {
            return await ReloadAsync(addClassTo: competitionId);
        }

        await classes.CreateAsync(new CompetitionClass
        {
            CompetitionId = competitionId,
            Name = Class.Name,
            VRefCruKmh = Class.VRefCruKmh,
        });
        TempData["Flash"] = "Class created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateClassAsync(int id)
    {
        ValidateOnly(Class, nameof(Class));
        var cc = await classes.GetAsync(id);
        if (cc is null)
        {
            return NotFound();
        }

        if (await classes.NameExistsAsync(cc.CompetitionId, Class.Name, excludeId: id))
        {
            ModelState.AddModelError("Class.Name", "This competition already has a class with that name.");
        }

        if (!ModelState.IsValid)
        {
            return await ReloadAsync(editClass: id);
        }

        cc.Name = Class.Name;
        cc.VRefCruKmh = Class.VRefCruKmh;
        await classes.UpdateAsync(cc);
        TempData["Flash"] = "Class updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteClassAsync(int id)
    {
        await classes.DeleteAsync(id);
        TempData["Flash"] = "Class deleted.";
        return RedirectToPage();
    }

    /// <summary>
    /// Discards all validation state and re-validates only <paramref name="model"/>
    /// under the given key prefix. Both <see cref="Competition"/> and
    /// <see cref="Class"/> bind on every post, so a class submit would otherwise
    /// fail on the (empty, required) competition fields and vice versa; each handler
    /// validates just the model it uses.
    /// </summary>
    private void ValidateOnly(object model, string prefix)
    {
        ModelState.Clear();
        TryValidateModel(model, prefix);
    }

    private async Task<IActionResult> ReloadAsync(
        int? editCompetition = null, int? editClass = null, int? addClassTo = null)
    {
        Competitions = await competitions.ListWithClassesAsync();
        EditCompetition = editCompetition;
        EditClass = editClass;
        AddClassTo = addClassTo;
        return Page();
    }
}

/// <summary>Form-bound view model for a class row.</summary>
public class ClassInput
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "V_RefCru (km/h)")]
    [Range(1, 500)]
    public double VRefCruKmh { get; set; } = 130;
}
