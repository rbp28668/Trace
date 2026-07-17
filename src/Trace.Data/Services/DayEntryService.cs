using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;

namespace Trace.Data.Services;

/// <summary>
/// Application services for day entries (who flew which glider on a day) and their
/// scored flights.
/// </summary>
public class DayEntryService
{
    private readonly TraceDbContext db;

    public DayEntryService(TraceDbContext db) => this.db = db;

    public Task<List<DayEntry>> ListAsync(int dayId, int classId) =>
        db.DayEntries
            .Include(e => e.Pilot)
            .Include(e => e.P2Pilot)
            .Include(e => e.Glider)
            .Include(e => e.Flight)!.ThenInclude(f => f!.IgcFile)
            .Where(e => e.DayId == dayId && e.CompetitionClassId == classId)
            .OrderBy(e => e.Glider!.CompNo)
            .ToListAsync();

    public Task<DayEntry?> GetAsync(int id) =>
        db.DayEntries
            .Include(e => e.Pilot)
            .Include(e => e.P2Pilot)
            .Include(e => e.Glider)
            .Include(e => e.Flight)!.ThenInclude(f => f!.IgcFile)
            .FirstOrDefaultAsync(e => e.Id == id);

    /// <summary>
    /// Seeds day entries from the class's competition entries — every entered
    /// pilot/glider gets a day entry, skipping ones already present. Returns the
    /// number created.
    /// </summary>
    public async Task<int> SeedFromCompetitionEntriesAsync(int dayId, int classId)
    {
        List<int> existingGliders = await db.DayEntries
            .Where(e => e.DayId == dayId && e.CompetitionClassId == classId)
            .Select(e => e.GliderId)
            .ToListAsync();

        List<CompetitionEntry> entries = await db.CompetitionEntries
            .Include(en => en.Pilots.OrderBy(p => p.Order))
            .Where(en => en.CompetitionClassId == classId &&
                         !existingGliders.Contains(en.GliderId))
            .ToListAsync();

        int created = 0;
        foreach (CompetitionEntry en in entries)
        {
            // Seed the day with the entry's roster: primary pilot (order 0), and
            // the second pilot as P2 when the entry is a two-crew glider. The user
            // edits who actually flew afterwards.
            EntryPilot? primary = en.Pilots.FirstOrDefault();
            if (primary is null)
            {
                continue; // an entry with no pilots yet can't seed a day flight
            }

            db.DayEntries.Add(new DayEntry
            {
                DayId = dayId,
                CompetitionClassId = classId,
                PilotId = primary.PilotId,
                P2PilotId = en.Pilots.Skip(1).FirstOrDefault()?.PilotId,
                GliderId = en.GliderId,
            });
            created++;
        }

        await db.SaveChangesAsync();
        return created;
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        DayEntry? e = await db.DayEntries.FindAsync(id);
        if (e is not null)
        {
            db.DayEntries.Remove(e);
            await db.SaveChangesAsync();
        }
    }
}
