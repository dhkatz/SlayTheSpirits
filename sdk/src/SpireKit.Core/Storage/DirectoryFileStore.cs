namespace SpireKit.Storage;

/// <summary>An <see cref="IFileStore"/> backed by a real directory on disk.</summary>
public sealed class DirectoryFileStore : IFileStore
{
    private readonly string _root;

    public DirectoryFileStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _root = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory => _root;

    public string? ReadText(string path)
    {
        var full = Resolve(path);
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    public void WriteText(string path, string content)
    {
        var full = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, full, overwrite: true);
    }

    public void Delete(string path)
    {
        var full = Resolve(path);
        if (File.Exists(full))
        {
            File.Delete(full);
        }
    }

    public IReadOnlyList<string> List(string directory)
    {
        var relative = FileStorePaths.Normalize(directory);
        var full = Resolve(relative);
        if (!Directory.Exists(full))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(full)
            .Where(static f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            .Select(f => relative + "/" + Path.GetFileName(f))
            .Order(StringComparer.Ordinal)];
    }

    private string Resolve(string path) => Path.Combine(_root, FileStorePaths.Normalize(path));
}
