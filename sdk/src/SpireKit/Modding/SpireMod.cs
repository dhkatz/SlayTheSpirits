namespace SpireKit.Modding;

/// <summary>
/// Base class for a SpireKit mod. Subclass it, then boot it from the game's entry point:
/// <code>
/// [ModInitializer(nameof(Init))]
/// public static class Entry
/// {
///     public static void Init() => SpireBootstrap.Boot&lt;MyMod&gt;();
/// }
/// </code>
/// </summary>
public abstract class SpireMod
{
    private ModContext? _context;

    /// <summary>Must equal the manifest <c>id</c> (and the DLL/PCK file stem).</summary>
    public abstract string Id { get; }

    /// <summary>
    /// Every model class (card, relic, power, listener…) must start with this prefix. The game derives
    /// model ids from the class name alone, so unprefixed names collide with base or other mods' content.
    /// </summary>
    public abstract string ModelPrefix { get; }

    /// <summary>Available from <see cref="OnInitialize"/> onward.</summary>
    public ModContext Context => _context ?? throw new InvalidOperationException("The mod has not been booted yet.");

    /// <summary>
    /// Runs from the game's mod initializer, before the model database exists. Register pool content,
    /// net channels and event subscriptions here. Do not touch <c>ModelDb</c> or <c>LocManager</c> yet.
    /// </summary>
    protected internal virtual void OnInitialize(ModContext context)
    {
    }

    /// <summary>Runs once models, localization and network message types are initialized (before the main menu).</summary>
    protected internal virtual void OnGameReady(ModContext context)
    {
    }

    internal void Attach(ModContext context) => _context = context;
}
