using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;

namespace Trace.Data.Services;

/// <summary>
/// Application services for competition entries — a glider plus the ordered
/// roster of pilots who may fly it in a class for the whole competition.
/// </summary>
public class EntryService
{
    private readonly TraceDbContext db;

    public EntryService(TraceDbContext db) => this.db = db;

    public Task<List<CompetitionEntry>> ListForClassAsync(int classId) =>
        db.CompetitionEntries
            .Include(en => en.Glider)
            .Include(en => en.Pilots.OrderBy(p => p.Order))
                .ThenInclude(ep => ep.Pilot)
            .Where(en => en.CompetitionClassId == classId)
            .OrderBy(en => en.Glider!.CompNo)
            .ToListAsync();

    public Task<CompetitionEntry?> GetAsync(int id) =>
        db.CompetitionEntries
            .Include(en => en.Glider)!.ThenInclude(g => g!.Loggers)
            .Include(en => en.Pilots.OrderBy(p => p.Order))
                .ThenInclude(ep => ep.Pilot)
            .FirstOrDefaultAsync(en => en.Id == id);

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

    /// <summary>
    /// Removes an entry and, since gliders are now owned through their entry,
    /// the glider too — unless it is still referenced by a day entry (its FK is
    /// <c>Restrict</c>), in which case the glider is left in place.
    /// </summary>
    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        CompetitionEntry? en = await db.CompetitionEntries.FindAsync(id);
        if (en is null)
        {
            return;
        }

        int gliderId = en.GliderId;
        db.CompetitionEntries.Remove(en);
        await db.SaveChangesAsync();

        bool gliderInUse = await db.DayEntries.AnyAsync(de => de.GliderId == gliderId);
        if (!gliderInUse)
        {
            Glider? g = await db.Gliders.FindAsync(gliderId);
            if (g is not null)
            {
                db.Gliders.Remove(g);
                await db.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Replaces an entry's pilot roster with the given pilot ids, in order (first
    /// = primary). Deletes the old rows in one save then inserts the new ones, so
    /// EF never interleaves an insert with a stale row and trips the unique
    /// <c>(entry, order)</c> index.
    /// </summary>
    public async System.Threading.Tasks.Task SetPilotsAsync(int entryId, IReadOnlyList<int> pilotIds)
    {
        CompetitionEntry? en = await db.CompetitionEntries
            .Include(e => e.Pilots)
            .FirstOrDefaultAsync(e => e.Id == entryId);
        if (en is null)
        {
            return;
        }

        if (en.Pilots.Count > 0)
        {
            en.Pilots.Clear();
            await db.SaveChangesAsync();
        }

        for (int order = 0; order < pilotIds.Count; order++)
        {
            en.Pilots.Add(new EntryPilot { PilotId = pilotIds[order], Order = order });
        }

        await db.SaveChangesAsync();
    }
}
