using Godot;
using MegaCrit.Sts2.Core.Saves;
using SpireKit.Logging;

namespace SpireKit.Storage;

/// <summary>Resolves a mod's private data folder inside the game's user data directory.</summary>
internal static class ModStorage
{
    /// <summary>
    /// <c>%AppData%\SlayTheSpire2\&lt;platform&gt;\&lt;userId&gt;\mods\&lt;modId&gt;</c> (per account), falling back to
    /// <c>%AppData%\SlayTheSpire2\mods\&lt;modId&gt;</c> if the platform account isn't available yet.
    /// </summary>
    public static IFileStore Create(string modId, IModLog log)
    {
        string userPath;
        try
        {
            userPath = UserDataPathProvider.GetAccountScopedBasePath($"mods/{modId}");
        }
#pragma warning disable CA1031 // Platform (Steam) may not be initialized; a shared folder is an acceptable fallback.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            log.Warn($"Account-scoped storage unavailable ({ex.Message}); using shared mod folder.");
            userPath = $"user://mods/{modId}";
        }

        var store = new DirectoryFileStore(ProjectSettings.GlobalizePath(userPath));
        log.Info($"Storage: {store.RootDirectory}");
        return store;
    }
}
