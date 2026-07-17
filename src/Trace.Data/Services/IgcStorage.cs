namespace Trace.Data.Services;

/// <summary>Configurable root for stored IGC logs (bound from the Igc config section).</summary>
public class IgcStorageOptions
{
    public string RootPath { get; set; } = "App_Data/igc";
}

/// <summary>
/// Stores IGC logs on disk under a configurable root, in a
/// <c>{class}/{day}/</c> tree. The database keeps only metadata plus the path
/// relative to the root (see <c>data-app-plan.md</c> §9).
/// </summary>
public class IgcStorage
{
    private readonly string root;

    public IgcStorage(IgcStorageOptions options)
    {
        // Resolve to an absolute path once so callers are independent of the CWD.
        root = Path.GetFullPath(options.RootPath);
    }

    /// <summary>
    /// Writes the log under <c>{classId}/{dayId}/{fileName}</c> and returns the
    /// path relative to the root (stored on the entity). The file name is
    /// sanitised; collisions overwrite (a re-upload for the same entry).
    /// </summary>
    public async Task<string> SaveAsync(int classId, int dayId, string fileName, Stream content)
    {
        string safeName = SanitiseFileName(fileName);
        string relativeDir = Path.Combine($"class_{classId}", $"day_{dayId}");
        string absoluteDir = Path.Combine(root, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        string relativePath = Path.Combine(relativeDir, safeName);
        string absolutePath = Path.Combine(root, relativePath);

        await using (var fs = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
        {
            await content.CopyToAsync(fs);
        }

        // Normalise to forward slashes so the stored value is OS-independent.
        return relativePath.Replace('\\', '/');
    }

    /// <summary>Reads a stored log's full text given its root-relative path.</summary>
    public Task<string> ReadTextAsync(string relativePath) =>
        File.ReadAllTextAsync(FullPath(relativePath));

    /// <summary>Absolute path for a stored root-relative path.</summary>
    public string FullPath(string relativePath) =>
        Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public void Delete(string relativePath)
    {
        string full = FullPath(relativePath);
        if (File.Exists(full))
        {
            File.Delete(full);
        }
    }

    private static string SanitiseFileName(string fileName)
    {
        string name = Path.GetFileName(fileName);
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "flight.igc" : name;
    }
}
