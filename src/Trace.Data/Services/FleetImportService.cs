using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;
using Trace.Io;

namespace Trace.Data.Services;

/// <summary>One parsed CSV row, ready to preview before committing.</summary>
public class FleetImportRow
{
    public string CompNo { get; set; } = string.Empty;
    public string? Registration { get; set; }
    public string Type { get; set; } = string.Empty;
    public double Handicap { get; set; }
    public string? PilotName { get; set; }

    /// <summary>True if a glider with this comp number already exists in the class.</summary>
    public bool IsDuplicate { get; set; }
}

/// <summary>
/// Parses a fleet/entrants CSV into preview rows and commits them into a class.
/// Handles both the <c>FleetReader</c> layout
/// (<c>Type,Registration,CompNumber,Handicap</c>) and the richer entrants layout
/// (<c>CompNumber,Pilot,Club,Type,Class,Handicap</c>) by inspecting the header.
/// </summary>
public class FleetImportService
{
    private readonly TraceDbContext db;

    public FleetImportService(TraceDbContext db) => this.db = db;

    /// <summary>Parses CSV text and marks rows that clash with existing gliders.</summary>
    public async Task<List<FleetImportRow>> PreviewAsync(int classId, string csv)
    {
        List<FleetImportRow> rows = Parse(csv);

        HashSet<string> existing = (await db.Gliders
                .Where(g => g.CompetitionClassId == classId)
                .Select(g => g.CompNo)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (FleetImportRow r in rows)
        {
            r.IsDuplicate = existing.Contains(r.CompNo);
        }

        return rows;
    }

    /// <summary>
    /// Commits the given rows into the class. Existing comp numbers are updated
    /// in place; new ones are added. Pilots named in the CSV are created if
    /// missing and linked via a <see cref="CompetitionEntry"/>.
    /// </summary>
    public async Task<int> CommitAsync(int classId, IEnumerable<FleetImportRow> rows)
    {
        List<Glider> existing = await db.Gliders
            .Where(g => g.CompetitionClassId == classId)
            .ToListAsync();
        Dictionary<string, Glider> byCompNo =
            existing.ToDictionary(g => g.CompNo, StringComparer.OrdinalIgnoreCase);

        int changed = 0;
        foreach (FleetImportRow r in rows)
        {
            if (byCompNo.TryGetValue(r.CompNo, out Glider? g))
            {
                g.Type = r.Type;
                g.Registration = r.Registration;
                g.Handicap = r.Handicap;
            }
            else
            {
                g = new Glider
                {
                    CompetitionClassId = classId,
                    CompNo = r.CompNo,
                    Type = r.Type,
                    Registration = r.Registration,
                    Handicap = r.Handicap,
                };
                db.Gliders.Add(g);
                byCompNo[r.CompNo] = g;
            }

            changed++;

            if (!string.IsNullOrWhiteSpace(r.PilotName))
            {
                await LinkPilotAsync(classId, g, r.PilotName!.Trim());
            }
        }

        await db.SaveChangesAsync();
        return changed;
    }

    private async System.Threading.Tasks.Task LinkPilotAsync(
        int classId, Glider glider, string pilotName)
    {
        Pilot? pilot = await db.Pilots
            .FirstOrDefaultAsync(p => p.Name == pilotName);
        if (pilot is null)
        {
            pilot = new Pilot { Name = pilotName };
            db.Pilots.Add(pilot);
        }

        // The entry is keyed by (class, glider). Create it with this pilot as the
        // primary (order 0) if the glider isn't yet entered. EF resolves the FKs
        // once the glider/pilot are saved.
        bool entered = glider.Id != 0 && await db.CompetitionEntries.AnyAsync(en =>
            en.CompetitionClassId == classId && en.GliderId == glider.Id);
        if (!entered)
        {
            db.CompetitionEntries.Add(new CompetitionEntry
            {
                CompetitionClassId = classId,
                Glider = glider,
                Pilots = { new EntryPilot { Pilot = pilot, Order = 0 } },
            });
        }
    }

    /// <summary>Parses either supported CSV layout into rows.</summary>
    public static List<FleetImportRow> Parse(string csv)
    {
        var rows = new List<FleetImportRow>();
        using var reader = new StringReader(csv);
        string? line;
        int? compIdx = null, regIdx = null, typeIdx = null, hcapIdx = null, pilotIdx = null;
        bool headerSeen = false;

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            List<string> f = CupReader.SplitCsv(line);
            if (f.Count < 4)
            {
                continue;
            }

            if (!headerSeen && LooksLikeHeader(f))
            {
                MapHeader(f, ref compIdx, ref regIdx, ref typeIdx, ref hcapIdx, ref pilotIdx);
                headerSeen = true;
                continue;
            }

            // No header: assume the FleetReader column order.
            if (!headerSeen)
            {
                typeIdx = 0; regIdx = 1; compIdx = 2; hcapIdx = 3;
                headerSeen = true;
            }

            string comp = Get(f, compIdx);
            string type = Get(f, typeIdx);
            string hcapText = Get(f, hcapIdx);
            if (!double.TryParse(hcapText, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double handicap))
            {
                continue;
            }

            rows.Add(new FleetImportRow
            {
                CompNo = comp,
                Type = type,
                Registration = EmptyToNull(Get(f, regIdx)),
                Handicap = handicap,
                PilotName = EmptyToNull(Get(f, pilotIdx)),
            });
        }

        return rows;
    }

    private static bool LooksLikeHeader(List<string> f) =>
        f.Any(x => x.Trim().Equals("Handicap", StringComparison.OrdinalIgnoreCase)
                   || x.Trim().Equals("CompNumber", StringComparison.OrdinalIgnoreCase)
                   || x.Trim().Equals("Type", StringComparison.OrdinalIgnoreCase));

    private static void MapHeader(List<string> f, ref int? comp, ref int? reg,
        ref int? type, ref int? hcap, ref int? pilot)
    {
        for (int i = 0; i < f.Count; i++)
        {
            switch (f[i].Trim().ToLowerInvariant())
            {
                case "compnumber":
                case "compno":
                case "comp":
                    comp = i; break;
                case "registration":
                case "reg":
                    reg = i; break;
                case "type":
                    type = i; break;
                case "handicap":
                    hcap = i; break;
                case "pilot":
                case "name":
                    pilot = i; break;
            }
        }
    }

    private static string Get(List<string> f, int? idx) =>
        idx is int i && i >= 0 && i < f.Count ? f[i].Trim() : string.Empty;

    private static string? EmptyToNull(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
