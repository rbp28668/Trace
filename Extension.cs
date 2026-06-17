namespace Trace;

/// <summary>
/// Models an extension record in an IGC file.
/// </summary>
public class Extension
{
    /// <param name="start">1 based index of first char.</param>
    /// <param name="finish">1 based index of last char.</param>
    public Extension(string typeCode, int start, int finish)
    {
        TypeCode = typeCode;
        Start = start;
        Finish = finish;
    }

    public string TypeCode { get; }

    /// <summary>1 based index of first char.</summary>
    public int Start { get; }

    /// <summary>1 based index of last char.</summary>
    public int Finish { get; }

    public int Length => 1 + Finish - Start;
}
