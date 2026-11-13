using System.Reflection;
using SpireKit.Diagnostics;
using SpireKit.GameEvents;
using SpireKit.Logging;
using SpireKit.Patching;

namespace SpireKit.Modding;

/// <summary>Boots a <see cref="SpireMod"/>: services, SDK hooks, the mod's own setup, then patches.</summary>
public static class SpireBootstrap
{
    /// <summary>SDK version, logged at boot. Each mod embeds its own copy of SpireKit.</summary>
    public static string SdkVersion { get; } =
        typeof(SpireBootstrap).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";

    /// <summary>Call exactly once, from the mod's <c>[ModInitializer]</c> method.</summary>
    public static TMod Boot<TMod>()
        where TMod : SpireMod, new()
    {
        if (SpireRuntime.Current is not null)
        {
            throw new InvalidOperationException($"SpireKit already booted mod '{SpireRuntime.Current.Id}' in this assembly.");
        }

        var mod = new TMod();
        var context = new ModContext(mod);
        mod.Attach(context);
        SpireRuntime.Current = context;
        var log = context.Log;
        log.Info($"Booting (SpireKit {SdkVersion}).");

        try
        {
            context.Content.ValidateModelNames(typeof(TMod).Assembly);
            GameEventBridge.Install(context, typeof(TMod).Assembly);
            mod.OnInitialize(context);

            var runner = new PatchRunner(context.Harmony, context.Health, context.Id, log.WithScope("patch"));
            if (!runner.ApplyAll([typeof(SpireBootstrap).Assembly, typeof(TMod).Assembly]))
            {
                log.Error("A critical patch failed; unpatching everything. The mod is disabled for this session.");
                context.Harmony.UnpatchAll(context.Harmony.Id);
                context.Health.Report(context.Id, "mod", FeatureStatus.Disabled, "critical patch failed");
            }
        }
#pragma warning disable CA1031 // Never let a mod crash the game during startup.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            log.Error("Boot failed; the mod is disabled for this session.", ex);
            context.Health.Report(context.Id, "mod", FeatureStatus.Disabled, ex.Message);
            context.Harmony.UnpatchAll(context.Harmony.Id);
        }

        return mod;
    }

    /// <summary>Invoked by the SDK once the game's model database, localization and message types exist.</summary>
    internal static void NotifyGameReady()
    {
        var context = SpireRuntime.Current;
        if (context is null || !context.Health.IsEnabled(context.Id, "mod"))
        {
            return;
        }

        try
        {
            context.Loc.Attach();
            context.Session.AttachToRunManager();
            GameEventBridge.Instance?.AttachToRunManager();
            context.Mod.OnGameReady(context);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            context.Log.Error("OnGameReady failed.", ex);
            context.Health.Report(context.Id, "mod.gameReady", FeatureStatus.Disabled, ex.Message);
        }

        context.Log.Info(context.Health.Summarize(context.Id));
    }
}

/// <summary>
/// The booted mod for this assembly. Because SpireKit is merged into each mod's DLL, every mod has its
/// own copy of this static, so SDK patches always see their own mod.
/// </summary>
internal static class SpireRuntime
{
    public static ModContext? Current { get; set; }
}
