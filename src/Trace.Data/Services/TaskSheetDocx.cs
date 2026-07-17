using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Trace.Data.Entities;
using Trace.Geometry;
using Trace.Model;
using Trace.Planning;

namespace Trace.Data.Services;

/// <summary>
/// Generates an editable Word (.docx) task sheet in the DHT layout: a header, the
/// points table (Code/Name/Lat/Lon/Course/Dist/Type/Radius), the "Variable Barrel
/// Sizes" table (one row per handicap) and a "Task Properties" block. Notes, ATC
/// frequencies and the licensee line are emitted as editable placeholders — the
/// operator fills them in Word. Built with OpenXML so no Word install is needed.
/// </summary>
public static class TaskSheetDocx
{
    // Shared table-header fill (dark grey), matching the reference sheet.
    private const string HeaderFill = "595959";
    private const string HeaderText = "FFFFFF";

    /// <summary>
    /// Builds the .docx bytes for a planned task. <paramref name="computation"/> is
    /// the shared optimiser result (so the barrel table matches the stored plan);
    /// <paramref name="date"/> is the day's date for the header.
    /// </summary>
    public static byte[] Build(
        PlanningService.PlanComputation computation, string title, DateOnly date)
    {
        Course course = computation.Course;
        FleetPlan plan = computation.Plan;
        double vRefCru = computation.VRefCruKmh;
        CompetitionTask task = computation.Task;

        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(
            stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();
            main.Document.Append(body);

            AppendHeader(body, title, date);
            AppendPointsTable(body, course);
            AppendSpacer(body);
            AppendHeading(body, "Variable Barrel Sizes");
            AppendBarrelAndProperties(body, course, plan, vRefCru, task);
            AppendSpacer(body);
            AppendFooter(body);

            body.Append(PageMargins());
            main.Document.Save();
        }

        return stream.ToArray();
    }

    // --- Header -----------------------------------------------------------

    private static void AppendHeader(Body body, string title, DateOnly date)
    {
        // Two-column header row: "Handicap Task" left, task name + date right.
        var table = BorderlessTable();
        var row = new TableRow();

        row.Append(HeaderCell(
            new[] { RunParagraph("Handicap Task", bold: true, sizePt: 20) },
            width: 6000, align: JustificationValues.Left));
        row.Append(HeaderCell(
            new[]
            {
                RunParagraph(title, bold: true, sizePt: 13, align: JustificationValues.Right),
                RunParagraph(date.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture),
                    bold: true, sizePt: 13, align: JustificationValues.Right),
            },
            width: 3600, align: JustificationValues.Right));

