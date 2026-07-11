namespace Trace.Geometry;

/// <summary>
/// Geodesic navigation on the WGS84 ellipsoid (dht.md §3.1) via Vincenty's
/// formulae. Distances are in kilometres and bearings in degrees true (0–360,
/// clockwise from north). The ellipsoidal model reproduces published gliding
/// task-sheet leg distances to ~0.1 km (the spherical approximation used
/// previously was ~0.3% short — see docs/task_sheets).
///
/// Coordinates follow the codebase convention used by
/// <see cref="TracePoint"/>/<see cref="TaskPoint"/>: latitude is "northings"
/// (north +ve) and longitude is "eastings" (east +ve, west −ve).
/// </summary>
public static class Geodesy
{
    /// <summary>Mean earth radius in kilometres (WGS84 mean radius).</summary>
    public const double EarthRadiusKm = 6371.0088;

    // WGS84 ellipsoid parameters (metres / dimensionless).
    private const double WgsA = 6378137.0;             // semi-major axis
    private const double WgsF = 1.0 / 298.257223563;   // flattening
    private const double WgsB = WgsA * (1.0 - WgsF);   // semi-minor axis

    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;
    private const double VincentyTolerance = 1e-12;    // ~0.06 mm in λ
    private const int VincentyMaxIterations = 200;

    /// <summary>
    /// Geodesic (shortest-path) distance between two points on the WGS84
    /// ellipsoid, in kilometres, via Vincenty's inverse formula.
    /// </summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        => Inverse(lat1, lon1, lat2, lon2).DistanceKm;

    /// <summary>
    /// Initial true bearing (forward azimuth) of the geodesic from point 1 to
    /// point 2 on the WGS84 ellipsoid, in degrees (0–360).
    /// </summary>
    public static double BearingDegrees(double lat1, double lon1, double lat2, double lon2)
        => Inverse(lat1, lon1, lat2, lon2).InitialBearingDeg;

    /// <summary>
    /// Destination point reached by travelling <paramref name="distanceKm"/> along
    /// the given initial true bearing from the start point, on the WGS84 ellipsoid
    /// (Vincenty's direct formula). Returns (latitude, longitude) in degrees.
    /// </summary>
    public static (double Lat, double Lon) PointAt(
        double lat, double lon, double bearingDegrees, double distanceKm)
    {
        double s = distanceKm * 1000.0; // metres
        double α1 = bearingDegrees * DegToRad;
        double sinα1 = Math.Sin(α1);
        double cosα1 = Math.Cos(α1);

        double tanU1 = (1.0 - WgsF) * Math.Tan(lat * DegToRad);
        double cosU1 = 1.0 / Math.Sqrt(1.0 + tanU1 * tanU1);
        double sinU1 = tanU1 * cosU1;

        double σ1 = Math.Atan2(tanU1, cosα1);
        double sinα = cosU1 * sinα1;
        double cosSqα = 1.0 - sinα * sinα;
        double uSq = cosSqα * (WgsA * WgsA - WgsB * WgsB) / (WgsB * WgsB);
        double A = 1.0 + uSq / 16384.0 * (4096.0 + uSq * (-768.0 + uSq * (320.0 - 175.0 * uSq)));
        double B = uSq / 1024.0 * (256.0 + uSq * (-128.0 + uSq * (74.0 - 47.0 * uSq)));

        double σ = s / (WgsB * A);
        double sinσ, cosσ, cos2σm;
        double σPrev;
        int iter = 0;
        do
        {
            cos2σm = Math.Cos(2.0 * σ1 + σ);
            sinσ = Math.Sin(σ);
            cosσ = Math.Cos(σ);
            double Δσ = B * sinσ * (cos2σm + B / 4.0 *
                (cosσ * (-1.0 + 2.0 * cos2σm * cos2σm) -
                 B / 6.0 * cos2σm * (-3.0 + 4.0 * sinσ * sinσ) * (-3.0 + 4.0 * cos2σm * cos2σm)));
            σPrev = σ;
            σ = s / (WgsB * A) + Δσ;
        }
        while (Math.Abs(σ - σPrev) > VincentyTolerance && ++iter < VincentyMaxIterations);

        double tmp = sinU1 * sinσ - cosU1 * cosσ * cosα1;
        double φ2 = Math.Atan2(
            sinU1 * cosσ + cosU1 * sinσ * cosα1,
            (1.0 - WgsF) * Math.Sqrt(sinα * sinα + tmp * tmp));
        double λ = Math.Atan2(
            sinσ * sinα1,
            cosU1 * cosσ - sinU1 * sinσ * cosα1);
        double C = WgsF / 16.0 * cosSqα * (4.0 + WgsF * (4.0 - 3.0 * cosSqα));
        double L = λ - (1.0 - C) * WgsF * sinα *
            (σ + C * sinσ * (cos2σm + C * cosσ * (-1.0 + 2.0 * cos2σm * cos2σm)));

        return (φ2 * RadToDeg, NormalizeLongitude(lon + L * RadToDeg));
    }

