using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SpireKit.Diagnostics;
using SpireKit.Logging;
using SpireKit.Modding;

namespace SpireKit.Compat;

/// <summary>
/// Adds this mod's runes to ARAM: Mayhem (the <c>HextechRunes</c> mod) when it's installed, through its public
/// soft-dependency API. Without it, nothing happens: the runes stay unused models, in no pool.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="AddRune{TRune}"/> from <see cref="SpireMod.OnInitialize"/>. Mods initialize in no fixed order, so
/// runes are handed to ARAM straight away if it's already loaded, or the moment it loads, which is still inside its
/// registration window. Don't declare a manifest dependency on it.
/// </para>
/// <para>
/// A rune is a <see cref="RelicModel"/> with <c>Rarity =&gt; RelicRarity.Starter</c> (ARAM refuses anything else, so no
/// other relic source can hand it out). Its text lives in the <c>relics</c> table like any relic's; labels and tags
/// read the <c>relic_collection</c> table (<c>HEXTECH_POOL.&lt;key&gt;</c>, <c>HEXTECH_TAG.&lt;key&gt;</c>), which a mod
/// fills with its own <c>localization/&lt;lang&gt;/relic_collection.json</c>.
/// </para>
/// </remarks>
public sealed class HextechRuneRegistry
{
    private const string Feature = "compat.hextechRunes";

    private readonly ModContext _context;
    private readonly IModLog _log;
    private readonly List<(HextechRune Rune, Func<Player, bool>? IsAvailable)> _pending = [];
    private string? _sectionTitle;
    private HextechInterop? _interop;
    private bool _watching;
    private bool _failed;
    private int _registered;

    internal HextechRuneRegistry(ModContext context)
    {
        _context = context;
        _log = context.Log.WithScope("hextech");
    }

    /// <summary>Whether ARAM: Mayhem is loaded in this game, whether or not registration worked.</summary>
    public static bool IsInstalled => OptionalAssembly.Find(HextechInterop.AssemblyName) is not null;

    /// <summary>Whether ARAM: Mayhem took this mod's runes.</summary>
    public bool IsConnected => _interop is not null && !_failed;

    /// <summary>
    /// Adds a player rune to ARAM: Mayhem's pools, if it's installed.
    /// </summary>
    /// <param name="rarity">Its Hextech tier: the act's tier decides which runes are offered.</param>
    /// <param name="tag">ARAM's tag key (theirs, e.g. <c>SURVIVAL</c>, or your own); null for <c>COMPREHENSIVE</c>.</param>
    /// <param name="character">
    /// A character pool: labels the rune with the character and weights it up for them. It doesn't stop anyone else
    /// getting it, so <paramref name="isAvailable"/> should check the character too.
    /// </param>
    /// <param name="options">Act exclusions and the like.</param>
    /// <param name="poolLabel">A source label key (<c>HEXTECH_POOL.&lt;key&gt;</c>), or null for ARAM's default.</param>
    /// <param name="isAvailable">
    /// Whether a player may be offered it. Runs on every peer and must agree on each: read only synced state (the
    /// player, their run, host-authoritative settings).
    /// </param>
    public HextechRuneRegistry AddRune<TRune>(
        HextechRarity rarity,
        string? tag = null,
        HextechCharacter? character = null,
        HextechRuneOptions options = HextechRuneOptions.None,
        string? poolLabel = null,
        Func<Player, bool>? isAvailable = null)
        where TRune : RelicModel
    {
        var rune = new HextechRune(typeof(TRune), rarity)
        {
            Options = options,
            Character = character,
            CharacterOrder = _pending.Count,
            Tag = tag,
            PoolLabel = poolLabel,
        };
        _pending.Add((rune, isAvailable));
        if (_interop is not null)
        {
            Register(rune, isAvailable);
        }
        else
        {
            Watch();
        }

        return this;
    }

    /// <summary>The heading over this mod's runes in ARAM's config menu: a key in the <c>relic_collection</c> table.</summary>
    public HextechRuneRegistry SetSectionTitle(string titleKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titleKey);
        _sectionTitle = titleKey;
        if (_interop is not null)
        {
            RegisterSectionTitle();
        }

        return this;
    }

    private void Watch()
    {
        if (!_watching)
        {
            _watching = true;
            OptionalAssembly.WhenLoaded(HextechInterop.AssemblyName, Connect, _log);
        }
    }

    private void Connect(Assembly assembly)
    {
        try
        {
            _interop = HextechInterop.Bind(assembly, typeof(Func<Player, bool>));
        }
        catch (NotSupportedException ex)
        {
            Fail(ex.Message);
            return;
        }

        _log.Info($"ARAM: Mayhem found (interop API {_interop.ApiVersion}); adding {_pending.Count} runes.");
        foreach (var (rune, isAvailable) in _pending)
        {
            Register(rune, isAvailable);
        }

        if (_sectionTitle is not null)
        {
            RegisterSectionTitle();
        }

        if (!_failed)
        {
            _context.Health.Report(_context.Id, Feature, FeatureStatus.Healthy, $"{_registered} runes");
        }
    }

    private void Register(HextechRune rune, Func<Player, bool>? isAvailable)
    {
        try
        {
            var used = _interop!.RegisterPlayerRune(rune, _context.Id, isAvailable);
            _registered++;
            if (used != rune.Options)
            {
                _log.Warn($"{rune.Type.Name}: this version of ARAM: Mayhem doesn't support {rune.Options & ~used}; registered without it.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Fail($"{rune.Type.Name}: {ex.Message}");
        }
    }

    private void RegisterSectionTitle()
    {
        try
        {
            _interop!.RegisterConfigSectionTitle(_context.Id, _sectionTitle!);
        }
        catch (ArgumentException ex)
        {
            _log.Warn($"Config section title rejected: {ex.Message}");
        }
    }

    private void Fail(string reason)
    {
        _failed = true;
        _log.Warn($"ARAM: Mayhem integration: {reason}");
        _context.Health.Report(_context.Id, Feature, FeatureStatus.Degraded, reason);
    }
}
