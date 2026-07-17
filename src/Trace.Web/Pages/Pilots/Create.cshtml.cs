using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Pilots;

public class CreateModel : PageModel
{
    private readonly PilotService pilots;

    public CreateModel(PilotService pilots) => this.pilots = pilots;

    [BindProperty, Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Account number")]
    public int? AccountNo { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await pilots.CreateAsync(new Pilot { Name = Name, AccountNo = AccountNo });
        TempData["Flash"] = "Pilot created.";
        return RedirectToPage("Index");
    }
}
