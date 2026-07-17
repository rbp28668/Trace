using Microsoft.AspNetCore.Mvc.RazorPages;
using Trace.Data.Entities;
using Trace.Data.Services;

namespace Trace.Web.Pages;

public class IndexModel : PageModel
{
    private readonly CompetitionService competitions;
    private readonly ClassService classes;

    public IndexModel(CompetitionService competitions, ClassService classes)
    {
        this.competitions = competitions;
        this.classes = classes;
    }

    public Competition? Active { get; private set; }
    public IReadOnlyList<CompetitionClass> Classes { get; private set; } = [];

    public async System.Threading.Tasks.Task OnGetAsync()
    {
        Active = await competitions.GetActiveAsync();
        if (Active is not null)
        {
            Classes = await classes.ListForCompetitionAsync(Active.Id);
        }
    }
}
