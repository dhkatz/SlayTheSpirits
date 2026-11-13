using GameLogger = MegaCrit.Sts2.Core.Logging.Log;

namespace SpireKit.Logging;

/// <summary>Routes <see cref="IModLog"/> into the game's log (<c>%AppData%\SlayTheSpire2\logs</c>).</summary>
public sealed class GameLog(string modId) : IModLog
{
    private readonly string _prefix = $"[{modId}] ";

    public void Write(LogLevel level, string message, Exception? exception = null)
    {
        var text = exception is null ? _prefix + message : $"{_prefix}{message}\n{exception}";
        switch (level)
        {
            case LogLevel.Debug:
                GameLogger.Debug(text);
                break;
            case LogLevel.Info:
                GameLogger.Info(text);
                break;
            case LogLevel.Warn:
                GameLogger.Warn(text);
                break;
            default:
                GameLogger.Error(text);
                break;
        }
    }
}
