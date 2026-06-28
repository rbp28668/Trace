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
    public void ThrowsWhenNoTaskBlock()
    {
        const string noTask =
            "name,code,country,lat,lon,elev,style\n" +
            "\"A\",\"A\",UK,5111.250N,00101.917W,186m,1\n";
        Assert.Throws<InvalidDataException>(() => CourseReader.Read(new StringReader(noTask)));
    }
}