    /// <summary>
    /// Vincenty inverse solution: geodesic distance (km) and initial bearing (deg)
    /// between two points on the WGS84 ellipsoid. Coincident points return zero;
    /// the (rare) near-antipodal non-convergence falls back to the last iterate.
    /// </summary>
    private static (double DistanceKm, double InitialBearingDeg) Inverse(
        double lat1, double lon1, double lat2, double lon2)
    {
        double L = (lon2 - lon1) * DegToRad;
        double tanU1 = (1.0 - WgsF) * Math.Tan(lat1 * DegToRad);
        double cosU1 = 1.0 / Math.Sqrt(1.0 + tanU1 * tanU1);
        double sinU1 = tanU1 * cosU1;
        double tanU2 = (1.0 - WgsF) * Math.Tan(lat2 * DegToRad);
        double cosU2 = 1.0 / Math.Sqrt(1.0 + tanU2 * tanU2);
        double sinU2 = tanU2 * cosU2;

        double λ = L;
        double sinλ, cosλ, sinσ, cosσ, σ, sinα = 0.0, cosSqα = 1.0, cos2σm = 0.0;
        int iter = 0;
        double λPrev;
        do
        {
            sinλ = Math.Sin(λ);
            cosλ = Math.Cos(λ);
            double t1 = cosU2 * sinλ;
            double t2 = cosU1 * sinU2 - sinU1 * cosU2 * cosλ;
            sinσ = Math.Sqrt(t1 * t1 + t2 * t2);
            if (sinσ == 0.0)
            {
                return (0.0, 0.0); // coincident points
            }

            cosσ = sinU1 * sinU2 + cosU1 * cosU2 * cosλ;
            σ = Math.Atan2(sinσ, cosσ);
            sinα = cosU1 * cosU2 * sinλ / sinσ;
            cosSqα = 1.0 - sinα * sinα;
            cos2σm = cosSqα != 0.0 ? cosσ - 2.0 * sinU1 * sinU2 / cosSqα : 0.0; // equatorial line
            double C = WgsF / 16.0 * cosSqα * (4.0 + WgsF * (4.0 - 3.0 * cosSqα));
            λPrev = λ;
            λ = L + (1.0 - C) * WgsF * sinα *
                (σ + C * sinσ * (cos2σm + C * cosσ * (-1.0 + 2.0 * cos2σm * cos2σm)));
        }
        while (Math.Abs(λ - λPrev) > VincentyTolerance && ++iter < VincentyMaxIterations);

        double uSq = cosSqα * (WgsA * WgsA - WgsB * WgsB) / (WgsB * WgsB);
        double A = 1.0 + uSq / 16384.0 * (4096.0 + uSq * (-768.0 + uSq * (320.0 - 175.0 * uSq)));
        double B = uSq / 1024.0 * (256.0 + uSq * (-128.0 + uSq * (74.0 - 47.0 * uSq)));
        double sinσf = Math.Sin(σ);
        double cosσf = Math.Cos(σ);
        double Δσ = B * sinσf * (cos2σm + B / 4.0 *
            (cosσf * (-1.0 + 2.0 * cos2σm * cos2σm) -
             B / 6.0 * cos2σm * (-3.0 + 4.0 * sinσf * sinσf) * (-3.0 + 4.0 * cos2σm * cos2σm)));

        double distanceKm = WgsB * A * (σ - Δσ) / 1000.0;
        double bearing = Normalize360(Math.Atan2(
            cosU2 * Math.Sin(λ),
            cosU1 * sinU2 - sinU1 * cosU2 * Math.Cos(λ)) * RadToDeg);
        return (distanceKm, bearing);
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
