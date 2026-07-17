using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages.Gliders;

public class ImportModel : PageModel
{
    private readonly FleetImportService import;
    private readonly ClassService classes;

    public ImportModel(FleetImportService import, ClassService classes)
    {
        this.import = import;
        this.classes = classes;
    }

    [BindProperty]
    public int ClassId { get; set; }

    [BindProperty]
    public string? Csv { get; set; }

    [BindProperty]
    public IFormFile? Upload { get; set; }

    /// <summary>Parsed rows carried between the preview and commit POSTs as JSON.</summary>
    [BindProperty]
    public string? RowsJson { get; set; }

    public CompetitionClass? Class { get; private set; }
    public IReadOnlyList<FleetImportRow> Preview { get; private set; } = [];

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

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        Class = await classes.GetAsync(ClassId);
        if (Class is null)
        {
            return NotFound();
        }

        string csv = await ReadInputAsync();
        if (string.IsNullOrWhiteSpace(csv))
        {
            ModelState.AddModelError(string.Empty, "Paste CSV text or choose a file to upload.");
            return Page();
        }

        Preview = await import.PreviewAsync(ClassId, csv);
        if (Preview.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "No valid rows found in the CSV.");
            return Page();
        }

        RowsJson = JsonSerializer.Serialize(Preview);
        return Page();
    }

    public async Task<IActionResult> OnPostCommitAsync()
    {
        Class = await classes.GetAsync(ClassId);
        if (Class is null)
        {
            return NotFound();
        }

        List<FleetImportRow> rows = string.IsNullOrEmpty(RowsJson)
            ? []
            : JsonSerializer.Deserialize<List<FleetImportRow>>(RowsJson) ?? [];

        int n = await import.CommitAsync(ClassId, rows);
        TempData["Flash"] = $"Imported {n} glider(s).";
        return RedirectToPage("/Entries/Index", new { classId = ClassId });
    }

    private async Task<string> ReadInputAsync()
    {
        if (Upload is { Length: > 0 })
        {
            using var reader = new StreamReader(Upload.OpenReadStream(), Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        return Csv ?? string.Empty;
    }
}
