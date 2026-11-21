using System.Reflection;
using System.Runtime.ExceptionServices;

namespace SpireKit.Compat;

/// <summary>A Hextech rune's tier in ARAM: Mayhem. Each act offers runes of one tier.</summary>
public enum HextechRarity
{
    Silver,
    Gold,
    Prismatic,
}

/// <summary>Where and when ARAM: Mayhem may offer a rune (its <c>PlayerRuneFlags</c>, by name).</summary>
[Flags]
public enum HextechRuneOptions
{
    None = 0,

    /// <summary>Off by default in ARAM's config menu.</summary>
    Disabled = 1 << 0,

    /// <summary>Never offered in act 1.</summary>
    FirstActExcluded = 1 << 1,

    /// <summary>Never offered in act 3 (ignored with an endless mode).</summary>
    ThirdActExcluded = 1 << 2,

    /// <summary>Never offered or listed in the config menu: the mod hands it out itself.</summary>
    SelectionExcluded = 1 << 3,

    /// <summary>In ARAM's attribute-conversion group (at most one of them).</summary>
    AttributeConversionExclusive = 1 << 4,

    /// <summary>
    /// The first rune choice of every act always offers one available rune with this flag (API version 2; dropped on
    /// older versions of ARAM).
    /// </summary>
    GuaranteedFirstPick = 1 << 5,
}

/// <summary>A character pool in ARAM: Mayhem. It sets the label and a higher weight; it doesn't limit who gets it.</summary>
public enum HextechCharacter
{
    Ironclad,
    Silent,
    Regent,
    Defect,
    Necrobinder,
    Fool,
}

/// <summary>How a rune joins ARAM: Mayhem's pools.</summary>
/// <param name="Type">The rune: a concrete <c>RelicModel</c> subclass whose <c>Rarity</c> is <c>Starter</c>.</param>
/// <param name="Rarity">Its Hextech tier.</param>
public sealed record HextechRune(Type Type, HextechRarity Rarity)
{
    public HextechRuneOptions Options { get; init; }

    /// <summary>The character it's for (label and weight only), or null for everyone.</summary>
    public HextechCharacter? Character { get; init; }

    /// <summary>Sort order inside its character pool.</summary>
    public int CharacterOrder { get; init; }

    /// <summary>
    /// ARAM's tag key (<c>HEXTECH_TAG.&lt;key&gt;</c> in the <c>relic_collection</c> table), or null for
    /// <c>COMPREHENSIVE</c>. Owning runes with a tag makes runes with that tag more likely.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// The source pill on the selection screen (<c>HEXTECH_POOL.&lt;key&gt;</c> in <c>relic_collection</c>), or null
    /// for ARAM's default (the character's name, or "Generic").
    /// </summary>
    public string? PoolLabel { get; init; }
}

/// <summary>
/// Calls ARAM: Mayhem's public soft-dependency API (<c>HextechRunes.HextechRunesInterop</c>) through reflection, so a
/// mod can add runes without referencing its DLL and still runs when it isn't installed.
/// </summary>
/// <remarks>
/// See ARAM's <c>INTEGRATION.md</c>. Its published signatures never change; new abilities come with a higher
/// <see cref="ApiVersion"/>. Registration only works while mods initialize (before the shared relic pool is first
/// used); later calls throw <see cref="InvalidOperationException"/>.
/// </remarks>
public sealed class HextechInterop
{
    /// <summary>The name of ARAM's real assembly (its loader is <c>HextechRunes.Loader</c>).</summary>
    public const string AssemblyName = "HextechRunes";

    public const string TypeName = "HextechRunes.HextechRunesInterop";

    // Exact: delegate variance would otherwise match a different filter type (Func<object, bool>).
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.ExactBinding;

    private readonly MethodInfo _registerPlayerRune;
    private readonly MethodInfo? _setPoolLabel;
    private readonly MethodInfo? _registerSectionTitle;

    private HextechInterop(int apiVersion, MethodInfo registerPlayerRune, MethodInfo? setPoolLabel, MethodInfo? registerSectionTitle)
    {
        ApiVersion = apiVersion;
        _registerPlayerRune = registerPlayerRune;
        _setPoolLabel = setPoolLabel;
        _registerSectionTitle = registerSectionTitle;
    }

    /// <summary>ARAM's interop version: 1 since 0.9.7, 2 adds <see cref="HextechRuneOptions.GuaranteedFirstPick"/>.</summary>
    public int ApiVersion { get; }

