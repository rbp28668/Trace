using System.Globalization;
using Trace.Geometry;

namespace Trace.Airspace;

/// <summary>
/// Reads airspace in the OpenAir format (airspace.md). Supports the record types
/// present in the UK dataset: AC (class), AN (name), AL/AH (vertical limits),
/// DP (polygon point), DC (circle), V X= (centre), V D= (arc direction), and
/// DB (arc between two points). Circles and arcs are tessellated into polygon
/// vertices so downstream geometry only handles polygons.
///
/// Spec: https://github.com/naviter/seeyou_file_formats/blob/main/OpenAir_File_Format_Support.md
/// </summary>
public class OpenAirReader
{
    private const double NmToKm = 1.852;
    private const int ArcSegments = 32; // tessellation resolution for a full circle

    public List<AirspaceVolume> Airspaces { get; } = new();

    /// <summary>Reads a file, appending to <see cref="Airspaces"/> (multi-file load).</summary>
    public void Parse(string path)
    {
        using var reader = new StreamReader(path);
        Parse(reader);
    }

    public void Parse(TextReader reader)
    {
        Builder? current = null;
        // Arc/circle state (per OpenAir: V X= sets centre, V D=+/- sets direction).
        GeoPoint? centre = null;
        int arcDirection = 1; // +1 clockwise, -1 anticlockwise

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '*')
            {
                continue;
            }

            string keyword = TwoCharKeyword(line);
            string rest = line.Length > keyword.Length ? line[keyword.Length..].Trim() : string.Empty;

