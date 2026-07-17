using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Services;

namespace Trace.Web.Pages.Pilots;

public class EditModel : PageModel
{
    private readonly PilotService pilots;

    public EditModel(PilotService pilots) => this.pilots = pilots;

    [BindProperty]
    public int Id { get; set; }

    [BindProperty, Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Account number")]
    public int? AccountNo { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await pilots.GetAsync(id);
        if (p is null)
        {
            return NotFound();
        }

        Id = p.Id;
        Name = p.Name;
        AccountNo = p.AccountNo;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var p = await pilots.GetAsync(Id);
        if (p is null)
        {
            return NotFound();
        }

        p.Name = Name;
        p.AccountNo = AccountNo;
        await pilots.UpdateAsync(p);

        TempData["Flash"] = "Pilot updated.";
        return RedirectToPage("Index");
    }
}