    /// <summary>
    /// Binds to ARAM in <paramref name="assembly"/>. <paramref name="predicateType"/> is the availability filter's
    /// type, <c>Func&lt;Player, bool&gt;</c> (a game type, so the game-bound layer supplies it).
    /// </summary>
    /// <exception cref="NotSupportedException">This version of ARAM has no usable interop API; the message says why.</exception>
    public static HextechInterop Bind(Assembly assembly, Type predicateType)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(predicateType);

        var interop = assembly.GetType(TypeName)
            ?? throw new NotSupportedException($"{TypeName} not found: this version of ARAM: Mayhem is too old (needs 0.9.7+).");
        if (interop.GetProperty("ApiVersion", PublicStatic)?.GetValue(null) is not int version || version < 1)
        {
            throw new NotSupportedException($"{TypeName}.ApiVersion is missing: this version of ARAM: Mayhem is too old (needs 0.9.7+).");
        }

        Type[] signature = [typeof(Type), typeof(string), typeof(string), typeof(string), typeof(int), typeof(string), typeof(string), predicateType];
        var register = interop.GetMethod("RegisterPlayerRune", PublicStatic, signature)
            ?? throw new NotSupportedException($"{TypeName}.RegisterPlayerRune has an unexpected signature (API version {version}).");

        return new HextechInterop(
            version,
            register,
            interop.GetMethod("SetPlayerRunePoolLabel", PublicStatic, [typeof(Type), typeof(string)]),
            interop.GetMethod("RegisterConfigSectionTitle", PublicStatic, [typeof(string), typeof(string)]));
    }

    /// <summary>
    /// Adds a player rune to ARAM's pools, and its pool label if it has one. Flags this version of ARAM doesn't know
    /// are left out.
    /// </summary>
    /// <param name="rune">The rune.</param>
    /// <param name="modId">The registering mod's id (the config menu groups runes by it).</param>
    /// <param name="isAvailable">
    /// Who may be offered it, as the <c>predicateType</c> given to <see cref="Bind"/>. It runs on every peer and must
    /// give the same answer on each: read only synced state.
    /// </param>
    /// <returns>The flags actually registered.</returns>
    /// <exception cref="ArgumentException">ARAM rejected a value (unknown tag isn't one: tags are free text).</exception>
    /// <exception cref="InvalidOperationException">Too late: the registration window has closed.</exception>
    public HextechRuneOptions RegisterPlayerRune(HextechRune rune, string modId, Delegate? isAvailable)
    {
        ArgumentNullException.ThrowIfNull(rune);
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);

        var flags = SupportedFlags(rune.Options, ApiVersion);
        Invoke(_registerPlayerRune,
        [
            rune.Type,
            rune.Rarity.ToString(),
            FormatFlags(flags),
            rune.Character?.ToString(),
            rune.CharacterOrder,
            rune.Tag,
            modId,
            isAvailable,
        ]);

        if (rune.PoolLabel is { } label && _setPoolLabel is not null)
        {
            Invoke(_setPoolLabel, [rune.Type, label]);
        }

        return flags;
    }

    /// <summary>
    /// The heading for this mod's runes in ARAM's config menu: a full key in the <c>relic_collection</c> table.
    /// </summary>
    /// <returns>False if this version of ARAM can't do it.</returns>
    public bool RegisterConfigSectionTitle(string modId, string titleKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(titleKey);
        if (_registerSectionTitle is null)
        {
            return false;
        }

        Invoke(_registerSectionTitle, [modId, titleKey]);
        return true;
    }

    /// <summary>Comma-separated flag names, as ARAM parses them; null for none.</summary>
    internal static string? FormatFlags(HextechRuneOptions flags) =>
        flags == HextechRuneOptions.None
            ? null
            : string.Join(",", Enum.GetValues<HextechRuneOptions>().Where(f => f != HextechRuneOptions.None && flags.HasFlag(f)));

    internal static HextechRuneOptions SupportedFlags(HextechRuneOptions flags, int apiVersion) =>
        apiVersion >= 2 ? flags : flags & ~HextechRuneOptions.GuaranteedFirstPick;

    /// <summary>Calls ARAM, rethrowing its own exception rather than reflection's wrapper.</summary>
    private static void Invoke(MethodInfo method, object?[] args)
    {
        try
        {
            method.Invoke(null, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}
