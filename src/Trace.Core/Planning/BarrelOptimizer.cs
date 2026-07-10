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
        double[] refRadii = BuildRadii(course, extraSavedKm: 0.0, geometry);
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

        // Maximum extra distance that can be saved with every variable turnpoint at R_max.
        double maxExtraSaved = MaxExtraSavedKm(course, geometry);

        // Duration at the two extremes (more saving => less time, monotonic).
        double[] radiiAtZero = BuildRadii(course, 0.0, geometry);
        double tAtZero = DurationHours(course, geometry, radiiAtZero, airspeed, WindSpeed, WindDirection);

        double extra;
        bool converged;
        if (tAtZero <= tRef + toleranceHours)
        {
            // Already at or below T_Ref with minimum barrels (e.g. the reference
            // glider itself, or a higher-H glider): no expansion needed.
            extra = 0.0;
            converged = Math.Abs(tAtZero - tRef) <= toleranceHours;
        }
        else
        {
            double[] radiiAtMax = BuildRadii(course, maxExtraSaved, geometry);
            double tAtMax = DurationHours(course, geometry, radiiAtMax, airspeed, WindSpeed, WindDirection);
            if (tAtMax > tRef + toleranceHours)
            {
                // Cannot expand barrels enough to reach T_Ref.
                extra = maxExtraSaved;
                converged = false;
                warnings.Add($"Cannot reach reference time even at R_max={options.RMaxKm} km " +
                    "for all variable turnpoints; using maximum barrels.");
            }
            else
            {
                extra = Bisect(course, geometry, airspeed, tRef, maxExtraSaved, toleranceHours);
                converged = true;
            }
        }

        double[] radii = BuildRadii(course, extra, geometry);
        if (AnyClamped(course, geometry, extra))
        {
            warnings.Add("One or more barrels hit a bound (R_min/R_max); " +
                "distance reallocated across remaining turnpoints.");
        }

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

    private double Bisect(Course course, CourseGeometry geometry, double airspeed,
        double tRef, double maxExtra, double toleranceHours)
    {
        double lo = 0.0;
        double hi = maxExtra;
        for (int i = 0; i < options.MaxIterations; i++)
        {
            double mid = 0.5 * (lo + hi);
            double[] radii = BuildRadii(course, mid, geometry);
            double t = DurationHours(course, geometry, radii, airspeed, WindSpeed, WindDirection);

            if (Math.Abs(t - tRef) <= toleranceHours)
            {
                return mid;
            }

            // More saving -> less time. If still too slow, save more.
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
    /// Builds the per-point radius array for a given total extra distance to save
    /// beyond the all-R_min baseline, distributed equally across variable
    /// turnpoints (dht.md §4.1) and clamped to each point's [R_min, R_max].
    /// </summary>
    private double[] BuildRadii(Course course, double extraSavedKm, CourseGeometry geometry)
    {
        IReadOnlyList<CoursePoint> pts = course.Points;
        var radii = new double[pts.Count];
        List<int> variable = VariableTurnpoints(course, geometry);

        double perTurnpoint = variable.Count > 0 ? extraSavedKm / variable.Count : 0.0;

        for (int i = 0; i < pts.Count; i++)
        {
            CoursePoint p = pts[i];
            if (variable.Contains(i))
            {
                double half = Math.Sin(DeflectionRad(geometry, i) / 2.0);
                // extra saving at this turnpoint = 2*(R - Rmin)*sin(Δφ/2) = perTurnpoint
                double r = RMin(p) + (half > 0 ? perTurnpoint / (2.0 * half) : 0.0);
                radii[i] = Math.Clamp(r, RMin(p), RMax(p));
            }
            else if (p.Type is CoursePointType.Turnpoint or CoursePointType.Checkpoint)
            {
                radii[i] = p.DefaultRadiusKm; // fixed interior zone
            }
            else
            {
                radii[i] = p.DefaultRadiusKm; // start/finish (not used for corner saving)
            }
        }

        return radii;
    }

    private double MaxExtraSavedKm(Course course, CourseGeometry geometry)
    {
        IReadOnlyList<CoursePoint> pts = course.Points;
        double sum = 0.0;
        foreach (int i in VariableTurnpoints(course, geometry))
        {
            double half = Math.Sin(DeflectionRad(geometry, i) / 2.0);
            sum += 2.0 * (RMax(pts[i]) - RMin(pts[i])) * half;
        }

        return sum;
    }

    private bool AnyClamped(Course course, CourseGeometry geometry, double extraSavedKm)
    {
        IReadOnlyList<CoursePoint> pts = course.Points;
        List<int> variable = VariableTurnpoints(course, geometry);
        if (variable.Count == 0)
        {
            return false;
        }

        double perTurnpoint = extraSavedKm / variable.Count;
        foreach (int i in variable)
        {
            double half = Math.Sin(DeflectionRad(geometry, i) / 2.0);
            double r = RMin(pts[i]) + (half > 0 ? perTurnpoint / (2.0 * half) : 0.0);
            if (r > RMax(pts[i]) + 1e-9 || r < RMin(pts[i]) - 1e-9)
            {
                return true;
            }
        }

        return false;
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
