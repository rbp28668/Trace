namespace Trace.Geometry;

/// <summary>
/// Turnpoint corner geometry (dht.md §3.1–§3.2): the angle through which the
/// track turns at a turnpoint, and the physical distance a glider saves by
/// rounding a barrel of radius R rather than flying to the exact centre.
/// </summary>
public static class LegGeometry
{
    private const double DegToRad = Math.PI / 180.0;

    /// <summary>
    /// Shallow-turn threshold (dht.md §4.1): turnpoints whose internal vertex
    /// angle exceeds this are too straight to usefully save distance. Tunable
    /// heuristic, not a fundamental limit.
    /// </summary>
    public const double MaxVertexAngleDegrees = 150.0;

    /// <summary>
    /// Track deflection angle Δφ at a turnpoint, given the inbound leg bearing
    /// (leg k−1) and the outbound leg bearing (leg k), both in degrees true.
    /// This is the absolute change of course, in [0, 180].
    /// </summary>
    public static double DeflectionAngle(double inboundBearing, double outboundBearing)
    {
        double diff = Math.Abs(outboundBearing - inboundBearing);
        return Math.Min(diff, 360.0 - diff);
    }

    /// <summary>
    /// Internal vertex angle θ = 180° − Δφ (dht.md §3.1). θ = 180° is a straight
    /// line through the turnpoint; θ = 0° is a full hairpin.
    /// </summary>
    public static double VertexAngle(double deflectionAngle) => 180.0 - deflectionAngle;

    /// <summary>
    /// Distance saved by rounding a barrel of radius <paramref name="radiusKm"/>
    /// at a turnpoint with the given deflection angle Δφ (dht.md §3.2):
    /// <c>D_saved = 2·R·sin(Δφ/2)</c>.
    /// </summary>
    public static double DistanceSaved(double radiusKm, double deflectionAngle)
        => 2.0 * radiusKm * Math.Sin(deflectionAngle * DegToRad / 2.0);

    /// <summary>
    /// Inverse of <see cref="DistanceSaved"/>: the barrel radius required to save
    /// a given distance at a turnpoint with the given deflection angle.
    /// Diverges as the turn approaches straight (Δφ → 0).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the turnpoint is too shallow to save distance (vertex angle
    /// &gt; <see cref="MaxVertexAngleDegrees"/>, i.e. Δφ below the cutoff).
    /// </exception>
    public static double RadiusForDistanceSaved(double distanceSavedKm, double deflectionAngle)
    {
        if (IsTooShallow(deflectionAngle))
        {
            throw new ArgumentException(
                $"Turnpoint too shallow (deflection {deflectionAngle:F1}°, vertex " +
                $"{VertexAngle(deflectionAngle):F1}° > {MaxVertexAngleDegrees}°) to save distance.",
                nameof(deflectionAngle));
        }

        return distanceSavedKm / (2.0 * Math.Sin(deflectionAngle * DegToRad / 2.0));
    }

    /// <summary>
    /// True when the turn is too shallow to be a usable variable turnpoint, i.e.
    /// the internal vertex angle exceeds <see cref="MaxVertexAngleDegrees"/>.
    /// </summary>
    public static bool IsTooShallow(double deflectionAngle)
        => VertexAngle(deflectionAngle) > MaxVertexAngleDegrees;
}
