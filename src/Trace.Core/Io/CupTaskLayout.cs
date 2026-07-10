namespace Trace.Io;

/// <summary>
/// Helpers for reconciling a SeeYou task line with its observation zones.
///
/// By spec the task line is <c>description, takeoff, tp1, …, last tp, landing</c>
/// and observation zones are numbered from the Start: <c>ObsZone=0</c> is the
/// first point after the takeoff, so the takeoff and landing are not numbered.
/// The takeoff/landing may be a real airfield, a repeat of the start/finish, or a
/// <c>???</c> placeholder. <see cref="Trim"/> removes them so the remaining list
/// runs Start → turnpoints → Finish, after which the trimmed index of a point
/// equals its <c>ObsZone</c> number.
/// Spec: docs/CUP_file_format.md ("Format of Task Record").
/// </summary>
public static class CupTaskLayout
{
    /// <summary>
    /// Trims takeoff/landing entries from <paramref name="names"/> in place so it
    /// runs Start → … → Finish. When the task carries observation zones the zone
    /// count is authoritative — it equals the number of real task points, so two
    /// extra names mean a takeoff and a landing to drop. Otherwise a placeholder
    /// (<c>???</c>/empty) or a duplicate of the adjacent point is dropped
    /// heuristically. After trimming, the index of each remaining point equals its
    /// <c>ObsZone</c> number (0 = Start).
    /// </summary>
    public static void Trim(List<string> names, int zoneCount)
    {
        // Zones pin the real point count exactly: drop symmetric takeoff+landing.
        if (zoneCount > 0 && names.Count == zoneCount + 2)
        {
            names.RemoveAt(names.Count - 1);
            names.RemoveAt(0);
            return;
        }

        // zoneCount == names.Count means the list is already Start…Finish (a file
        // written without takeoff/landing). Any other shape — including a missing
        // takeoff or landing, or no zones at all — falls back to the heuristic.
        if (zoneCount > 0 && names.Count == zoneCount)
        {
            return;
        }

        if (names.Count >= 3 && IsDroppable(names, 0))
        {
            names.RemoveAt(0);
        }

        if (names.Count >= 3 && IsDroppable(names, names.Count - 1))
        {
            names.RemoveAt(names.Count - 1);
        }
    }

    /// <summary>
    /// True if the name at <paramref name="index"/> is a takeoff/landing that can
    /// be dropped: a <c>???</c>/empty placeholder, or a duplicate of its neighbour.
    /// </summary>
    private static bool IsDroppable(IReadOnlyList<string> names, int index)
    {
        string name = names[index];
        if (IsPlaceholder(name))
        {
            return true;
        }

        int neighbour = index == 0 ? 1 : index - 1;
        return name.Equals(names[neighbour], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True for a SeeYou takeoff/landing placeholder (empty or <c>???</c>).</summary>
    public static bool IsPlaceholder(string name)
    {
        string t = name.Trim();
        return t.Length == 0 || t.All(c => c == '?');
    }
}
