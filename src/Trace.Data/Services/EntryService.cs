using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;

namespace Trace.Data.Services;

/// <summary>
/// Application services for competition entries — pairing a pilot with a glider
/// within a class for the whole competition.
/// </summary>
public class EntryService
{
    private readonly TraceDbContext db;

    public EntryService(TraceDbContext db) => this.db = db;

    public Task<List<CompetitionEntry>> ListForClassAsync(int classId) =>
        db.CompetitionEntries
            .Include(en => en.Pilot)
            .Include(en => en.Glider)
            .Include(en => en.P2Pilot)
            .Where(en => en.CompetitionClassId == classId)
            .OrderBy(en => en.Glider!.CompNo)
            .ToListAsync();

    public Task<CompetitionEntry?> GetAsync(int id) =>
        db.CompetitionEntries
            .Include(en => en.Pilot)
            .Include(en => en.Glider)
            .FirstOrDefaultAsync(en => en.Id == id);

    /// <summary>Gliders in the class that aren't yet entered (plus optionally the current one).</summary>
    public async Task<List<Glider>> AvailableGlidersAsync(int classId, int? includeGliderId = null)
    {
        List<int> taken = await db.CompetitionEntries
            .Where(en => en.CompetitionClassId == classId)
            .Select(en => en.GliderId)
            .ToListAsync();

        return await db.Gliders
            .Where(g => g.CompetitionClassId == classId &&
                        (!taken.Contains(g.Id) || g.Id == includeGliderId))
            .OrderBy(g => g.CompNo)
            .ToListAsync();
    }

    public async Task<CompetitionEntry> CreateAsync(CompetitionEntry entry)
    {
        db.CompetitionEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    public async System.Threading.Tasks.Task UpdateAsync(CompetitionEntry entry)
    {
        db.CompetitionEntries.Update(entry);
        await db.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        CompetitionEntry? en = await db.CompetitionEntries.FindAsync(id);
        if (en is not null)
        {
            db.CompetitionEntries.Remove(en);
            await db.SaveChangesAsync();
        }
    }
}
