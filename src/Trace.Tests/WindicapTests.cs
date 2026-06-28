using Trace.Wind;
using Xunit;

namespace Trace.Tests;

public class WindicapTests
{
    [Fact]
    public void AirspeedScalesWithHandicap()
    {
        Assert.Equal(130.0, Windicap.Airspeed(130.0, 100.0), 6);
        Assert.Equal(120.9, Windicap.Airspeed(130.0, 93.0), 6);   // ASW 19
        Assert.Equal(144.95, Windicap.Airspeed(130.0, 111.5), 6); // JS3-18m
    }

    [Fact]
    public void DirectHeadwindSubtractsFromAirspeed()
    {
        // Leg due north (0°), wind from the north (ω=0): pure headwind γ=0.
        double vg = Windicap.GroundSpeed(airspeed: 100.0, legBearing: 0.0, windSpeed: 30.0, windDirection: 0.0);
        Assert.Equal(70.0, vg, 6);
    }

    [Fact]
    public void DirectTailwindAddsToAirspeed()
    {
        // Leg due north, wind from the south (ω=180): pure tailwind.
        double vg = Windicap.GroundSpeed(airspeed: 100.0, legBearing: 0.0, windSpeed: 30.0, windDirection: 180.0);
        Assert.Equal(130.0, vg, 6);
    }

    [Fact]
    public void PureCrosswindReducesGroundSpeedBelowAirspeed()
    {
        // Leg due north, wind from the east (ω=90): pure crosswind, no head/tail.
        double vg = Windicap.GroundSpeed(airspeed: 100.0, legBearing: 0.0, windSpeed: 30.0, windDirection: 90.0);
        Assert.Equal(Math.Sqrt(100.0 * 100.0 - 30.0 * 30.0), vg, 6); // ≈ 95.39, crab penalty
        Assert.True(vg < 100.0);
    }

    [Fact]
    public void CrosswindExceedingAirspeedThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Windicap.GroundSpeed(airspeed: 20.0, legBearing: 0.0, windSpeed: 30.0, windDirection: 90.0));
    }

    [Fact]
    public void WindOverrideTriggersAtFortyPercent()
    {
        Assert.True(Windicap.IsWindOverride(windSpeed: 40.0, airspeed: 100.0));
        Assert.True(Windicap.IsWindOverride(windSpeed: 41.0, airspeed: 100.0));
        Assert.False(Windicap.IsWindOverride(windSpeed: 39.0, airspeed: 100.0));
    }
}

public class TaskMetricsTests
{
    [Fact]
    public void TotalDistanceSumsLegs()
    {
        var legs = new[] { new Leg(50.0, 0.0), new Leg(30.0, 90.0), new Leg(20.0, 180.0) };
        Assert.Equal(100.0, TaskMetrics.TotalDistanceKm(legs), 6);
    }

    [Fact]
    public void DurationInNilWindIsDistanceOverAirspeed()
    {
        var legs = new[] { new Leg(100.0, 0.0), new Leg(100.0, 90.0) };
        double t = TaskMetrics.Duration(legs, airspeed: 100.0, windSpeed: 0.0, windDirection: 0.0);
        Assert.Equal(2.0, t, 6); // 200 km / 100 km/h
    }

    [Fact]
    public void HeadwindAndTailwindLegsAreAsymmetric()
    {
        // Out-and-return into a headwind takes longer than nil wind overall.
        var legs = new[] { new Leg(100.0, 0.0), new Leg(100.0, 180.0) };
        double nilWind = TaskMetrics.Duration(legs, 100.0, 0.0, 0.0);
        double withWind = TaskMetrics.Duration(legs, 100.0, windSpeed: 30.0, windDirection: 0.0);
        Assert.True(withWind > nilWind);
    }
}
