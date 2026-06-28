namespace Trace.Wind;

/// <summary>
/// Windicapping and ground-speed derivation (dht.md §3.3, §5.2).
///
/// Speeds are km/h, bearings/directions degrees true. The wind direction ω is
/// the meteorological FROM direction (the heading the wind blows from), matching
/// dht.md §2.3.
/// </summary>
public static class Windicap
{
    private const double DegToRad = Math.PI / 180.0;

    /// <summary>
    /// Wind-override fraction (dht.md §5.2): if wind speed reaches this fraction
    /// of a glider's airspeed, the task should be downgraded or warned about.
    /// </summary>
    public const double WindOverrideFraction = 0.4;

    /// <summary>
    /// Airspeed scaling by handicap (dht.md §3.3.1): <c>Va(H) = VRefCru·H/100</c>,
    /// where <paramref name="vRefCru"/> is the nil-wind cruise airspeed of a
    /// notional H=100 glider.
    /// </summary>
    public static double Airspeed(double vRefCru, double handicap) => vRefCru * handicap / 100.0;

    /// <summary>
    /// Wind angle of attack γ = α − ω for a leg of bearing
    /// <paramref name="legBearing"/> in wind from <paramref name="windDirection"/>
    /// (dht.md §3.3.2).
    /// </summary>
    public static double WindAngle(double legBearing, double windDirection)
        => legBearing - windDirection;

    /// <summary>Crosswind component <c>W·sin(γ)</c> (dht.md §3.3.3).</summary>
    public static double Crosswind(double windSpeed, double windAngleDegrees)
        => windSpeed * Math.Sin(windAngleDegrees * DegToRad);

    /// <summary>
    /// Headwind component <c>W·cos(γ)</c> (dht.md §3.3.3). Positive is a
    /// headwind (slows the glider), negative is a tailwind.
    /// </summary>
    public static double Headwind(double windSpeed, double windAngleDegrees)
        => windSpeed * Math.Cos(windAngleDegrees * DegToRad);

    /// <summary>
    /// Leg ground speed via the wind-triangle solution (dht.md §3.3.4):
    /// <c>Vg = √(Va² − Wx²) − Wh</c>.
    /// </summary>
    /// <param name="airspeed">True airspeed Va (km/h).</param>
    /// <param name="legBearing">Leg track bearing α (degrees true).</param>
    /// <param name="windSpeed">Wind speed W (km/h).</param>
    /// <param name="windDirection">Wind FROM direction ω (degrees true).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the crosswind exceeds airspeed (|Wx| &gt; Va): the track is
    /// unflyable and the wind triangle has no real solution (dht.md §3.3 note).
    /// </exception>
    public static double GroundSpeed(
        double airspeed, double legBearing, double windSpeed, double windDirection)
    {
        double γ = WindAngle(legBearing, windDirection);
        double wx = Crosswind(windSpeed, γ);
        double wh = Headwind(windSpeed, γ);

        if (Math.Abs(wx) > airspeed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windSpeed),
                $"Crosswind component {wx:F1} exceeds airspeed {airspeed:F1} km/h; " +
                "the track is unflyable (no real wind-triangle solution).");
        }

        return Math.Sqrt(airspeed * airspeed - wx * wx) - wh;
    }

    /// <summary>
    /// True when the wind is strong enough relative to the glider's airspeed to
    /// trigger the dht.md §5.2 override (W ≥ 0.4·Va).
    /// </summary>
    public static bool IsWindOverride(double windSpeed, double airspeed)
        => windSpeed >= WindOverrideFraction * airspeed;
}
