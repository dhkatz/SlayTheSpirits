using SpireKit.Logging;

namespace SpireKit.Tests;

internal sealed class RecordingLog : IModLog
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public void Write(LogLevel level, string message, Exception? exception = null) => Entries.Add((level, message, exception));
}
