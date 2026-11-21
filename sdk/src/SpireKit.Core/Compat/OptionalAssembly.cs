using System.Reflection;
using SpireKit.Logging;

namespace SpireKit.Compat;

/// <summary>
/// Finds another mod's assembly without referencing it, for optional integrations (soft dependencies).
/// </summary>
/// <remarks>
/// Mods initialize in no fixed order, so the other mod may load before or after this one. Match it by assembly name,
/// not by its manifest id: loaders and forks often keep the assembly name while the id changes.
/// </remarks>
public static class OptionalAssembly
{
    /// <summary>The loaded assembly called <paramref name="name"/>, or null if it isn't loaded (yet).</summary>
    public static Assembly? Find(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);

    /// <summary>
    /// Calls <paramref name="onLoaded"/> once with the assembly called <paramref name="name"/>: straight away if it's
    /// already loaded, otherwise the moment it loads. Never called if it never loads.
    /// </summary>
    /// <remarks>
    /// A late load runs the callback inside whatever code is loading that assembly (usually the other mod's loader),
    /// so exceptions from it are caught and logged here rather than breaking that mod.
    /// </remarks>
    public static void WhenLoaded(string name, Action<Assembly> onLoaded, IModLog log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(onLoaded);
        ArgumentNullException.ThrowIfNull(log);

        var done = 0;
        AssemblyLoadEventHandler? handler = null;

        void Run(Assembly assembly)
        {
            if (Interlocked.Exchange(ref done, 1) != 0)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyLoad -= handler;
            try
            {
                onLoaded(assembly);
            }
#pragma warning disable CA1031 // Never let an integration break the mod that's loading.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                log.Error($"Integration with {name} failed.", ex);
            }
        }

        handler = (_, args) =>
        {
            if (args.LoadedAssembly.GetName().Name == name)
            {
                Run(args.LoadedAssembly);
            }
        };

        // Subscribe before looking, so a load in between isn't missed; Run makes sure it only happens once.
        AppDomain.CurrentDomain.AssemblyLoad += handler;
        if (Find(name) is { } loaded)
        {
            Run(loaded);
        }
    }
}
