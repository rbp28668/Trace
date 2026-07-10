using System.IO;
using Trace.Io;
using Trace.Model;
using Xunit;

namespace Trace.Tests;

public class CourseReaderTests
{
    private const string TriangleCup =
        "name,code,country,lat,lon,elev,style,rwdir,rwlen,rwwidth,freq,desc,userdata,pics\n" +
        "\"Lasham\",\"LAS\",UK,5111.250N,00101.917W,186m,4,,,,,\"Start\",,\n" +
        "\"Membury\",\"MEM\",UK,5128.000N,00131.000W,230m,1,,,,,\"TP1\",,\n" +
        "\"Didcot\",\"DID\",UK,5136.500N,00115.300W,90m,1,,,,,\"TP2\",,\n" +
        "-----Related Tasks-----\n" +
        "\"Tri\",\"Lasham\",\"Membury\",\"Didcot\",\"Lasham\"\n" +
        "ObsZone=0,Style=2,R1=5000m,A1=90,Line=1\n";

    [Fact]
    public void BuildsCourseWithStartTurnpointsFinish()
    {
        Course course = CourseReader.Read(new StringReader(TriangleCup));

        Assert.Equal(4, course.Points.Count);
        Assert.Equal(CoursePointType.Start, course.Points[0].Type);
        Assert.Equal(CoursePointType.Turnpoint, course.Points[1].Type);
        Assert.Equal(CoursePointType.Turnpoint, course.Points[2].Type);
        Assert.Equal(CoursePointType.Finish, course.Points[3].Type);
    }

    [Fact]
    public void ResolvesDuplicateStartFinishWaypoint()
    {
        Course course = CourseReader.Read(new StringReader(TriangleCup));
        // Lasham appears as both start and finish at identical coordinates.
        Assert.Equal(course.Points[0].Latitude, course.Points[3].Latitude, 6);
        Assert.Equal(course.Points[0].Longitude, course.Points[3].Longitude, 6);
    }

    [Fact]
    public void ReadsPerTurnpointBoundsFromUserData()
    {
        // TP1 carries explicit barrel bounds in userdata; its 0.5 km zone would
        // otherwise be classed as a fixed checkpoint, but the bounds force it
        // variable and supply the per-turnpoint RMin/RMax.
        const string cup =
            "name,code,country,lat,lon,elev,style,rwdir,rwlen,rwwidth,freq,desc,userdata,pics\n" +
            "\"Lasham\",\"LAS\",UK,5111.250N,00101.917W,186m,4,,,,,\"Start\",,\n" +
            "\"Membury\",\"MEM\",UK,5128.000N,00131.000W,230m,1,,,,,\"TP1\",\"{\"\"rmin\"\":3,\"\"rmax\"\":9}\",\n" +
            "\"Didcot\",\"DID\",UK,5136.500N,00115.300W,90m,1,,,,,\"TP2\",,\n" +
            "\"Lasham\",\"LAS\",UK,5111.250N,00101.917W,186m,4,,,,,\"Finish\",,\n" +
            "-----Related Tasks-----\n" +
            "\"Bounds\",\"Lasham\",\"Membury\",\"Didcot\",\"Lasham\"\n" +
            "ObsZone=0,Style=2,R1=5000m,A1=90,Line=1\n" +
            "ObsZone=1,Style=1,R1=500m,A1=180\n" +
            "ObsZone=2,Style=1,R1=500m,A1=180\n" +
            "ObsZone=3,Style=1,R1=3000m,A1=180\n";

        Course course = CourseReader.Read(new StringReader(cup));

        CoursePoint tp1 = course.Points[1];
        Assert.Equal(CoursePointType.Turnpoint, tp1.Type); // bounds override the <1 km checkpoint rule
        Assert.Equal(3.0, tp1.RMinKm);
        Assert.Equal(9.0, tp1.RMaxKm);

        // TP2 has no bounds and a 0.5 km zone: a fixed checkpoint.
        CoursePoint tp2 = course.Points[2];
        Assert.Equal(CoursePointType.Checkpoint, tp2.Type);
        Assert.Null(tp2.RMinKm);
    }

    [Fact]
    public void ThrowsWhenNoTaskBlock()
    {
        const string noTask =
            "name,code,country,lat,lon,elev,style\n" +
            "\"A\",\"A\",UK,5111.250N,00101.917W,186m,1\n";
        Assert.Throws<InvalidDataException>(() => CourseReader.Read(new StringReader(noTask)));
    }
}
