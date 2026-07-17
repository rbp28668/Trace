using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;

namespace Trace.Data.Services;

/// <summary>Application services for pilots (shared across classes).</summary>
public class PilotService
{
    private readonly TraceDbContext db;

    public PilotService(TraceDbContext db) => this.db = db;

    public Task<List<Pilot>> ListAsync() =>
        db.Pilots.OrderBy(p => p.Name).ToListAsync();

    public Task<Pilot?> GetAsync(int id) =>
        db.Pilots.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Pilot> CreateAsync(Pilot pilot)
    {
        db.Pilots.Add(pilot);
        await db.SaveChangesAsync();
        return pilot;
    }

    public async System.Threading.Tasks.Task UpdateAsync(Pilot pilot)
    {
        db.Pilots.Update(pilot);
        await db.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        Pilot? p = await db.Pilots.FindAsync(id);
        if (p is not null)
        {
            db.Pilots.Remove(p);
            await db.SaveChangesAsync();
        }
    }
}
