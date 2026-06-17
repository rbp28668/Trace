using System.Globalization;

namespace Trace;

/// <summary>
/// A single trace point (B Record) in an IGC file.
/// </summary>
public class TracePoint
{
    public TracePoint()
    {
    }

    /// <summary>
    /// Parses a B record line, using the supplied extension definitions to
    /// locate the optional fields (fix accuracy, satellites in use, etc.).
    /// </summary>
    public TracePoint(string bRecord, IEnumerable<Extension> extensions)
    {
        try
        {
            // B 102027   5210970N  00006652W A 00066  00123  00107
            if (bRecord[0] != 'B')
            {
                throw new FormatException("Not a B record");
            }

            When = ReadTime(bRecord.Substring(1, 6));

            string northingsText = bRecord.Substring(7, 7);
            char ns = bRecord[14];
            int degreesNorth = int.Parse(northingsText.Substring(0, 2), CultureInfo.InvariantCulture); // 00 -> 90
            double minutesNorth = double.Parse(northingsText.Substring(2), CultureInfo.InvariantCulture) / 1000.0;
            Northings = degreesNorth + minutesNorth / 60.0;
            if (char.ToUpperInvariant(ns) == 'S')
            {
                Northings = -Northings;
            }

            string eastingsText = bRecord.Substring(15, 8);
            char ew = bRecord[23];
            int degreesEast = int.Parse(eastingsText.Substring(0, 3), CultureInfo.InvariantCulture); // 000 -> 180
            double minutesEast = double.Parse(eastingsText.Substring(3), CultureInfo.InvariantCulture) / 1000.0;
            Eastings = degreesEast + minutesEast / 60.0;
            if (char.ToUpperInvariant(ew) == 'W')
            {
                Eastings = -Eastings;
            }

            char altFlag = bRecord[24];

            AltBaro = float.Parse(bRecord.Substring(25, 5), CultureInfo.InvariantCulture);

            if (altFlag == 'A' && bRecord.Length >= 35) // not all files have barometric altitude
            {
                AltGps = float.Parse(bRecord.Substring(30, 5), CultureInfo.InvariantCulture);
            }
            else
            {
                AltGps = 0;
            }

            foreach (Extension e in extensions)
            {
                int extStart = e.Start - 1;
                if (extStart >= bRecord.Length)
                {
                    continue; // extension declared beyond this fix record
                }

                // Clamp to the available length: some loggers declare extension
                // columns that run past the actual B record (matching C++
                // std::string::substr, which clamps rather than throwing).
                int extLength = Math.Min(e.Length, bRecord.Length - extStart);
                string text = bRecord.Substring(extStart, extLength);
                switch (e.TypeCode)
                {
                    case "FXA": // fix accuracy
                        FixAccuracy = int.Parse(text, CultureInfo.InvariantCulture);
                        break;
                    case "SIU": // satellites in use
                        SatellitesInUse = int.Parse(text, CultureInfo.InvariantCulture);
                        break;
                    case "ENL": // Engine Noise Level
                        EngineNoiseLevel = int.Parse(text, CultureInfo.InvariantCulture);
                        break;
                    case "IAS": // Indicated Airspeed
                        Ias = int.Parse(text, CultureInfo.InvariantCulture);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Problem parsing B record ({e.Message}): {bRecord}");
            throw;
        }
    }

    public TracePoint(DateTime when, double eastings, double northings, float altGps, float altBaro)
    {
        When = when;
        Eastings = eastings;
        Northings = northings;
        AltGps = altGps;
        AltBaro = altBaro;
        SatellitesInUse = 99;
    }

    /// <summary>Time of the fix (UTC).</summary>
    public DateTime When { get; }

    /// <summary>Decimal degrees, west is +ve.</summary>
    public double Eastings { get; }

    /// <summary>Decimal degrees, north is +ve.</summary>
    public double Northings { get; }

    /// <summary>GPS altitude in meters.</summary>
    public float AltGps { get; }

    /// <summary>Barometric altitude in meters.</summary>
    public float AltBaro { get; }

    public int FixAccuracy { get; }

    public int SatellitesInUse { get; }

    public int EngineNoiseLevel { get; }

    public int Ias { get; }

    // Gets the time of the data point in format HHmmss.
    private static DateTime ReadTime(string time)
    {
        int hh = int.Parse(time.Substring(0, 2), CultureInfo.InvariantCulture);
        int mm = int.Parse(time.Substring(2, 2), CultureInfo.InvariantCulture);
        int ss = int.Parse(time.Substring(4, 2), CultureInfo.InvariantCulture);

        return new DateTime(1970, 1, 1, hh, mm, ss, DateTimeKind.Utc);
    }

    public void Write(TextWriter os, IEnumerable<Extension> extensions)
    {
        DateTime t = When.ToUniversalTime();

        var sb = new System.Text.StringBuilder();
        sb.Append('B');

        // Time
        sb.Append(t.Hour.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(t.Minute.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(t.Second.ToString("D2", CultureInfo.InvariantCulture));

        // Northings
        char ns = Northings > 0 ? 'N' : 'S';
        double lat = Math.Abs(Northings);
        int degrees = (int)Math.Floor(lat);
        lat -= degrees;
        lat *= 60; // now in minutes
        int minutes = (int)Math.Floor(lat * 1000);

        sb.Append(degrees.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(minutes.ToString("D5", CultureInfo.InvariantCulture));
        sb.Append(ns);

        // Eastings
        char ew = Eastings > 0 ? 'E' : 'W';
        double lng = Math.Abs(Eastings);
        degrees = (int)Math.Floor(lng);
        lng -= degrees;
        lng *= 60; // to minutes
        minutes = (int)Math.Floor(lng * 1000);

        sb.Append(degrees.ToString("D3", CultureInfo.InvariantCulture));
        sb.Append(minutes.ToString("D5", CultureInfo.InvariantCulture));
        sb.Append(ew);

        // Altitude
        sb.Append('A');
        sb.Append(((int)AltBaro).ToString("D5", CultureInfo.InvariantCulture));
        sb.Append(((int)AltGps).ToString("D5", CultureInfo.InvariantCulture));

        int pos = 0;
        foreach (Extension e in extensions)
        {
            // Implicit assumption that the extensions are in order by position on the line.
            System.Diagnostics.Debug.Assert(pos < e.Start);
            pos = e.Finish;

            switch (e.TypeCode)
            {
                case "FXA": // fix accuracy
                    sb.Append(FixAccuracy.ToString("D" + e.Length, CultureInfo.InvariantCulture));
                    break;
                case "SIU": // Satellites in use
                    sb.Append(SatellitesInUse.ToString("D" + e.Length, CultureInfo.InvariantCulture));
                    break;
                case "ENL": // Engine Noise Level
                    sb.Append(EngineNoiseLevel.ToString("D" + e.Length, CultureInfo.InvariantCulture));
                    break;
                case "MOP": // Method of propulsion
                    sb.Append(0.ToString("D" + e.Length, CultureInfo.InvariantCulture));
                    break;
            }
        }

        os.WriteLine(sb.ToString());
    }
}
