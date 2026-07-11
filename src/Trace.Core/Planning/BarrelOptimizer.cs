using Trace.Geometry;
using Trace.Model;
using Trace.Wind;

namespace Trace.Planning;

/// <summary>Bounds and tolerances for the barrel optimiser (dht.md §4.1).</summary>
public class OptimizerOptions
{
    /// <summary>Absolute lower bound R_min (km).</summary>
    public double RMinKm { get; init; } = 0.5;

    /// <summary>Absolute upper bound R_max (km).</summary>
    public double RMaxKm { get; init; } = 12.0;

    /// <summary>Convergence tolerance on task duration (seconds). dht.md §4.1: ±5 s.</summary>
    public double ToleranceSeconds { get; init; } = 5.0;

    /// <summary>Maximum bisection iterations.</summary>
    public int MaxIterations { get; init; } = 100;
}

/// <summary>Per-glider optimiser result.</summary>
public class GliderPlan
{
    public required Glider Glider { get; init; }

    /// <summary>Optimised barrel radius (km) per course point, indexed by point.</summary>
    public required IReadOnlyList<double> RadiiKm { get; init; }

    /// <summary>Effective task distance flown (km).</summary>
    public double EffectiveDistanceKm { get; init; }

    /// <summary>Simulated task duration (hours).</summary>
    public double DurationHours { get; init; }

    /// <summary>True if the duration matched T_Ref within tolerance.</summary>
    public bool Converged { get; init; }

    /// <summary>Warnings (wind override, infeasible expansion, clamping).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>Whole-fleet optimiser result.</summary>
public class FleetPlan
{
    public required double ReferenceHandicap { get; init; }
    public required double ReferenceDistanceKm { get; init; }
    public required double ReferenceDurationHours { get; init; }
    public required IReadOnlyList<GliderPlan> Plans { get; init; }
}

/// <summary>
/// Sizes turnpoint barrels so that every glider's simulated task duration matches
/// the reference glider's (dht.md §3.4, §4.1). Centre-to-centre leg bearings are
/// held fixed; the saving at a turnpoint of radius R with deflection Δφ is
/// 2·R·sin(Δφ/2), split evenly across its two adjacent legs.
/// </summary>
public class BarrelOptimizer
{
    private readonly OptimizerOptions options;

    public BarrelOptimizer(OptimizerOptions? options = null)
    {
        this.options = options ?? new OptimizerOptions();
    }

    public FleetPlan Optimize(Course course, CourseGeometry geometry, Fleet fleet, double referenceHandicap)
    {
        double vRefAirspeed = Windicap.Airspeed(fleet.VRefCruKmh, referenceHandicap);

        // Reference glider flies R_min at every variable turnpoint.
        double[] refRadii = BuildRadii(course, options.RMinKm, geometry);
        double tRef = DurationHours(course, geometry, refRadii, vRefAirspeed,
            windSpeed: WindSpeed, windDirection: WindDirection);
        double dRef = EffectiveDistanceKm(course, geometry, refRadii);

        var plans = new List<GliderPlan>();
        foreach (Glider g in fleet.Gliders)
        {
            plans.Add(PlanGlider(course, geometry, fleet, g, referenceHandicap, tRef));
        }

        return new FleetPlan
        {
            ReferenceHandicap = referenceHandicap,
            ReferenceDistanceKm = dRef,
            ReferenceDurationHours = tRef,
            Plans = plans,
        };
    }

    /// <summary>Forecast wind speed (km/h). Set before <see cref="Optimize"/>.</summary>
    public double WindSpeed { get; set; }

    /// <summary>Forecast wind FROM direction (deg true).</summary>
    public double WindDirection { get; set; }

