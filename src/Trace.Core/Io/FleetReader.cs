using System.Globalization;
using Trace.Model;

namespace Trace.Io;

/// <summary>
/// Reads the fleet CSV (dht.md §2.1, implementation-plan §4): columns
/// <c>Type, Registration, CompNumber, Handicap</c>. A header row is optional and
/// detected by a non-numeric handicap field. Handicaps are supplied directly
/// (BGA scheme).
/// </summary>
public static class FleetReader
{
    public static Fleet Read(string path, double vRefCruKmh)
    {
        using var reader = new StreamReader(path);
        return Read(reader, vRefCruKmh);
    }

    public static Fleet Read(TextReader reader, double vRefCruKmh)
    {
        var gliders = new List<Glider>();
        string? line;
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

            // A row whose handicap field is not a number is treated as a header.
            if (!double.TryParse(f[3].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double handicap))
            {
                continue;
            }

            gliders.Add(new Glider(f[0].Trim(), f[1].Trim(), f[2].Trim(), handicap));
        }

        return new Fleet(gliders, vRefCruKmh);
    }
}
