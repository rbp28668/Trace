using System.Globalization;
using System.Text;

namespace Trace;

public class IGCFile
{
    private readonly TraceList trace = new();
    private readonly List<Extension> extensions = new();
    private bool taskDeclarationSeen;

    public IGCFile()
    {
        Date = DateTime.UtcNow; // default to now

        // Add standard extensions for fix accuracy and satellites in use.
        // I023638FXA3940SIU
        extensions.Add(new Extension("FXA", 36, 38));
        extensions.Add(new Extension("SIU", 39, 40));
    }

    /// <summary>Index of the ladder flight that produced this.</summary>
    public int FlightIndex { get; set; }

    /// <summary>AFLARQK</summary>
    public string LoggerId { get; set; } = string.Empty;

    /// <summary>HFDTE300515</summary>
    public DateTime Date { get; set; }

    /// <summary>HFFXA500</summary>
    public int FixAccuracy { get; set; }

    /// <summary>HFPLTPilotincharge:BRUCE PORTEOUS</summary>
    public string P1 { get; set; } = string.Empty;

    /// <summary>HPCM2Crew2:</summary>
    public string P2 { get; set; } = string.Empty;

    /// <summary>HFGTYGliderType:LS7</summary>
    public string GliderType { get; set; } = string.Empty;

    /// <summary>HFGIDGliderID:G-DFOG</summary>
    public string Registration { get; set; } = string.Empty;

    /// <summary>HFDTM100GPSDatum:WGS84</summary>
    public string Datum { get; set; } = string.Empty;

    /// <summary>HFRFWFirmwareVersion:Flarm-IGC06.01</summary>
    public string LoggerFirmware { get; set; } = string.Empty;

    /// <summary>HFRHWHardwareVersion:LXN-Flarm-IGC</summary>
    public string LoggerHardware { get; set; } = string.Empty;

    /// <summary>HFFTYFRType:LXN Red Box Flarm</summary>
    public string LoggerType { get; set; } = string.Empty;

    /// <summary>HFGPSu-blox:TIM-LP,16,8191</summary>
    public string GpsType { get; set; } = string.Empty;

    /// <summary>HFPRSPressAltSensor:Intersema MS5534B,8191</summary>
    public string PressureSensor { get; set; } = string.Empty;

    /// <summary>HFCCLCompetitionClass:Standard</summary>
    public string CompetitionClass { get; set; } = string.Empty;

    /// <summary>HFCIDCompetitionID:952</summary>
    public string CompetitionId { get; set; } = string.Empty;

    public TraceList Trace => trace;

    public void SetTrace(IEnumerable<TracePoint> points)
    {
        trace.Clear();
        trace.AddRange(points);
    }

    public List<Extension> Extensions => extensions;

    /// <summary>
    /// The task (flight) declaration, or <c>null</c> if the file contains no
    /// C records.
    /// </summary>
    public Task? Task { get; set; }

    public void Parse(string path)
    {
        using var reader = new StreamReader(path);
        Parse(reader);
    }