            switch (keyword)
            {
                case "AC":
                    current?.Commit(Airspaces);
                    current = new Builder { Class = rest };
                    centre = null;
                    arcDirection = 1;
                    break;

                case "AN":
                    if (current != null) current.Name = rest;
                    break;

                case "AL":
                    if (current != null) current.LowerFt = ParseAltitude(rest);
                    break;

                case "AH":
                    if (current != null) current.UpperFt = ParseAltitude(rest);
                    break;

                case "DP":
                    current?.Points.Add(ParseCoordinate(rest));
                    break;

                case "DC":
                    if (current != null && centre is { } c)
                    {
                        AddCircle(current, c, double.Parse(rest, CultureInfo.InvariantCulture) * NmToKm);
                    }

                    break;

                case "DB":
                    if (current != null && centre is { } ac)
                    {
                        AddArc(current, ac, arcDirection, rest);
                    }

                    break;

                case "V ":
                    (centre, arcDirection) = ParseVariable(rest, centre, arcDirection);
                    break;

                // DA (arc by radius/angles), DY (airway) etc. are not present in
                // the UK dataset; ignore unknown keywords.
            }
        }

        current?.Commit(Airspaces);
    }

    private static string TwoCharKeyword(string line)
    {
        if (line.StartsWith("V ", StringComparison.Ordinal)) return "V ";
        return line.Length >= 2 ? line[..2] : line;
    }

    private static (GeoPoint? Centre, int Direction) ParseVariable(string rest, GeoPoint? centre, int direction)
    {
        // "X=57:18:34 N 002:16:02 W" or "D=+" / "D=-"
        if (rest.StartsWith("X=", StringComparison.OrdinalIgnoreCase))
        {
            return (ParseCoordinate(rest[2..].Trim()), direction);
        }

        if (rest.StartsWith("D=", StringComparison.OrdinalIgnoreCase))
        {
            return (centre, rest.Contains('-') ? -1 : 1);
        }

        return (centre, direction);
    }

    private static void AddCircle(Builder b, GeoPoint centre, double radiusKm)
    {
        for (int i = 0; i < ArcSegments; i++)
        {
            double bearing = 360.0 * i / ArcSegments;
            (double lat, double lon) = Geodesy.PointAt(centre.Latitude, centre.Longitude, bearing, radiusKm);
            b.Points.Add(new GeoPoint(lat, lon));
        }
    }

    private static void AddArc(Builder b, GeoPoint centre, int direction, string rest)
    {
        // DB <from>, <to> — arc centred at the current centre, in the given
        // direction, between the two endpoints.
        string[] parts = rest.Split(',');
        if (parts.Length != 2)
        {
            return;
        }

        GeoPoint from = ParseCoordinate(parts[0].Trim());
        GeoPoint to = ParseCoordinate(parts[1].Trim());

        double radiusKm = Geodesy.DistanceKm(centre.Latitude, centre.Longitude, from.Latitude, from.Longitude);
        double a0 = Geodesy.BearingDegrees(centre.Latitude, centre.Longitude, from.Latitude, from.Longitude);
        double a1 = Geodesy.BearingDegrees(centre.Latitude, centre.Longitude, to.Latitude, to.Longitude);

        double sweep = direction > 0 ? Norm(a1 - a0) : -Norm(a0 - a1);
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (360.0 / ArcSegments)));

        b.Points.Add(from);
        for (int i = 1; i <= steps; i++)
        {
            double bearing = a0 + sweep * i / steps;
            (double lat, double lon) = Geodesy.PointAt(centre.Latitude, centre.Longitude, bearing, radiusKm);
            b.Points.Add(new GeoPoint(lat, lon));
        }
    }

    private static double Norm(double deg)
    {
        double d = deg % 360.0;
        return d < 0 ? d + 360.0 : d;
    }

    /// <summary>Parses "DD:MM:SS N DDD:MM:SS W" into decimal degrees.</summary>
    public static GeoPoint ParseCoordinate(string text)
    {
        // Split into lat and lon halves on the N/S hemisphere letter.
        text = text.Trim();
        int latEnd = -1;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = char.ToUpperInvariant(text[i]);
            if (ch is 'N' or 'S')
            {
                latEnd = i;
                break;
            }
        }

        if (latEnd < 0)
        {
            throw new FormatException($"Bad OpenAir coordinate: {text}");
        }

        double lat = ParseDms(text[..latEnd].Trim());
        if (char.ToUpperInvariant(text[latEnd]) == 'S') lat = -lat;

        string lonPart = text[(latEnd + 1)..].Trim();
        char lonHemi = char.ToUpperInvariant(lonPart[^1]);
        double lon = ParseDms(lonPart[..^1].Trim());
        if (lonHemi == 'W') lon = -lon;

        return new GeoPoint(lat, lon);
    }

    private static double ParseDms(string text)
    {
        string[] parts = text.Split(':');
        double deg = double.Parse(parts[0], CultureInfo.InvariantCulture);
        double min = parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 0.0;
        double sec = parts.Length > 2 ? double.Parse(parts[2], CultureInfo.InvariantCulture) : 0.0;
        return deg + min / 60.0 + sec / 3600.0;
    }

    /// <summary>
    /// Parses an OpenAir altitude token to feet: "1500 ft", "FL115" (×100 ft),
    /// "SFC"/"GND" (0), "UNL" (very large).
    /// </summary>
    public static double ParseAltitude(string text)
    {
        text = text.Trim().ToUpperInvariant();
        if (text is "SFC" or "GND" or "0")
        {
            return 0.0;
        }

        if (text is "UNL" or "UNLIM" or "UNLIMITED")
        {
            return 999999.0;
        }

        if (text.StartsWith("FL", StringComparison.Ordinal))
        {
            return double.Parse(text[2..].Trim(), CultureInfo.InvariantCulture) * 100.0;
        }

        // Strip a trailing unit/altitude-datum (FT, AMSL, AGL, MSL).
        string digits = new string(text.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return digits.Length > 0 ? double.Parse(digits, CultureInfo.InvariantCulture) : 0.0;
    }

    private sealed class Builder
    {
        public string Class = string.Empty;
        public string Name = string.Empty;
        public double LowerFt;
        public double UpperFt = 999999.0;
        public List<GeoPoint> Points { get; } = new();

        public void Commit(List<AirspaceVolume> into)
        {
            if (Points.Count >= 3)
            {
                into.Add(new AirspaceVolume(Class, Name, Points, LowerFt, UpperFt));
            }
        }
    }
}
