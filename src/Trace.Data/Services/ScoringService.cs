using Microsoft.EntityFrameworkCore;
using Trace;
using Trace.Data.Entities;
using Trace.Scoring;

namespace Trace.Data.Services;

/// <summary>
/// Uploads and scores IGC logs for day entries. Scoring reuses the same
/// per-handicap task the planner produced: <see cref="PlanningService"/> emits the
/// personalised .cup, <see cref="ScoringTask.FromCup"/> parses it, and the
/// <see cref="ScoringEngine"/> (Trace.Core) evaluates the trace in-process.
/// </summary>
public class ScoringService
{
    private readonly TraceDbContext db;
    private readonly IgcStorage storage;
    private readonly PlanningService planning;

    public ScoringService(TraceDbContext db, IgcStorage storage, PlanningService planning)
    {
        this.db = db;
        this.storage = storage;
        this.planning = planning;
    }

    /// <summary>
    /// Stores the uploaded log for a day entry and (re)creates its flight metadata.
    /// Does not score yet — call <see cref="ScoreAsync"/> after.
    /// </summary>
    public async System.Threading.Tasks.Task UploadAsync(
        int dayEntryId, string fileName, Stream content, DateTime nowUtc)
    {
        DayEntry entry = await db.DayEntries
            .Include(e => e.Flight)!.ThenInclude(f => f!.IgcFile)
            .FirstOrDefaultAsync(e => e.Id == dayEntryId)
            ?? throw new InvalidOperationException($"Day entry {dayEntryId} not found.");

        string relativePath = await storage.SaveAsync(
            entry.CompetitionClassId, entry.DayId, fileName, content);

        Flight flight = entry.Flight ??= new Flight { DayEntryId = entry.Id };
        flight.IgcFile ??= new IgcFile();
        flight.IgcFile.FileName = fileName;
        flight.IgcFile.StoredPath = relativePath;
        flight.IgcFile.UploadedUtc = nowUtc;

        // A new upload invalidates any previous score.
        flight.Start = null;
        flight.Finish = null;
        flight.Time = null;
        flight.Speed = null;
        flight.Distance = null;
        flight.AirspaceChecked = false;
        flight.AirspaceValid = false;

        if (entry.Flight.Id == 0)
        {
            db.Flights.Add(entry.Flight);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Scores the entry's uploaded log against the active task for its class/day,
    /// using the barrels stored for the glider's handicap. Returns the outcome, or
    /// throws with a user-facing message if prerequisites are missing.
    /// </summary>
    public async Task<ScoreResult> ScoreAsync(int dayEntryId)
    {
        DayEntry entry = await db.DayEntries
            .Include(e => e.Glider)
            .Include(e => e.Flight)!.ThenInclude(f => f!.IgcFile)
            .FirstOrDefaultAsync(e => e.Id == dayEntryId)
            ?? throw new InvalidOperationException($"Day entry {dayEntryId} not found.");

        if (entry.Flight?.IgcFile is null)
        {
            throw new InvalidOperationException("Upload an IGC log for this entry first.");
        }

        CompetitionTask active = await db.Tasks
            .FirstOrDefaultAsync(t => t.DayId == entry.DayId &&
                                      t.CompetitionClassId == entry.CompetitionClassId &&
                                      t.Active)
            ?? throw new InvalidOperationException(
                "No active task for this class on this day. Activate one first.");

        if (active.DRefKm is null)
        {
            throw new InvalidOperationException(
                "The active task has not been planned. Run the planner before scoring.");
        }

        double handicap = entry.Glider!.Handicap;
        string? cup = await planning.ExportCupAsync(active.Id, handicap)
            ?? throw new InvalidOperationException(
                $"No planned barrels for handicap {handicap:0.#}. Re-run the planner.");

        ScoringTask task = ScoringTask.FromCup(new StringReader(cup));

        var igc = new IGCFile();
        igc.Parse(new StringReader(await storage.ReadTextAsync(entry.Flight.IgcFile.StoredPath)));

        var engine = new ScoringEngine
        {
            ReferenceDistanceKm = active.DRefKm ?? 0.0,
            ReferenceHandicap = active.RefHandicap ?? handicap,
            Handicap = handicap,
        };
        ScoreResult result = engine.Score(task, igc.Trace);

        Flight flight = entry.Flight;
        entry.CompetitionTaskId = active.Id; // record which task was flown/scored
        if (result.Outcome == ScoreOutcome.Finished)
        {
            flight.Time = result.TaskTime;
            flight.Speed = result.ScoringSpeedKmh;
            flight.Distance = null;
        }
        else if (result.Outcome == ScoreOutcome.LandedOut)
        {
            flight.Time = null;
            flight.Speed = null;
            flight.Distance = result.FinalScoreDistanceKm;
        }
        else
        {
            flight.Time = null;
            flight.Speed = null;
            flight.Distance = 0.0;
        }

        await db.SaveChangesAsync();
        return result;
    }
}
