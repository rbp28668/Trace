using System.Globalization;
using System.Text;
using Trace.Geometry;
using Trace.Model;

namespace Trace.Rendering;

/// <summary>
/// Renders a task as a standalone SVG: the centre-to-centre legs, each turnpoint's
/// observation sector (R1/A1, oriented by <see cref="ZoneStyle"/>) and DHT barrel
/// (R2), the start line and the finish ring. A single fleet-overview diagram — the
/// sectors are the fixed task geometry and the barrels are whatever radii are
/// passed in (typically the reference glider's baseline).
///
/// Geometry matches <c>Trace.Scoring.ScoringEngine</c>: symmetric sectors open
/// OUTWARD (the reverse of the inward bisector of the bearings to the two
/// neighbours), directional styles face the named neighbour (dht.md §4.2).
/// Coordinates are projected to a local equirectangular km frame about the task
/// centroid; adequate for the task-scale drawing.
/// </summary>
public static class TaskDiagram
{
    private const double CanvasMax = 950.0;   // longest side, px
    private const double PadKm = 5.0;         // margin around the geometry

    /// <summary>
    /// Renders <paramref name="course"/> to an SVG string. <paramref name="barrelRadiiKm"/>
    /// gives the barrel radius (km) to draw at each course point, indexed by point;
    /// pass the reference glider's radii for a baseline fleet view. When null, each
    /// point's source-zone barrel is used.
    /// </summary>
    public static string Render(Course course, string title,
        IReadOnlyList<double>? barrelRadiiKm = null)
    {
        IReadOnlyList<CoursePoint> pts = course.Points;
        if (pts.Count == 0)
        {
            return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"/>";
        }

        double lat0 = pts.Average(p => p.Latitude);
        double lon0 = pts.Average(p => p.Longitude);
        double kmPerLat = 111.19;
        double kmPerLon = 111.19 * Math.Cos(lat0 * Math.PI / 180.0);

        // Project each point to local km (x east, y north).
        var xy = new (double X, double Y)[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            xy[i] = ((pts[i].Longitude - lon0) * kmPerLon, (pts[i].Latitude - lat0) * kmPerLat);
        }

        // Bounding box, expanded by each point's outer shape.
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        for (int i = 0; i < pts.Count; i++)
        {
            double r = OuterRadiusKm(pts[i], Barrel(pts, i, barrelRadiiKm));
            minX = Math.Min(minX, xy[i].X - r); maxX = Math.Max(maxX, xy[i].X + r);
            minY = Math.Min(minY, xy[i].Y - r); maxY = Math.Max(maxY, xy[i].Y + r);
        }

        minX -= PadKm; maxX += PadKm; minY -= PadKm; maxY += PadKm;
        double wKm = maxX - minX, hKm = maxY - minY;
        double scale = CanvasMax / Math.Max(wKm, hKm);
        double w = wKm * scale, h = hKm * scale;

        // Screen transform: x right, y down (north up).
        double Sx(double x) => (x - minX) * scale;
        double Sy(double y) => (maxY - y) * scale;

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w:F0}\" height=\"{h:F0}\" viewBox=\"0 0 {w:F0} {h:F0}\" font-family=\"sans-serif\">\n");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#fbfbf9\"/>\n");

        // Centre-to-centre legs.
        sb.Append("<path d=\"");
        for (int i = 0; i < pts.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{(i == 0 ? "M" : "L")}{Sx(xy[i].X):F1},{Sy(xy[i].Y):F1} ");
        }

        sb.Append("\" fill=\"none\" stroke=\"#999\" stroke-width=\"1.5\" stroke-dasharray=\"6 4\"/>\n");

        for (int i = 0; i < pts.Count; i++)
        {
            RenderPoint(sb, pts, xy, i, barrelRadiiKm, scale, Sx, Sy);
        }

        // Title, scale bar, north.
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"18\" y=\"32\" font-size=\"20\" font-weight=\"bold\">{Escape(title)}</text>\n");
        double bar = 10.0 * scale;
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"18\" y1=\"{h - 20:F0}\" x2=\"{18 + bar:F0}\" y2=\"{h - 20:F0}\" stroke=\"#000\" stroke-width=\"2\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"18\" y=\"{h - 26:F0}\" font-size=\"12\">10 km</text>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{w - 44:F0}\" y=\"26\" font-size=\"13\">N &#8593;</text>\n");
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    private static void RenderPoint(StringBuilder sb, IReadOnlyList<CoursePoint> pts,
        (double X, double Y)[] xy, int i, IReadOnlyList<double>? barrelRadiiKm,
        double scale, Func<double, double> Sx, Func<double, double> Sy)
    {
        CoursePoint p = pts[i];
        ObservationZone? z = p.SourceZone;
        double cx = Sx(xy[i].X), cy = Sy(xy[i].Y);
        double barrelKm = Barrel(pts, i, barrelRadiiKm);
        double dir = ZoneDirection(pts, i);

        double sectorKm = z != null ? z.SectorRadiusMetres / 1000.0 : 0.0;
        double halfAngle = z?.SectorHalfAngleDegrees ?? 180.0;
        bool isLine = z?.IsLine ?? p.Type == CoursePointType.Start;

        if (isLine)
        {
            // Start line: perpendicular to the course direction, half-width = R1.
            double hw = (sectorKm > 0 ? sectorKm : barrelKm) * scale;
            (double ax, double ay) = Offset(cx, cy, dir + 90.0, hw);
            (double bx, double by) = Offset(cx, cy, dir - 90.0, hw);
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{ax:F1}\" y1=\"{ay:F1}\" x2=\"{bx:F1}\" y2=\"{by:F1}\" stroke=\"#0a7\" stroke-width=\"3\"/>\n");
            sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{cx:F1}\" cy=\"{cy:F1}\" r=\"3.5\" fill=\"#0a7\"/>\n");
            Label(sb, cx, cy, i, p.Name, "START line", "#053");
            return;
        }

        double sectorPx = sectorKm * scale;
        if (halfAngle < 180.0 && sectorPx > 0)
        {
            // Sector as an explicit polygon sampled from dir-A1 to dir+A1.
            int n = Math.Max(2, (int)(2 * halfAngle / 3));
            sb.Append(CultureInfo.InvariantCulture, $"<path d=\"M{cx:F1},{cy:F1} ");
            for (int k = 0; k <= n; k++)
            {
                double b = dir - halfAngle + 2 * halfAngle * k / n;
                (double px, double py) = Offset(cx, cy, b, sectorPx);
                sb.Append(CultureInfo.InvariantCulture, $"L{px:F1},{py:F1} ");
            }

            sb.Append("Z\" fill=\"#3a7bd5\" fill-opacity=\"0.15\" stroke=\"#3a7bd5\" stroke-width=\"1\"/>\n");
        }
        else if (sectorPx > 0 && barrelKm <= 0)
        {
            // Full-circle zone with no separate barrel (e.g. finish ring).
            sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{cx:F1}\" cy=\"{cy:F1}\" r=\"{sectorPx:F1}\" fill=\"#e07b39\" fill-opacity=\"0.15\" stroke=\"#e07b39\" stroke-width=\"1.5\"/>\n");
        }

        double barrelPx = barrelKm * scale;
        if (barrelPx > 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{cx:F1}\" cy=\"{cy:F1}\" r=\"{barrelPx:F1}\" fill=\"#e07b39\" fill-opacity=\"0.30\" stroke=\"#e07b39\" stroke-width=\"1.5\"/>\n");
        }

        sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{cx:F1}\" cy=\"{cy:F1}\" r=\"3.5\" fill=\"#111\"/>\n");

        var sub = new List<string>();
        if (halfAngle < 180.0 && sectorKm > 0)
        {
            sub.Add($"sector {sectorKm:F0}km/{halfAngle:F0}°");
        }

        if (p.Type == CoursePointType.Finish)
        {
            sub.Add($"finish ring {(sectorKm > 0 ? sectorKm : barrelKm):F0}km");
        }
        else if (barrelKm > 0)
        {
            sub.Add($"barrel {barrelKm:F1}km");
        }

        Label(sb, cx, cy, i, p.Name, string.Join("; ", sub), "#111");
    }

    /// <summary>Barrel radius (km) to draw at point i: supplied radii, else source zone.</summary>
    private static double Barrel(IReadOnlyList<CoursePoint> pts, int i, IReadOnlyList<double>? radiiKm)
    {
        if (radiiKm != null && i < radiiKm.Count && radiiKm[i] > 0)
        {
            return radiiKm[i];
        }

        ObservationZone? z = pts[i].SourceZone;
        return z != null ? z.BarrelRadiusMetres / 1000.0 : pts[i].DefaultRadiusKm;
    }

    private static double OuterRadiusKm(CoursePoint p, double barrelKm)
    {
        double sectorKm = p.SourceZone != null ? p.SourceZone.SectorRadiusMetres / 1000.0 : 0.0;
        return Math.Max(barrelKm, sectorKm);
    }

    /// <summary>
    /// Sector bisector direction (deg true) at point i, matching
    /// <c>ScoringEngine.ZoneDirection</c>: symmetric opens OUTWARD, directional
    /// styles face their neighbour. Returns 0 for a full-circle zone.
    /// </summary>
    private static double ZoneDirection(IReadOnlyList<CoursePoint> pts, int i)
    {
        CoursePoint p = pts[i];
        double? toPrev = i > 0
            ? Geodesy.BearingDegrees(p.Latitude, p.Longitude, pts[i - 1].Latitude, pts[i - 1].Longitude)
            : null;
        double? toNext = i < pts.Count - 1
            ? Geodesy.BearingDegrees(p.Latitude, p.Longitude, pts[i + 1].Latitude, pts[i + 1].Longitude)
            : null;

        ZoneStyle style = p.SourceZone?.Style ?? ZoneStyle.Symmetrical;
        switch (style)
        {
            case ZoneStyle.ToNext:
                return toNext ?? toPrev ?? 0.0;
            case ZoneStyle.ToPrevious:
                return toPrev ?? toNext ?? 0.0;
            case ZoneStyle.ToStart:
                return Geodesy.BearingDegrees(p.Latitude, p.Longitude, pts[0].Latitude, pts[0].Longitude);
            case ZoneStyle.Symmetrical:
            default:
                if (toPrev is double a && toNext is double b)
                {
                    return Geodesy.Normalize360(Bisector(a, b) + 180.0); // outward
                }

                return toPrev ?? toNext ?? 0.0;
        }
    }

    /// <summary>Angular bisector (deg true) of two bearings.</summary>
    private static double Bisector(double a, double b)
    {
        double ar = a * Math.PI / 180.0, br = b * Math.PI / 180.0;
        double x = Math.Cos(ar) + Math.Cos(br), y = Math.Sin(ar) + Math.Sin(br);
        if (Math.Abs(x) < 1e-12 && Math.Abs(y) < 1e-12)
        {
            return a;
        }

        return Geodesy.Normalize360(Math.Atan2(y, x) * 180.0 / Math.PI);
    }

    /// <summary>Screen point at bearing <paramref name="bearingDeg"/>, radius px, from (cx,cy).</summary>
    private static (double X, double Y) Offset(double cx, double cy, double bearingDeg, double px)
    {
        double r = bearingDeg * Math.PI / 180.0;
        return (cx + px * Math.Sin(r), cy - px * Math.Cos(r));
    }

    private static void Label(StringBuilder sb, double cx, double cy, int i, string name, string sub, string colour)
    {
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{cx + 8:F1}\" y=\"{cy - 8:F1}\" font-size=\"14\" font-weight=\"bold\" fill=\"{colour}\">{i}. {Escape(name)}</text>\n");
        if (!string.IsNullOrEmpty(sub))
        {
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{cx + 8:F1}\" y=\"{cy + 10:F1}\" font-size=\"11\" fill=\"#555\">{Escape(sub)}</text>\n");
        }
    }

    private static string Escape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