    public void Parse(TextReader reader)
    {
        // Remove default extensions as file should define them.
        extensions.Clear();
        taskDeclarationSeen = false;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                switch (line[0])
                {
                    case 'B':
                        trace.Add(new TracePoint(line, extensions));
                        break;

                    case 'H':
                        ParseInfoRecord(line);
                        break;

                    case 'C':
                        ParseTaskRecord(line);
                        break;

                    case 'I':
                        ParseExtensionRecord(line);
                        break;

                    case 'A':
                        LoggerId = line.Substring(1);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invalid line ignored {e.Message}");
                Console.WriteLine(line);
            }
        }
    }

    /// <summary>
    /// Parses an I or extension record. This defines what extensions are
    /// included in a B or fix record.
    /// </summary>
    private void ParseExtensionRecord(string line)
    {
        line = line.Trim();
        int count = int.Parse(line.Substring(1, 2), CultureInfo.InvariantCulture);
        int idx = 3; // point at first char after extension count
        for (int i = 0; i < count; ++i)
        {
            int start = int.Parse(line.Substring(idx, 2), CultureInfo.InvariantCulture);
            int finish = int.Parse(line.Substring(idx + 2, 2), CultureInfo.InvariantCulture);
            string typeCode = line.Substring(idx + 4, 3);

            extensions.Add(new Extension(typeCode, start, finish));

            idx += 7;
        }
    }

    private void ParseInfoRecord(string line)
    {
        line = line.Trim();

        if (line.StartsWith("HFDTE", StringComparison.Ordinal)) // HFDTE300515 or HFDTEDATE:060426,1
        {
            string ddmmyy = line.Substring(5);
            // Modern form: "HFDTEDATE:DDMMYY,NN" — strip the "DATE:" prefix and
            // any trailing flight number after the comma.
            if (ddmmyy.StartsWith("DATE:", StringComparison.Ordinal))
            {
                ddmmyy = ddmmyy.Substring(5);
            }
            int comma = ddmmyy.IndexOf(',');
            if (comma != -1)
            {
                ddmmyy = ddmmyy.Substring(0, comma);
            }

            int day = int.Parse(ddmmyy.Substring(0, 2), CultureInfo.InvariantCulture);
            int month = int.Parse(ddmmyy.Substring(2, 2), CultureInfo.InvariantCulture);
            int year = int.Parse(ddmmyy.Substring(4, 2), CultureInfo.InvariantCulture);
            // Y2K style bodge.
            year += year < 70 ? 2000 : 1900;
            Date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        }
        else if (line.StartsWith("HFFXA", StringComparison.Ordinal))
        {
            FixAccuracy = int.Parse(line.Substring(5), CultureInfo.InvariantCulture);
        }
        else
        {
            int idx = line.IndexOf(':');
            string tag = line.Substring(0, 5);
            if (idx != -1)
            {
                string value = line.Substring(idx + 1);
                switch (tag)
                {
                    case "HFPLT": P1 = value; break;
                    case "HPCM2": P2 = value; break;
                    case "HFGTY": GliderType = value; break;
                    case "HFGID": Registration = value; break;
                    case "HFDTM": Datum = value; break;
                    case "HFRFW": LoggerFirmware = value; break;
                    case "HFRHW": LoggerHardware = value; break;
                    case "HFFTY": LoggerType = value; break;
                    case "HFGPS": GpsType = value; break;
                    case "HFPRS": PressureSensor = value; break;
                    case "HFCCL": CompetitionClass = value; break;
                    case "HFCID": CompetitionId = value; break;
                }
            }
        }
    }

    /// <summary>
    /// Parses a C record. The first C record in a file is the task declaration
    /// header; subsequent C records are the task's waypoints.
    /// </summary>
    private void ParseTaskRecord(string line)
    {
        if (!taskDeclarationSeen)
        {
            Task = new Task();
            Task.ParseDeclaration(line);
            taskDeclarationSeen = true;
        }
        else
        {
            Task?.AddPoint(line);
        }
    }

    public void Write(TextWriter os)
    {
        // Logger ID
        if (!string.IsNullOrEmpty(LoggerId))
        {
            os.WriteLine($"A{LoggerId}");
        }

        // Date e.g. HFDTE170515  date as DDMMYY
        DateTime t = Date.ToUniversalTime();
        os.WriteLine(
            "HFDTE" +
            t.Day.ToString("D2", CultureInfo.InvariantCulture) +
            t.Month.ToString("D2", CultureInfo.InvariantCulture) +
            (t.Year % 100).ToString("D2", CultureInfo.InvariantCulture));

        // Fix accuracy e.g. HFFXA500
        os.WriteLine($"HFFXA{FixAccuracy}");

        // HFPLTPilotincharge : Bruce Porteous
        if (!string.IsNullOrEmpty(P1)) os.WriteLine($"HFPLTPilotincharge:{P1}");

        // HPCM2Crew2 :
        if (!string.IsNullOrEmpty(P2)) os.WriteLine($"HPCM2Crew2:{P2}");

        // HFGTYGliderType:LS-7
        if (!string.IsNullOrEmpty(GliderType)) os.WriteLine($"HFGTYGliderType:{GliderType}");

        // HFGIDGliderID : G-DFOG
        if (!string.IsNullOrEmpty(Registration)) os.WriteLine($"HFGIDGliderID:{Registration}");

        // HFDTM100GPSDatum : WGS84
        if (!string.IsNullOrEmpty(Datum)) os.WriteLine($"HFDTM100GPSDatum:{Datum}");

        // HFRFWFirmwareVersion : Flarm-IGC06.01
        if (!string.IsNullOrEmpty(LoggerFirmware)) os.WriteLine($"HFRFWFirmwareVersion:{LoggerFirmware}");

        // HFRHWHardwareVersion : LXN-Flarm-IGC
        if (!string.IsNullOrEmpty(LoggerHardware)) os.WriteLine($"HFRHWHardwareVersion:{LoggerHardware}");

        // HFFTYFRType : LXN Red Box Flarm
        if (!string.IsNullOrEmpty(LoggerType)) os.WriteLine($"HFFTYFRType{LoggerType}");

        // HFGPSu-blox : TIM-LP,16,8191
        if (!string.IsNullOrEmpty(GpsType)) os.WriteLine($"HFGPSu-blox{GpsType}");

        // HFPRSPressAltSensor : Intersema MS5534B,8191
        if (!string.IsNullOrEmpty(PressureSensor)) os.WriteLine($"HFPRSPressAltSensor:{PressureSensor}");

        // HFCCLCompetitionClass : Standard
        if (!string.IsNullOrEmpty(CompetitionClass)) os.WriteLine($"HFCCLCompetitionClass:{CompetitionClass}");

        // HFCIDCompetitionID : 952
        if (!string.IsNullOrEmpty(CompetitionId)) os.WriteLine($"HFCIDCompetitionID:{CompetitionId}");

        // Extension records
        // I023638FXA3940SIU
        if (extensions.Count > 0)
        {
            var sb = new StringBuilder();
            sb.Append('I');
            sb.Append(extensions.Count.ToString("D2", CultureInfo.InvariantCulture));
            foreach (Extension e in extensions)
            {
                sb.Append(e.Start.ToString("D2", CultureInfo.InvariantCulture));
                sb.Append(e.Finish.ToString("D2", CultureInfo.InvariantCulture));
                sb.Append(e.TypeCode);
            }

            os.WriteLine(sb.ToString());
        }

        // Task declaration ("C") records
        Task?.Write(os);

        // Trace points (B records)
        foreach (TracePoint point in trace)
        {
            point.Write(os, extensions);
        }
    }
}
