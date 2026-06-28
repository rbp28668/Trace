namespace Trace.Geometry;

/// <summary>
/// Great-circle navigation on a spherical earth (dht.md §3.1). Distances are in
/// kilometres and bearings in degrees true (0–360, clockwise from north).
///
/// Coordinates follow the codebase convention used by
/// <see cref="TracePoint"/>/<see cref="TaskPoint"/>: latitude is "northings"
/// (north +ve) and longitude is "eastings" (east +ve, west −ve).
///
/// A spherical model is used for speed and is accurate to ~0.3% — adequate at
/// task scale. A WGS84/ellipsoidal path can be added later (see
/// docs/implementation-plan.md §8).
/// </summary>
public static class Geodesy
{
    /// <summary>Mean earth radius in kilometres (WGS84 mean radius).</summary>
    public const double EarthRadiusKm = 6371.0088;

    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    /// <summary>
    /// Great-circle (orthodromic) distance between two points, in kilometres,
    /// via the spherical law of cosines with a haversine fallback for very
    /// small separations where the cosine form loses precision.
    /// </summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        double φ1 = lat1 * DegToRad;
        double φ2 = lat2 * DegToRad;
        double Δφ = (lat2 - lat1) * DegToRad;
        double Δλ = (lon2 - lon1) * DegToRad;

        // Haversine — numerically stable across the full range of separations.
        double a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                   Math.Cos(φ1) * Math.Cos(φ2) * Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Initial true bearing (forward azimuth) of the great circle from point 1
    /// to point 2, in degrees (0–360).
    /// </summary>
    public static double BearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        double φ1 = lat1 * DegToRad;
        double φ2 = lat2 * DegToRad;
        double Δλ = (lon2 - lon1) * DegToRad;

        double y = Math.Sin(Δλ) * Math.Cos(φ2);
        double x = Math.Cos(φ1) * Math.Sin(φ2) -
                   Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);
        double θ = Math.Atan2(y, x) * RadToDeg;
        return Normalize360(θ);
    }

    /// <summary>
    /// Destination point reached by travelling <paramref name="distanceKm"/>
    /// along the given initial true bearing from the start point. Returns
    /// (latitude, longitude) in degrees.
    /// </summary>
    public static (double Lat, double Lon) PointAt(
        double lat, double lon, double bearingDegrees, double distanceKm)
    {
        double δ = distanceKm / EarthRadiusKm; // angular distance
        double θ = bearingDegrees * DegToRad;
        double φ1 = lat * DegToRad;
        double λ1 = lon * DegToRad;

        double sinφ2 = Math.Sin(φ1) * Math.Cos(δ) +
                       Math.Cos(φ1) * Math.Sin(δ) * Math.Cos(θ);
        double φ2 = Math.Asin(sinφ2);
        double λ2 = λ1 + Math.Atan2(
            Math.Sin(θ) * Math.Sin(δ) * Math.Cos(φ1),
            Math.Cos(δ) - Math.Sin(φ1) * sinφ2);

        return (φ2 * RadToDeg, NormalizeLongitude(λ2 * RadToDeg));
    }

    /// <summary>Wraps an angle to [0, 360).</summary>
    public static double Normalize360(double degrees)
    {
        double d = degrees % 360.0;
        return d < 0 ? d + 360.0 : d;
    }

    private static double NormalizeLongitude(double degrees)
    {
        double d = (degrees + 540.0) % 360.0;
        return d - 180.0;
    }
}
