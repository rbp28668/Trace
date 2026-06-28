using Trace.Airspace;

namespace Trace.Geometry;

/// <summary>
/// Planar polygon tests on geographic points. At task scale (and for airspace
/// boundaries already tessellated to vertices) treating lat/lon as planar
/// coordinates is accurate enough for containment/intersection; a small
/// longitude scaling by cos(latitude) keeps east–west distances proportionate.
/// </summary>
public static class Polygon
{
    /// <summary>
    /// True if the point lies within the polygon (ray-casting, even-odd rule).
    /// Longitudes are scaled by cos(lat) about the test point so the test is not
    /// distorted at higher latitudes.
    /// </summary>
    public static bool Contains(IReadOnlyList<GeoPoint> polygon, double lat, double lon)
    {
        double scale = Math.Cos(lat * Math.PI / 180.0);
        double x = lon * scale;
        double y = lat;

        bool inside = false;
        int n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double xi = polygon[i].Longitude * scale, yi = polygon[i].Latitude;
            double xj = polygon[j].Longitude * scale, yj = polygon[j].Latitude;

            bool crosses = (yi > y) != (yj > y) &&
                           x < (xj - xi) * (y - yi) / (yj - yi) + xi;
            if (crosses)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// True if the circle (centre lat/lon, radius km) intersects or lies within
    /// the polygon: either the centre is inside, or any edge passes within the
    /// radius of the centre.
    /// </summary>
    public static bool IntersectsCircle(IReadOnlyList<GeoPoint> polygon, double lat, double lon, double radiusKm)
    {
        if (Contains(polygon, lat, lon))
        {
            return true;
        }

        int n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double dist = DistancePointToSegmentKm(lat, lon,
                polygon[j].Latitude, polygon[j].Longitude,
                polygon[i].Latitude, polygon[i].Longitude);
            if (dist <= radiusKm)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Shortest great-circle-ish distance (km) from a point to a segment,
    /// computed in a local planar projection about the point.
    /// </summary>
    public static double DistancePointToSegmentKm(double lat, double lon,
        double latA, double lonA, double latB, double lonB)
    {
        const double kmPerDegLat = 111.195; // mean km per degree latitude
        double scale = Math.Cos(lat * Math.PI / 180.0);

        double px = (lon) * scale * kmPerDegLat;
        double py = lat * kmPerDegLat;
        double ax = lonA * scale * kmPerDegLat;
        double ay = latA * kmPerDegLat;
        double bx = lonB * scale * kmPerDegLat;
        double by = latB * kmPerDegLat;

        double dx = bx - ax, dy = by - ay;
        double lenSq = dx * dx + dy * dy;
        double t = lenSq > 0 ? ((px - ax) * dx + (py - ay) * dy) / lenSq : 0.0;
        t = Math.Clamp(t, 0.0, 1.0);

        double cx = ax + t * dx, cy = ay + t * dy;
        double ex = px - cx, ey = py - cy;
        return Math.Sqrt(ex * ex + ey * ey);
    }
}