        table.Append(row);
        body.Append(table);
        AppendSpacer(body);
    }

    // --- Points table -----------------------------------------------------

    private static void AppendPointsTable(Body body, Course course)
    {
        string[] headers = { "Code", "Name", "Latitude", "Longitude", "Course", "Dist", "Type", "Radius" };
        var table = GridTable();
        table.Append(HeaderRow(headers));

        IReadOnlyList<CoursePoint> pts = course.Points;
        for (int i = 0; i < pts.Count; i++)
        {
            CoursePoint p = pts[i];
            string type = TypeLabel(p.Type);
            string course_ = i == 0 ? "" : Bearing(pts[i - 1], p);
            string dist = i == 0 ? "" : LegDistanceKm(pts[i - 1], p);
            string radius = RadiusLabel(p);

            table.Append(DataRow(
                ShortCode(p.Name), p.Name,
                FormatLat(p.Latitude), FormatLon(p.Longitude),
                course_, dist, type, radius));
        }

        body.Append(table);
    }

    private static string TypeLabel(CoursePointType type) => type switch
    {
        CoursePointType.Start => "Start",
        CoursePointType.Finish => "Finish",
        CoursePointType.Checkpoint => "Checkpoint",
        _ => "Variable",
    };

    private static string RadiusLabel(CoursePoint p) => p.Type switch
    {
        CoursePointType.Turnpoint => "See below",
        _ => FormatRadiusKm(p.DefaultRadiusKm),
    };

    private static string FormatRadiusKm(double km)
    {
        // Whole numbers as "5K", fractional as "0.5K" (matching the reference).
        string n = km == Math.Floor(km)
            ? ((int)km).ToString(CultureInfo.InvariantCulture)
            : km.ToString("0.#", CultureInfo.InvariantCulture);
        return n + "K";
    }

    private static string Bearing(CoursePoint from, CoursePoint to)
    {
        double b = Geodesy.BearingDegrees(from.Latitude, from.Longitude, to.Latitude, to.Longitude);
        return Math.Round(b).ToString("0", CultureInfo.InvariantCulture);
    }

    private static string LegDistanceKm(CoursePoint from, CoursePoint to)
    {
        double d = Geodesy.DistanceKm(from.Latitude, from.Longitude, to.Latitude, to.Longitude);
        return d.ToString("0.0", CultureInfo.InvariantCulture);
    }

    // --- Variable Barrel Sizes + Task Properties (side by side) -----------

    private static void AppendBarrelAndProperties(
        Body body, Course course, FleetPlan plan, double vRefCru, CompetitionTask task)
    {
        // Outer two-column borderless table: barrel table left, properties right.
        var outer = BorderlessTable();
        var row = new TableRow();

        var leftCell = new TableCell(CellWidth(5200));
        leftCell.Append(BarrelTable(course, plan, vRefCru));
        leftCell.Append(RunParagraph(
            "Act Dist is the distance to fly through the air mass — increases with " +
            "wind speed when task is windicapped.", italic: true, sizePt: 8));
        row.Append(leftCell);

        var rightCell = new TableCell(CellWidth(4400));
        AppendProperties(rightCell, course, plan, vRefCru, task);
        row.Append(rightCell);

        outer.Append(row);
        body.Append(outer);
    }

    private static Table BarrelTable(Course course, FleetPlan plan, double vRefCru)
    {
        var table = GridTable();
        table.Append(HeaderRow(new[] { "Handicap", "Radius", "Act Dist", "Hcp Dist" }));

        List<int> varIdx = VariableTurnpointIndices(course).ToList();
        var byHandicap = plan.Plans
            .GroupBy(p => p.Glider.Handicap)
            .OrderByDescending(g => g.Key)
            .Select(g => g.First());

        foreach (GliderPlan gp in byHandicap)
        {
            double h = gp.Glider.Handicap;
            double radiusKm = varIdx.Count > 0 ? gp.RadiiKm[varIdx[0]] : 0.0;
            double actDist = vRefCru * h / 100.0 * gp.DurationHours; // air-mass distance
            double hcpDist = h > 0 ? actDist * 100.0 / h : 0.0;

            table.Append(DataRow(
                h.ToString("0.0", CultureInfo.InvariantCulture),
                F1(radiusKm), F1(actDist), F1(hcpDist)));
        }

        return table;
    }

    private static void AppendProperties(
        TableCell cell, Course course, FleetPlan plan, double vRefCru, CompetitionTask task)
    {
        cell.Append(ShadedLabel("Task Properties"));

        double taskLengthKm = plan.ReferenceDistanceKm;
        string wind = task.WindSpeedKmh is double ws && task.WindDirDeg is double wd
            ? $"{ws:0.#} km/h / {wd:0}°"
            : "—";

        // Derived, factual properties.
        foreach (string line in new[]
        {
            "Distances in KILOMETERS",
            $"Task Length: {taskLengthKm:0.0}K",
            "Calculation Scheme: WINDICAPPED",
            $"Wind Speed / Direction: {wind}",
            "Start Shape: ZONE",
            "Variable TP Shape: THISTLE",
            "Variable TP Sector / Angle: 10K / 90deg",
            "Checkpoint Shape: THISTLE",
            "Checkpoint Sector / Angle: 10K / 90deg",
            "Finish Line Shape: RING",
        })
        {
            cell.Append(RunParagraph(line, sizePt: 10));
        }

        // Editable placeholders: the operator fills these in Word.
        cell.Append(RunParagraph("", sizePt: 6));
        cell.Append(RunParagraph("Notes:", bold: true, sizePt: 10));
        cell.Append(Placeholder("[ add task notes here ]"));
        cell.Append(RunParagraph("", sizePt: 6));
        cell.Append(RunParagraph("ATC Frequencies:", bold: true, sizePt: 10));
        cell.Append(Placeholder("[ add ATC frequencies here ]"));
    }

    private static IEnumerable<int> VariableTurnpointIndices(Course course)
    {
        for (int i = 0; i < course.Points.Count; i++)
        {
            if (course.Points[i].Type == CoursePointType.Turnpoint)
            {
                yield return i;
            }
        }
    }

    // --- Footer -----------------------------------------------------------

    private static void AppendFooter(Body body)
    {
        var table = BorderlessTable();
        var row = new TableRow();
        row.Append(HeaderCell(
            new[] { Placeholder("[ © author / year ]") }, 4700, JustificationValues.Left));
        row.Append(HeaderCell(
            new[] { Placeholder("[ Licensed to … ]", align: JustificationValues.Right) },
            4700, JustificationValues.Right));
        table.Append(row);
        body.Append(table);
    }

    // --- Coordinate formatting (DDMM.mmm N / DDDMM.mmm E) ------------------

    private static string FormatLat(double deg)
    {
        char hemi = deg >= 0 ? 'N' : 'S';
        double a = Math.Abs(deg);
        int d = (int)a;
        double min = (a - d) * 60.0;
        return $"{d:00} {min:00.000} {hemi}";
    }

    private static string FormatLon(double deg)
    {
        char hemi = deg >= 0 ? 'E' : 'W';
        double a = Math.Abs(deg);
        int d = (int)a;
        double min = (a - d) * 60.0;
        return $"{d:000} {min:00.000} {hemi}";
    }

    private static string ShortCode(string name)
    {
        string upper = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return upper.Length <= 3 ? upper : upper[..3];
    }

    private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    // --- OpenXML building blocks ------------------------------------------

    private static void AppendHeading(Body body, string text) =>
        body.Append(RunParagraph(text, bold: true, sizePt: 18));

    private static void AppendSpacer(Body body) =>
        body.Append(RunParagraph("", sizePt: 6));

    private static Paragraph RunParagraph(
        string text, bool bold = false, bool italic = false, int sizePt = 11,
        JustificationValues? align = null)
    {
        var runProps = new RunProperties();
        if (bold) { runProps.Append(new Bold()); }
        if (italic) { runProps.Append(new Italic()); }
        runProps.Append(new FontSize { Val = (sizePt * 2).ToString(CultureInfo.InvariantCulture) });

        var run = new Run();
        run.Append(runProps);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        var para = new Paragraph();
        var pProps = new ParagraphProperties(
            new SpacingBetweenLines { After = "40", Before = "0" });
        if (align is JustificationValues j)
        {
            pProps.Append(new Justification { Val = j });
        }

        para.Append(pProps);
        para.Append(run);
        return para;
    }

    private static Paragraph Placeholder(string text, JustificationValues? align = null)
    {
        var runProps = new RunProperties();
        runProps.Append(new Italic());
        runProps.Append(new Color { Val = "808080" });
        runProps.Append(new FontSize { Val = "20" });

        var run = new Run();
        run.Append(runProps);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        var para = new Paragraph();
        var pProps = new ParagraphProperties(new SpacingBetweenLines { After = "40" });
        if (align is JustificationValues j)
        {
            pProps.Append(new Justification { Val = j });
        }

        para.Append(pProps);
        para.Append(run);
        return para;
    }

    private static Paragraph ShadedLabel(string text)
    {
        var runProps = new RunProperties(
            new Bold(),
            new Color { Val = HeaderText },
            new FontSize { Val = "22" });
        var run = new Run(runProps, new Text(text));

        var pProps = new ParagraphProperties(
            new Shading { Val = ShadingPatternValues.Clear, Fill = HeaderFill },
            new SpacingBetweenLines { After = "40" });
        return new Paragraph(pProps, run);
    }

    private static Table GridTable()
    {
        var props = new TableProperties(
            new TableBorders(
                Border<TopBorder>(), Border<BottomBorder>(), Border<LeftBorder>(),
                Border<RightBorder>(), Border<InsideHorizontalBorder>(), Border<InsideVerticalBorder>()),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });
        return new Table(props);
    }

    private static Table BorderlessTable()
    {
        var props = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });
        return new Table(props);
    }

    private static T Border<T>() where T : BorderType, new() =>
        new() { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" };

    private static TableRow HeaderRow(string[] headers)
    {
        var row = new TableRow();
        foreach (string h in headers)
        {
            var props = new TableCellProperties(
                new Shading { Val = ShadingPatternValues.Clear, Fill = HeaderFill });
            var run = new Run(
                new RunProperties(new Bold(), new Color { Val = HeaderText },
                    new FontSize { Val = "18" }),
                new Text(h));
            row.Append(new TableCell(props,
                new Paragraph(new ParagraphProperties(
                    new SpacingBetweenLines { After = "20" }), run)));
        }

        return row;
    }

    private static TableRow DataRow(params string[] cells)
    {
        var row = new TableRow();
        foreach (string c in cells)
        {
            var run = new Run(new RunProperties(new FontSize { Val = "18" }),
                new Text(c) { Space = SpaceProcessingModeValues.Preserve });
            row.Append(new TableCell(
                new Paragraph(new ParagraphProperties(
                    new SpacingBetweenLines { After = "20" }), run)));
        }

        return row;
    }

    private static TableCell HeaderCell(
        Paragraph[] paragraphs, int width, JustificationValues align)
    {
        var cell = new TableCell(CellWidth(width));
        foreach (Paragraph p in paragraphs)
        {
            cell.Append(p);
        }

        return cell;
    }

    private static TableCellProperties CellWidth(int dxa) =>
        new(new TableCellWidth { Width = dxa.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa });

    private static SectionProperties PageMargins() =>
        new(new PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720 });
}
