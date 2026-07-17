using System.ComponentModel.DataAnnotations;

namespace Trace.Web.Pages.Competitions;

/// <summary>Form-bound view model for creating/editing a competition.</summary>
public class CompetitionInput
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Site { get; set; }

    [Display(Name = "Start date")]
    [DataType(DataType.Date)]
    public DateOnly StartDate { get; set; }

    [Display(Name = "End date")]
    [DataType(DataType.Date)]
    public DateOnly EndDate { get; set; }

    [Display(Name = "Make this the active competition")]
    public bool MakeActive { get; set; }
}
