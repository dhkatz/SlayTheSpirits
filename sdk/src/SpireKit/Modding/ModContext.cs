using HarmonyLib;
using SpireKit.Content;
using SpireKit.Diagnostics;
using SpireKit.Events;
using SpireKit.Localization;
using SpireKit.Logging;
using SpireKit.Multiplayer;
using SpireKit.Runs;
using SpireKit.Storage;
using SpireKit.UI;
using SpireKit.UI.Settings;

namespace SpireKit.Modding;

/// <summary>Per-mod services. One instance per booted mod.</summary>
public sealed class ModContext
{
    private readonly Lazy<IFileStore> _storage;

    internal ModContext(SpireMod mod)
    {
        Mod = mod;
        Id = mod.Id;
        Log = new GameLog(mod.Id);
        Harmony = new Harmony($"spirekit.{mod.Id}");
        Events = new EventBus(Log.WithScope("events"));
        Health = new FeatureHealthRegistry();
        Session = new SessionTracker(Events, Log.WithScope("session"));
        Net = new NetHub(mod.Id, Session, Log.WithScope("net"));
        Content = new ContentRegistry(this);
        Toasts = new ToastOverlay(Log.WithScope("ui"));
        Loc = new LocInjector(Log.WithScope("loc"));
        RunData = new RunDataRegistry(Log.WithScope("rundata"));
        _storage = new Lazy<IFileStore>(() => ModStorage.Create(mod.Id, Log));
    }

    public SpireMod Mod { get; }

    public string Id { get; }

    public IModLog Log { get; }

    public Harmony Harmony { get; }

    /// <summary>Game events (see <c>SpireKit.GameEvents</c>) and anything the mod publishes itself.</summary>
    public IEventBus Events { get; }

    public FeatureHealthRegistry Health { get; }

    /// <summary>Lobby/run role tracking (offline, solo, host, client).</summary>
    public SessionTracker Session { get; }

    /// <summary>Named network channels, multiplexed over one message type.</summary>
    public NetHub Net { get; }

    /// <summary>Card/relic/potion registration and run-time gating.</summary>
    public ContentRegistry Content { get; }

    /// <summary>On-screen notifications.</summary>
    public ToastOverlay Toasts { get; }

    /// <summary>Runtime localization entries (re-applied on language change).</summary>
    public LocInjector Loc { get; }

    /// <summary>
    /// Per-run state that must survive save/quit and reach clients when a saved multiplayer run is loaded.
    /// Register slots in <c>OnInitialize</c>.
    /// </summary>
    public RunDataRegistry RunData { get; }

    /// <summary>Options this mod adds at rest sites, next to Rest and Smith.</summary>
    public RestSiteOptionRegistry RestSite { get; } = new();

    /// <summary>Tabs this mod adds to the game's Settings screen (main menu and pause menu).</summary>
    public SettingsTabRegistry SettingsTabs { get; } = new();

    /// <summary>The mod's private folder in the game's user data directory. Resolved on first use.</summary>
    public IFileStore Storage => _storage.Value;
}
