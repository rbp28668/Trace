using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;

namespace Trace.Data.Services;

/// <summary>
/// Application services for tasks and their turnpoints, including the
/// "at most one active task per class per day" invariant (the filtered unique
/// index is the backstop; activation here is an atomic transition).
/// </summary>
public class TaskService
{
    private readonly TraceDbContext db;

    public TaskService(TraceDbContext db) => this.db = db;

    /// <summary>Tasks for one class on one day, in display order.</summary>
    public Task<List<CompetitionTask>> ListAsync(int dayId, int classId) =>
        db.Tasks
            .Include(t => t.Turnpoints)
            .Where(t => t.DayId == dayId && t.CompetitionClassId == classId)
            .OrderBy(t => t.Index)
            .ToListAsync();

    public Task<CompetitionTask?> GetAsync(int id) =>
        db.Tasks
            .Include(t => t.Turnpoints.OrderBy(tp => tp.Index))
            .Include(t => t.Day)
            .Include(t => t.CompetitionClass)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<int> NextIndexAsync(int dayId, int classId)
    {
        int max = await db.Tasks
            .Where(t => t.DayId == dayId && t.CompetitionClassId == classId)
            .Select(t => (int?)t.Index)
            .MaxAsync() ?? -1;
        return max + 1;
    }

    public async Task<CompetitionTask> CreateAsync(CompetitionTask task)
    {
        if (task.Active)
        {
            await ClearActiveAsync(task.DayId, task.CompetitionClassId);
        }

        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    public async System.Threading.Tasks.Task UpdateAsync(CompetitionTask task)
    {
        db.Tasks.Update(task);
        await db.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        CompetitionTask? t = await db.Tasks.FindAsync(id);
        if (t is not null)
        {
            db.Tasks.Remove(t);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Makes <paramref name="id"/> the sole active task for its class/day.
    /// Deactivation + activation run in one transaction so the filtered unique
    /// index is never transiently violated.
    /// </summary>
    public async System.Threading.Tasks.Task SetActiveAsync(int id)
    {
        CompetitionTask? target = await db.Tasks.FindAsync(id);
        if (target is null)
        {
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        await ClearActiveAsync(target.DayId, target.CompetitionClassId);
        await db.SaveChangesAsync();

        target.Active = true;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Scrub the day for a class: deactivate whatever is active.</summary>
    public async System.Threading.Tasks.Task ScrubAsync(int dayId, int classId)
    {
        await ClearActiveAsync(dayId, classId);
        await db.SaveChangesAsync();
    }

    /// <summary>Replaces a task's turnpoints wholesale (used by editors/import).</summary>
    public async System.Threading.Tasks.Task ReplaceTurnpointsAsync(
        int taskId, IEnumerable<Turnpoint> turnpoints)
    {
        List<Turnpoint> existing = await db.Turnpoints
            .Where(tp => tp.CompetitionTaskId == taskId)
            .ToListAsync();
        db.Turnpoints.RemoveRange(existing);

        int i = 0;
        foreach (Turnpoint tp in turnpoints)
        {
            tp.CompetitionTaskId = taskId;
            tp.Index = i++;
            db.Turnpoints.Add(tp);
        }

        await db.SaveChangesAsync();
    }

    private async System.Threading.Tasks.Task ClearActiveAsync(int dayId, int classId)
    {
        List<CompetitionTask> active = await db.Tasks
            .Where(t => t.DayId == dayId && t.CompetitionClassId == classId && t.Active)
            .ToListAsync();
        foreach (CompetitionTask t in active)
        {
            t.Active = false;
        }
    }
}
