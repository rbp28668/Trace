namespace Trace.Wind;

/// <summary>
/// A single task leg for windicapping calculations: its baseline distance (km)
/// and track bearing (degrees true).
/// </summary>
public readonly record struct Leg(double DistanceKm, double BearingDegrees);

/// <summary>
/// Reference-fleet metrics and simulated task durations (dht.md §3.4).
///
/// All speeds km/h, distances km; durations are returned in hours (distance/speed
/// with km and km/h), convertible by the caller.
/// </summary>
public static class TaskMetrics
{
    /// <summary>
    /// Total baseline distance D_Ref across the legs (km). For the reference
    /// glider this is the full course flown at the minimum barrel radius.
    /// </summary>
    public static double TotalDistanceKm(IReadOnlyList<Leg> legs)
    {
        double sum = 0.0;
        foreach (Leg leg in legs)
        {
            sum += leg.DistanceKm;
        }

        return sum;
    }

    /// <summary>
    /// Simulated task duration (hours) for a glider of the given airspeed flying
    /// the supplied legs in the given wind: <c>T = Σ Lk / Vg,k</c> (dht.md §3.4).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Propagated from <see cref="Windicap.GroundSpeed"/> when a leg is unflyable
    /// in the given wind.
    /// </exception>
    public static double Duration(
        IReadOnlyList<Leg> legs, double airspeed, double windSpeed, double windDirection)
    {
        double total = 0.0;
        foreach (Leg leg in legs)
        {
            double vg = Windicap.GroundSpeed(airspeed, leg.BearingDegrees, windSpeed, windDirection);
            if (vg <= 0.0)
            {
                // Zero/negative ground speed: the glider cannot progress on this
                // leg (dht.md §5.2 — should already be caught by the override).
                return double.PositiveInfinity;
            }

            total += leg.DistanceKm / vg;
        }

        return total;
    }

    /// <summary>
    /// Reference task duration T_Ref (hours) for the reference glider flying the
    /// baseline legs (dht.md §3.4).
    /// </summary>
    public static double ReferenceDuration(
        IReadOnlyList<Leg> legs, double referenceAirspeed, double windSpeed, double windDirection)
        => Duration(legs, referenceAirspeed, windSpeed, windDirection);
}
