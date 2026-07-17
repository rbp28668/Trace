using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;

namespace Trace.Data.Services;

/// <summary>
/// Application services for competitions and the "one active competition"
/// invariant. The filtered unique index in <see cref="TraceDbContext"/> is the
/// backstop; this service makes activation a single atomic transition
/// (deactivate the current active competition, then activate the target).
/// </summary>
public class CompetitionService
{
    private readonly TraceDbContext db;

    public CompetitionService(TraceDbContext db) => this.db = db;

    public Task<List<Competition>> ListAsync() =>
        db.Competitions
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.StartDate)
            .ToListAsync();

    /// <summary>All competitions with their classes eagerly loaded, for the hub screen.</summary>
    public Task<List<Competition>> ListWithClassesAsync() =>
        db.Competitions
            .Include(c => c.Classes.OrderBy(cc => cc.Name))
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.StartDate)
            .ToListAsync();

    public Task<Competition?> GetAsync(int id) =>
        db.Competitions.FirstOrDefaultAsync(c => c.Id == id);

    /// <summary>The single active competition, or null if none is active.</summary>
    public Task<Competition?> GetActiveAsync() =>
        db.Competitions.FirstOrDefaultAsync(c => c.IsActive);

    /// <summary>True if another competition already uses this name.</summary>
    public Task<bool> NameExistsAsync(string name, int? excludeId = null) =>
        db.Competitions.AnyAsync(c =>
            c.Name == name && (excludeId == null || c.Id != excludeId));

    public async Task<Competition> CreateAsync(Competition competition)
    {
        // A brand-new competition is only active if it's the first one, or the
        // caller explicitly asked; keep activation an explicit action.
        if (competition.IsActive)
        {
            await ClearActiveAsync();
        }

        db.Competitions.Add(competition);
        await db.SaveChangesAsync();
        return competition;
    }

    public async System.Threading.Tasks.Task UpdateAsync(Competition competition)
    {
        db.Competitions.Update(competition);
        await db.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        Competition? c = await db.Competitions.FindAsync(id);
        if (c is not null)
        {
            db.Competitions.Remove(c);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Makes <paramref name="id"/> the sole active competition. Deactivating the
    /// previous one and activating the target happen in one transaction so the
    /// filtered unique index is never transiently violated.
    /// </summary>
    public async System.Threading.Tasks.Task SetActiveAsync(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await ClearActiveAsync();
        await db.SaveChangesAsync();

        Competition? target = await db.Competitions.FindAsync(id);
        if (target is not null)
        {
            target.IsActive = true;
            await db.SaveChangesAsync();
        }

        await tx.CommitAsync();
    }

    private async System.Threading.Tasks.Task ClearActiveAsync()
    {
        List<Competition> active =
            await db.Competitions.Where(c => c.IsActive).ToListAsync();
        foreach (Competition c in active)
        {
            c.IsActive = false;
        }
    }
}
