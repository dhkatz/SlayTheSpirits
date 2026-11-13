namespace SpireKit.Storage;

/// <summary>
/// Minimal file abstraction rooted at a mod-owned directory. The game-bound layer roots this in the
/// game's user data folder; tests use <see cref="InMemoryFileStore"/>.
/// </summary>
/// <remarks>Paths are relative, forward-slash separated, and must not escape the root.</remarks>
public interface IFileStore
{
    /// <summary>Returns the file's text, or <see langword="null"/> if it does not exist.</summary>
    string? ReadText(string path);

    /// <summary>Writes the file atomically (write temp + replace), creating directories as needed.</summary>
    void WriteText(string path, string content);

    /// <summary>Deletes the file if it exists.</summary>
    void Delete(string path);

    /// <summary>Lists files directly inside <paramref name="directory"/> (relative paths).</summary>
    IReadOnlyList<string> List(string directory);
}

/// <summary>Validation shared by <see cref="IFileStore"/> implementations.</summary>
public static class FileStorePaths
{
    /// <summary>Normalizes to forward slashes and rejects rooted or escaping paths.</summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('\\', '/').Trim('/');
        if (Path.IsPathRooted(path) || normalized.Split('/').Any(static s => s is ".." or "."))
        {
            throw new ArgumentException($"Path '{path}' must be relative and must not contain '.' or '..' segments.", nameof(path));
        }

        return normalized;
    }
}
