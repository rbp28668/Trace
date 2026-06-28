using Trace.Geometry;
using Trace.Model;

namespace Trace.Planning;

/// <summary>
/// Per-turnpoint geometry derived from a <see cref="Course"/>: the centre-to-centre
/// leg distances and bearings, and the track deflection angle at each interior
/// turnpoint (dht.md §3.1). Bearings are treated as fixed centre-to-centre values;
/// per the spec the change from barrel entry/exit is marginal and absorbed by the
/// optimiser's convergence loop.
/// </summary>
public class CourseGeometry
{
    public CourseGeometry(Course course)
    {
        IReadOnlyList<CoursePoint> pts = course.Points;
        var legs = new List<TaskLeg>();
        for (int k = 0; k < pts.Count - 1; k++)
        {
            CoursePoint a = pts[k];
            CoursePoint b = pts[k + 1];
            double dist = Geodesy.DistanceKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
            double bearing = Geodesy.BearingDegrees(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
            legs.Add(new TaskLeg(dist, bearing));
        }

        Legs = legs;

        // Deflection angle at each interior point i (connecting leg i-1 and leg i).
        var deflections = new double[pts.Count];
        for (int i = 1; i < pts.Count - 1; i++)
        {
            deflections[i] = LegGeometry.DeflectionAngle(legs[i - 1].BearingDegrees, legs[i].BearingDegrees);
        }

        Deflections = deflections;
    }

    /// <summary>Centre-to-centre legs in course order.</summary>
    public IReadOnlyList<TaskLeg> Legs { get; }

    /// <summary>
    /// Track deflection angle (degrees) at each course point, indexed by point.
    /// Index 0 and the last index are 0 (start/finish have no turn).
    /// </summary>
    public IReadOnlyList<double> Deflections { get; }

    /// <summary>Total centre-to-centre course distance (km).</summary>
    public double TotalCentreDistanceKm
    {
        get
        {
            double sum = 0.0;
            foreach (TaskLeg leg in Legs)
            {
                sum += leg.DistanceKm;
            }

            return sum;
        }
    }
}

/// <summary>A centre-to-centre course leg: baseline distance (km) and bearing (deg true).</summary>
public readonly record struct TaskLeg(double DistanceKm, double BearingDegrees);