    private GliderPlan PlanGlider(Course course, CourseGeometry geometry, Fleet fleet,
        Glider glider, double referenceHandicap, double tRef)
    {
        var warnings = new List<string>();
        double airspeed = Windicap.Airspeed(fleet.VRefCruKmh, glider.Handicap);

        if (Windicap.IsWindOverride(WindSpeed, airspeed))
        {
            warnings.Add($"Wind {WindSpeed:F0} km/h ≥ 0.4×Va ({0.4 * airspeed:F0} km/h): " +
                "task should be downgraded (dht.md §5.2).");
        }

        double toleranceHours = options.ToleranceSeconds / 3600.0;

        // A single barrel radius R is applied to EVERY variable turnpoint. Larger R
        // saves more distance (and more at acute turns than obtuse, since the saving
        // is 2·R·sin(Δφ/2)) so the task takes less time — monotonic in R. Bisect R
        // in [R_min, R_max] to match the reference time.
        double tAtMin = DurationHours(course, geometry, BuildRadii(course, options.RMinKm, geometry),
            airspeed, WindSpeed, WindDirection);

        double radius;
        bool converged;
        if (tAtMin <= tRef + toleranceHours)
        {
            // Already at or below T_Ref at R_min (the reference glider itself, or a
            // higher-H glider): no expansion needed.
            radius = options.RMinKm;
            converged = Math.Abs(tAtMin - tRef) <= toleranceHours;
        }
        else
        {
            double tAtMax = DurationHours(course, geometry, BuildRadii(course, options.RMaxKm, geometry),
                airspeed, WindSpeed, WindDirection);
            if (tAtMax > tRef + toleranceHours)
            {
                // Even R_max at every turnpoint cannot reach the reference time.
                radius = options.RMaxKm;
                converged = false;
                warnings.Add($"Cannot reach reference time even at R_max={options.RMaxKm} km " +
                    "for all variable turnpoints; using maximum barrels.");
            }
            else
            {
                radius = BisectRadius(course, geometry, airspeed, tRef, toleranceHours);
                converged = true;
            }
        }

        double[] radii = BuildRadii(course, radius, geometry);
        double duration = DurationHours(course, geometry, radii, airspeed, WindSpeed, WindDirection);
        return new GliderPlan
        {
            Glider = glider,
            RadiiKm = radii,
            EffectiveDistanceKm = EffectiveDistanceKm(course, geometry, radii),
            DurationHours = duration,
            Converged = converged,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Bisects the single barrel radius R in [R_min, R_max] so the simulated task
    /// duration matches <paramref name="tRef"/>. Duration decreases monotonically
    /// with R (bigger barrels save more distance).
    /// </summary>
    private double BisectRadius(Course course, CourseGeometry geometry, double airspeed,
        double tRef, double toleranceHours)
    {
        double lo = options.RMinKm;
        double hi = options.RMaxKm;
        for (int i = 0; i < options.MaxIterations; i++)
        {
            double mid = 0.5 * (lo + hi);
            double[] radii = BuildRadii(course, mid, geometry);
            double t = DurationHours(course, geometry, radii, airspeed, WindSpeed, WindDirection);

            if (Math.Abs(t - tRef) <= toleranceHours)
            {
                return mid;
            }

            // Bigger R -> less time. If still too slow, grow R.
            if (t > tRef)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return 0.5 * (lo + hi);
    }

    // --- Radius allocation -------------------------------------------------

    /// <summary>Effective lower barrel bound for a point: its own RMin or the global default.</summary>
    private double RMin(CoursePoint p) => p.RMinKm ?? options.RMinKm;

    /// <summary>Effective upper barrel bound for a point: its own RMax or the global default.</summary>
    private double RMax(CoursePoint p) => p.RMaxKm ?? options.RMaxKm;

    /// <summary>
    /// Builds the per-point radius array with a single uniform barrel radius
    /// <paramref name="radiusKm"/> applied to every variable turnpoint (clamped to
    /// that point's [R_min, R_max]). Fixed interior zones (checkpoints) and
    /// start/finish keep their default radii. A uniform R means the distance saved
    /// varies with each turn's geometry — an acute turn saves more than an obtuse
    /// one — which is the intended DHT behaviour (dht.md §3.2, §4.1).
    /// </summary>
    private double[] BuildRadii(Course course, double radiusKm, CourseGeometry geometry)
    {
        IReadOnlyList<CoursePoint> pts = course.Points;
        var radii = new double[pts.Count];
        List<int> variable = VariableTurnpoints(course, geometry);

        for (int i = 0; i < pts.Count; i++)
        {
            CoursePoint p = pts[i];
            radii[i] = variable.Contains(i)
                ? Math.Clamp(radiusKm, RMin(p), RMax(p))
                : p.DefaultRadiusKm; // checkpoints and start/finish keep their zone
        }

        return radii;
    }

    /// <summary>
    /// Indices of interior turnpoints that are variable candidates and not too
    /// shallow to save distance (dht.md §4.1 geometric weighting).
    /// </summary>
    private static List<int> VariableTurnpoints(Course course, CourseGeometry geometry)
    {
        var result = new List<int>();
        IReadOnlyList<CoursePoint> pts = course.Points;
        for (int i = 1; i < pts.Count - 1; i++)
        {
            if (pts[i].IsVariableCandidate && !LegGeometry.IsTooShallow(geometry.Deflections[i]))
            {
                result.Add(i);
            }
        }

        return result;
    }

    // --- Distance & time given radii --------------------------------------

    private static double DeflectionRad(CourseGeometry geometry, int point)
        => geometry.Deflections[point] * Math.PI / 180.0;

    /// <summary>Effective distance per leg after barrel corner-cutting.</summary>
    private static double[] EffectiveLegDistances(Course course, CourseGeometry geometry, double[] radii)
    {
        IReadOnlyList<CoursePoint> pts = course.Points;
        var halfSaving = new double[pts.Count];
        for (int i = 1; i < pts.Count - 1; i++)
        {
            halfSaving[i] = radii[i] * Math.Sin(DeflectionRad(geometry, i) / 2.0);
        }

        var legs = new double[geometry.Legs.Count];
        for (int k = 0; k < legs.Length; k++)
        {
            legs[k] = geometry.Legs[k].DistanceKm - halfSaving[k] - halfSaving[k + 1];
            if (legs[k] < 0.0)
            {
                legs[k] = 0.0;
            }
        }

        return legs;
    }

    private static double EffectiveDistanceKm(Course course, CourseGeometry geometry, double[] radii)
        => EffectiveLegDistances(course, geometry, radii).Sum();

    private static double DurationHours(Course course, CourseGeometry geometry, double[] radii,
        double airspeed, double windSpeed, double windDirection)
    {
        double[] legs = EffectiveLegDistances(course, geometry, radii);
        double total = 0.0;
        for (int k = 0; k < legs.Length; k++)
        {
            double vg = Windicap.GroundSpeed(airspeed, geometry.Legs[k].BearingDegrees, windSpeed, windDirection);
            if (vg <= 0.0)
            {
                return double.PositiveInfinity;
            }

            total += legs[k] / vg;
        }

        return total;
    }
}
