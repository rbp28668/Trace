using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Trace.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>HTTP status code when reached via status-code re-execution (e.g. 404).</summary>
    public int? Code { get; set; }

    public string Title => Code switch
    {
        404 => "Page not found",
        403 => "Access denied",
        _ => "Something went wrong",
    };

    public string Message => Code switch
    {
        404 => "The page or record you asked for doesn't exist. It may have been deleted.",
        403 => "You don't have permission to view this.",
        _ => "An error occurred while processing your request.",
    };

    public void OnGet(int? code = null)
    {
        Code = code;
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
