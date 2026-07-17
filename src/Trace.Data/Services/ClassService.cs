using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;

namespace Trace.Data.Services;

/// <summary>Application services for competition classes.</summary>
public class ClassService
{
    private readonly TraceDbContext db;

    public ClassService(TraceDbContext db) => this.db = db;

    public Task<List<CompetitionClass>> ListForCompetitionAsync(int competitionId) =>
        db.CompetitionClasses
            .Where(cc => cc.CompetitionId == competitionId)
            .OrderBy(cc => cc.Name)
            .ToListAsync();

    public Task<CompetitionClass?> GetAsync(int id) =>
        db.CompetitionClasses
            .Include(cc => cc.Competition)
            .FirstOrDefaultAsync(cc => cc.Id == id);

    /// <summary>True if the competition already has a class with this name.</summary>
    public Task<bool> NameExistsAsync(int competitionId, string name, int? excludeId = null) =>
        db.CompetitionClasses.AnyAsync(cc =>
            cc.CompetitionId == competitionId && cc.Name == name &&
            (excludeId == null || cc.Id != excludeId));

    public async Task<CompetitionClass> CreateAsync(CompetitionClass competitionClass)
    {
        db.CompetitionClasses.Add(competitionClass);
        await db.SaveChangesAsync();
        return competitionClass;
    }

    public async System.Threading.Tasks.Task UpdateAsync(CompetitionClass competitionClass)
    {
        db.CompetitionClasses.Update(competitionClass);
        await db.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        CompetitionClass? cc = await db.CompetitionClasses.FindAsync(id);
        if (cc is not null)
        {
            db.CompetitionClasses.Remove(cc);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Counts of the child collections, for list/summary displays.</summary>
    public async Task<(int gliders, int entries, int tasks)> GetCountsAsync(int classId)
    {
        int gliders = await db.Gliders.CountAsync(g => g.CompetitionClassId == classId);
        int entries = await db.CompetitionEntries.CountAsync(en => en.CompetitionClassId == classId);
        int tasks = await db.Tasks.CountAsync(t => t.CompetitionClassId == classId);
        return (gliders, entries, tasks);
    }
}
