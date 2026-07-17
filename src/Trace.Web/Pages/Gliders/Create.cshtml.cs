using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Gliders;

public class CreateModel : PageModel
{
    private readonly FleetService fleet;
    private readonly ClassService classes;

    public CreateModel(FleetService fleet, ClassService classes)
    {
        this.fleet = fleet;
        this.classes = classes;
    }

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

    public CompetitionClass? Class { get; private set; }

    public async Task<IActionResult> OnGetAsync(int classId)
    {
        Class = await classes.GetAsync(classId);
        if (Class is null)
        {
            return NotFound();
        }

        ClassId = classId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Class = await classes.GetAsync(ClassId);
        if (Class is null)
        {
            return NotFound();
        }

        if (await fleet.CompNoExistsAsync(ClassId, CompNo))
        {
            ModelState.AddModelError(nameof(CompNo),
                "A glider with this comp number already exists in this class.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await fleet.CreateAsync(new Glider
        {
            CompetitionClassId = ClassId,
            CompNo = CompNo,
            Type = Type,
            Registration = Registration,
            Handicap = Handicap,
        });

        TempData["Flash"] = "Glider added.";
        return RedirectToPage("Index", new { classId = ClassId });
    }
}
