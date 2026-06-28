namespace Trace.Model;

/// <summary>
/// A competing glider (dht.md §2.1). The handicap is supplied directly with the
/// fleet (BGA scheme, baseline 100); the reference cruise airspeed is derived
/// from the handicap via the fleet anchor speed, not stored per glider.
/// </summary>
public class Glider
{
    public Glider(string type, string registration, string compNumber, double handicap)
    {
        Type = type;
        Registration = registration;
        CompNumber = compNumber;
        Handicap = handicap;
    }

    /// <summary>Glider type, e.g. "ASW 19".</summary>
    public string Type { get; }

    /// <summary>Registration, e.g. "G-DDHT".</summary>
    public string Registration { get; }

    /// <summary>Competition number, e.g. "NX".</summary>
    public string CompNumber { get; }

    /// <summary>Performance index H relative to baseline 100 (BGA scheme).</summary>
    public double Handicap { get; }
}

/// <summary>
/// The competing fleet plus the single engine-level reference cruise airspeed
/// (dht.md §2.1): the nil-wind cruise speed of a notional H=100 glider, in km/h.
/// </summary>
public class Fleet
{
    private readonly List<Glider> gliders;

    public Fleet(IEnumerable<Glider> gliders, double vRefCruKmh)
    {
        this.gliders = new List<Glider>(gliders);
        VRefCruKmh = vRefCruKmh;
    }

    public IReadOnlyList<Glider> Gliders => gliders;

    /// <summary>Fleet anchor cruise airspeed at H=100, km/h.</summary>
    public double VRefCruKmh { get; }

    /// <summary>Highest handicap in the fleet (default reference handicap).</summary>
    public double MaxHandicap => gliders.Count == 0 ? 0.0 : gliders.Max(g => g.Handicap);
}
