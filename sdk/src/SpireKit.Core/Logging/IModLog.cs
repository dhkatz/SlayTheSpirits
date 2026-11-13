namespace SpireKit.Logging;

/// <summary>Severity of a log entry.</summary>
public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

/// <summary>
/// Minimal logging abstraction. The game-bound layer adapts this to the game's own logger;
/// tests use <see cref="NullModLog"/> or a recording implementation.
/// </summary>
public interface IModLog
{
    void Write(LogLevel level, string message, Exception? exception = null);
}

/// <summary>Convenience helpers for <see cref="IModLog"/>.</summary>
public static class ModLogExtensions
{
    public static void Debug(this IModLog log, string message) => log.Write(LogLevel.Debug, message);

    public static void Info(this IModLog log, string message) => log.Write(LogLevel.Info, message);

    public static void Warn(this IModLog log, string message, Exception? exception = null) =>
        log.Write(LogLevel.Warn, message, exception);

    public static void Error(this IModLog log, string message, Exception? exception = null) =>
        log.Write(LogLevel.Error, message, exception);

    /// <summary>Returns a log that prefixes every message with <c>[scope]</c>.</summary>
    public static IModLog WithScope(this IModLog log, string scope) => new ScopedModLog(log, scope);

    private sealed class ScopedModLog(IModLog inner, string scope) : IModLog
    {
        public void Write(LogLevel level, string message, Exception? exception = null) =>
            inner.Write(level, $"[{scope}] {message}", exception);
    }
}

/// <summary>A log that discards everything.</summary>
public sealed class NullModLog : IModLog
{
    public static readonly NullModLog Instance = new();

    private NullModLog()
    {
    }

    public void Write(LogLevel level, string message, Exception? exception = null)
    {
    }
}
