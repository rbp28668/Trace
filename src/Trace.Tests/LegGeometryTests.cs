using Trace.Geometry;
using Xunit;

namespace Trace.Tests;

public class LegGeometryTests
{
    [Theory]
    [InlineData(0.0, 90.0, 90.0)]    // 90° right turn
    [InlineData(350.0, 10.0, 20.0)]  // wraps across north
    [InlineData(10.0, 350.0, 20.0)]  // wraps the other way
    [InlineData(0.0, 180.0, 180.0)]  // full reversal
    [InlineData(45.0, 45.0, 0.0)]    // straight on
    public void DeflectionAngleHandlesWrap(double inbound, double outbound, double expected)
    {
        Assert.Equal(expected, LegGeometry.DeflectionAngle(inbound, outbound), 6);
    }

    [Fact]
    public void VertexAngleIsSupplementOfDeflection()
    {
        Assert.Equal(90.0, LegGeometry.VertexAngle(90.0), 6);
        Assert.Equal(180.0, LegGeometry.VertexAngle(0.0), 6);
    }

    [Fact]
    public void DistanceSavedForRightAngleTurn()
    {
        // Δφ = 90°, R = 1 km -> 2·1·sin(45°) = √2.
        double saved = LegGeometry.DistanceSaved(1.0, 90.0);
        Assert.Equal(Math.Sqrt(2.0), saved, 6);
    }

    [Theory]
    [InlineData(0.5, 90.0)]
    [InlineData(3.2, 60.0)]
    [InlineData(1.0, 120.0)]
    public void RadiusAndDistanceSavedRoundTrip(double radius, double deflection)
    {
        double saved = LegGeometry.DistanceSaved(radius, deflection);
        double recovered = LegGeometry.RadiusForDistanceSaved(saved, deflection);
        Assert.Equal(radius, recovered, 6);
    }

    [Fact]
    public void ShallowTurnIsRejected()
    {
        // Vertex 150° == deflection 30° is the boundary; just shallower must reject.
        Assert.True(LegGeometry.IsTooShallow(29.0));   // vertex 151°
        Assert.False(LegGeometry.IsTooShallow(31.0));  // vertex 149°
        Assert.Throws<ArgumentException>(
            () => LegGeometry.RadiusForDistanceSaved(0.5, 29.0));
    }

    [Fact]
    public void RadiusDivergesAsTurnStraightens()
    {
        double shallow = LegGeometry.RadiusForDistanceSaved(0.5, 31.0);
        double sharp = LegGeometry.RadiusForDistanceSaved(0.5, 90.0);
        // A shallower usable turn needs a much larger radius for the same saving.
        Assert.True(shallow > sharp);
    }
}
