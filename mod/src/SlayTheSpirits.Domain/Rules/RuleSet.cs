using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Rules;

/// <summary>Who receives a penalty.</summary>
public enum TargetKind
{
    /// <summary>The player the event is about (who died, who got hit, the card's chosen player).</summary>
    Subject,
    Everyone,

    /// <summary>Everyone except the subject.</summary>
    Others,
    RandomPlayer,

    /// <summary>A random player other than the subject.</summary>
    RandomOther,
}

/// <summary>How often rest sites offer their tabs (<see cref="Engine.RestSiteTabs"/>).</summary>
public enum RestSiteTabLimit
{
    /// <summary>Rest sites only have the game's own options.</summary>
    Off,

    /// <summary>Each player can take each tab at one rest site per act.</summary>
    OncePerAct,

    /// <summary>At every rest site.</summary>
    Unlimited,
}

/// <summary>Which shops on the map are the Tavern instead.</summary>
public enum TavernLimit
{
    /// <summary>Every shop is the game's merchant.</summary>
    Off,

    /// <summary>One shop per act, picked from the run's seed and marked on the map.</summary>
    OncePerAct,

    /// <summary>Every shop on the map.</summary>
    EveryShop,
}

/// <summary>What happens when a trigger fires.</summary>
/// <param name="Enabled">Off rules are kept (so the UI remembers their values) but never fire.</param>
/// <param name="Penalty">What to drink.</param>
/// <param name="Target">Who drinks.</param>
/// <param name="Threshold">For threshold triggers: fire only when the occurrence's magnitude is at least this.</param>
public sealed record TriggerRule(bool Enabled, Penalty Penalty, TargetKind Target, int? Threshold = null)
{
    public static TriggerRule On(PenaltyKind kind, TargetKind target, int count = 1, int? threshold = null) =>
        new(true, new Penalty(kind, count), target, threshold);

    public static TriggerRule Off(PenaltyKind kind, TargetKind target, int count = 1, int? threshold = null) =>
        new(false, new Penalty(kind, count), target, threshold);
}

/// <summary>Display names for penalty kinds, so groups can relabel ("Shot" → "10 push-ups").</summary>
/// <param name="Singular">E.g. "Shot".</param>
/// <param name="Plural">E.g. "Shots".</param>
public sealed record PenaltyLabel(string Singular, string Plural);

/// <summary>
/// The complete, shareable configuration of a drinking game. This is the document the host owns,
/// broadcasts to clients, locks for the duration of a run, and exports as a share code.
/// </summary>
public sealed record RuleSet
{
    /// <summary>Display name, e.g. "Standard" or "Dave's House Rules".</summary>
    public string Name { get; init; } = "Custom";

    /// <summary>Master switch. When off, no penalties fire (cards still exist if enabled).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Rules keyed by <see cref="Triggers.TriggerId"/> value.</summary>
    public IReadOnlyDictionary<string, TriggerRule> Triggers { get; init; } = new Dictionary<string, TriggerRule>();

    /// <summary>Per-monster rules for <see cref="Triggers.BuiltInTriggers.EnemyEncountered"/>, keyed by monster model id.</summary>
    public IReadOnlyDictionary<string, TriggerRule> Enemies { get; init; } = new Dictionary<string, TriggerRule>();

    /// <summary>
    /// Per-player, per-act caps. When a player would exceed the cap for a kind, the penalty is downgraded
    /// to the next lighter kind (and that cap is checked in turn). Missing kind = unlimited.
    /// </summary>
    public IReadOnlyDictionary<PenaltyKind, int> CapsPerAct { get; init; } = new Dictionary<PenaltyKind, int>();

    /// <summary>
    /// Tipsy gained per unit of each penalty kind (a "2 Shots" penalty gives twice the Shot value).
    /// Tipsy is the run-long drunkenness counter shown as a buff and used by Tavern cards.
    /// Missing kinds use <see cref="TipsyWeights.Defaults"/>.
    /// </summary>
    public IReadOnlyDictionary<PenaltyKind, int> Tipsy { get; init; } = TipsyWeights.Defaults;

    /// <summary>
    /// When a card makes another player drink, that player must confirm it on a card-selection screen
    /// before the drink card is played. Off (default) = drink cards play straight away and the per-combat
    /// bar tab under each player is the reminder.
    /// </summary>
    public bool ConfirmDrinks { get; init; }

    /// <summary>
    /// Rest sites offer two tabs next to Rest and Smith: Tavern cards for a minor Tavern curse, a Tavern relic for a
    /// major one (<see cref="Engine.RestSiteTabs"/>). Stored as <c>restSiteDrinks</c>: they used to cost a Chug and a
    /// Shot, and the setting still means the same thing.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("restSiteDrinks")]
    public RestSiteTabLimit RestSiteTabs { get; init; } = RestSiteTabLimit.OncePerAct;

    /// <summary>
    /// Shops that become the Tavern: a bar whose menu is paid for in gold and drinks (see <see cref="Engine.TavernMenu"/>).
    /// Multiplayer runs only, like the rest of the Tavern content.
    /// </summary>
    public TavernLimit Taverns { get; init; } = TavernLimit.OncePerAct;

    /// <summary>Optional relabels; missing kinds use the default English names.</summary>
    public IReadOnlyDictionary<PenaltyKind, PenaltyLabel> Labels { get; init; } = new Dictionary<PenaltyKind, PenaltyLabel>();

    /// <summary>Content toggles. Affects game simulation (card pools), so it must match on every peer.</summary>
    public ContentToggles Content { get; init; } = new();
}

/// <summary>Which mod content enters the game's pools.</summary>
public sealed record ContentToggles
{
    /// <summary>Colorless multiplayer-only Tavern cards.</summary>
    public bool TavernCards { get; init; } = true;

    /// <summary>Character-specific Tavern cards.</summary>
    public bool CharacterCards { get; init; } = true;

    /// <summary>Tavern potions (Light Beer, Tallboy, Firewater) among random potions.</summary>
    public bool TavernPotions { get; init; } = true;

    /// <summary>Individual cards switched off, by card id.</summary>
    public IReadOnlyList<string> DisabledCards { get; init; } = [];
}
