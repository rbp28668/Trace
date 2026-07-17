using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;

namespace Trace.Data.Services;

/// <summary>Application services for competition days.</summary>
public class DayService
{
    private readonly TraceDbContext db;

    public DayService(TraceDbContext db) => this.db = db;

    public Task<List<Day>> ListForCompetitionAsync(int competitionId) =>
        db.Days
            .Where(d => d.CompetitionId == competitionId)
            .OrderBy(d => d.DayNo)
            .ToListAsync();

    public Task<Day?> GetAsync(int id) =>
        db.Days
            .Include(d => d.Competition)
            .FirstOrDefaultAsync(d => d.Id == id);

    /// <summary>True if the competition already has a day with this number.</summary>
    public Task<bool> DayNoExistsAsync(int competitionId, int dayNo, int? excludeId = null) =>
        db.Days.AnyAsync(d =>
            d.CompetitionId == competitionId && d.DayNo == dayNo &&
            (excludeId == null || d.Id != excludeId));

    /// <summary>Next unused day number for the competition (1-based).</summary>
    public async Task<int> NextDayNoAsync(int competitionId)
    {
        int max = await db.Days
            .Where(d => d.CompetitionId == competitionId)
            .Select(d => (int?)d.DayNo)
            .MaxAsync() ?? 0;
        return max + 1;
    }

    public async Task<Day> CreateAsync(Day day)
    {
        db.Days.Add(day);
        await db.SaveChangesAsync();
        return day;
    }

    public async System.Threading.Tasks.Task UpdateAsync(Day day)
    {
        db.Days.Update(day);
        await db.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        Day? d = await db.Days.FindAsync(id);
        if (d is not null)
        {
            db.Days.Remove(d);
            await db.SaveChangesAsync();
        }
    }
}
