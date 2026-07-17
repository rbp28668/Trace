using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;

namespace Trace.Data.Services;

/// <summary>Application services for gliders and their loggers within a class.</summary>
public class FleetService
{
    private readonly TraceDbContext db;

    public FleetService(TraceDbContext db) => this.db = db;

    public Task<List<Glider>> ListForClassAsync(int classId) =>
        db.Gliders
            .Include(g => g.Loggers)
            .Where(g => g.CompetitionClassId == classId)
            .OrderBy(g => g.CompNo)
            .ToListAsync();

    public Task<Glider?> GetAsync(int id) =>
        db.Gliders
            .Include(g => g.Loggers)
            .Include(g => g.CompetitionClass)
            .FirstOrDefaultAsync(g => g.Id == id);

    public async Task<Glider> CreateAsync(Glider glider)
    {
        db.Gliders.Add(glider);
        await db.SaveChangesAsync();
        return glider;
    }

    public async System.Threading.Tasks.Task UpdateAsync(Glider glider)
    {
        db.Gliders.Update(glider);
        await db.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        Glider? g = await db.Gliders.FindAsync(id);
        if (g is not null)
        {
            db.Gliders.Remove(g);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>True if a glider with this comp number already exists in the class.</summary>
    public Task<bool> CompNoExistsAsync(int classId, string compNo, int? excludeId = null) =>
        db.Gliders.AnyAsync(g =>
            g.CompetitionClassId == classId &&
            g.CompNo == compNo &&
            (excludeId == null || g.Id != excludeId));
}
