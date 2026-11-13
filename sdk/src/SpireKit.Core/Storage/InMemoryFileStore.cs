namespace SpireKit.Storage;

/// <summary>An <see cref="IFileStore"/> held in memory. Intended for tests and previews.</summary>
public sealed class InMemoryFileStore : IFileStore
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Files => _files;

    public string? ReadText(string path) => _files.GetValueOrDefault(FileStorePaths.Normalize(path));

    public void WriteText(string path, string content) => _files[FileStorePaths.Normalize(path)] = content;

    public void Delete(string path) => _files.Remove(FileStorePaths.Normalize(path));

    public IReadOnlyList<string> List(string directory)
    {
        var prefix = FileStorePaths.Normalize(directory) + "/";
        return [.. _files.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && k.IndexOf('/', prefix.Length) < 0)
            .Order(StringComparer.Ordinal)];
    }
}
