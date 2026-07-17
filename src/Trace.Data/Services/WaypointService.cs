using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;
using Trace.Io;

namespace Trace.Data.Services;

/// <summary>
/// Application services for a competition's waypoint list, loaded from an
/// uploaded SeeYou .cup file (parsed by <see cref="CupReader"/>). Task turnpoints
/// are constrained to this list.
/// </summary>
public class WaypointService
{
    private readonly TraceDbContext db;

    public WaypointService(TraceDbContext db) => this.db = db;

    /// <summary>Waypoints for a competition, name-ordered.</summary>
    public Task<List<CompetitionWaypoint>> ListForCompetitionAsync(int competitionId) =>
        db.CompetitionWaypoints
            .Where(w => w.CompetitionId == competitionId)
            .OrderBy(w => w.Name)
            .ToListAsync();

    public Task<int> CountForCompetitionAsync(int competitionId) =>
        db.CompetitionWaypoints.CountAsync(w => w.CompetitionId == competitionId);

    /// <summary>Parses .cup text into preview waypoints without persisting them.</summary>
    public static List<CompetitionWaypoint> Parse(string cupText)
    {
        var reader = new CupReader();
        reader.Parse(new StringReader(cupText));

        return reader.Waypoints
            .Select(wp => new CompetitionWaypoint
            {
                Name = wp.Name,
                Code = string.IsNullOrWhiteSpace(wp.Code) ? null : wp.Code,
                Latitude = wp.Latitude,
                Longitude = wp.Longitude,
                Style = wp.Style,
                ElevationM = wp.ElevationM,
            })
            .ToList();
    }

    /// <summary>
    /// Replaces a competition's waypoint list with those parsed from the .cup
    /// text. De-duplicates by name (first occurrence wins) so the unique index
    /// holds. Returns the number of waypoints stored.
    /// </summary>
    public async Task<int> ReplaceFromCupAsync(int competitionId, string cupText)
    {
        List<CompetitionWaypoint> parsed = Parse(cupText);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<CompetitionWaypoint>();
        foreach (CompetitionWaypoint w in parsed)
        {
            if (!string.IsNullOrWhiteSpace(w.Name) && seen.Add(w.Name))
            {
                w.CompetitionId = competitionId;
                deduped.Add(w);
            }
        }

        List<CompetitionWaypoint> existing = await db.CompetitionWaypoints
            .Where(w => w.CompetitionId == competitionId)
            .ToListAsync();
        db.CompetitionWaypoints.RemoveRange(existing);
        await db.SaveChangesAsync();

        db.CompetitionWaypoints.AddRange(deduped);
        await db.SaveChangesAsync();
        return deduped.Count;
    }

    public async System.Threading.Tasks.Task ClearForCompetitionAsync(int competitionId)
    {
        List<CompetitionWaypoint> existing = await db.CompetitionWaypoints
            .Where(w => w.CompetitionId == competitionId)
            .ToListAsync();
        db.CompetitionWaypoints.RemoveRange(existing);
        await db.SaveChangesAsync();
    }
}
