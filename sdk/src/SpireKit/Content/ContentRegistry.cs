using System.Reflection;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using SpireKit.Diagnostics;
using SpireKit.Logging;
using SpireKit.Modding;

namespace SpireKit.Content;

/// <summary>
/// Registers a mod's cards, relics and potions with the game's pools, and decides per run whether
/// mod-owned cards may appear (e.g. host settings switching a card set off).
/// </summary>
/// <remarks>
/// Pool membership is fixed at startup by the game, so run-time toggles are implemented as a filter on
/// <c>CardPoolModel.GetUnlockedCards</c>, the single source every reward, shop and generator draws from.
/// The filter must give the same answer on every peer (read only host-synced state), or rewards desync.
/// </remarks>
public sealed class ContentRegistry
{
    private const string NamesFeature = "content.names";

    private readonly ModContext _context;
    private readonly HashSet<Type> _registered = [];
    private Func<CardModel, bool>? _cardFilter;

    internal ContentRegistry(ModContext context) => _context = context;

    /// <summary>Types registered through this registry.</summary>
    public IReadOnlyCollection<Type> Registered => _registered;

    /// <summary>Adds a card to a pool. Call from <see cref="SpireMod.OnInitialize"/>.</summary>
    public ContentRegistry AddCard<TPool, TCard>()
        where TPool : CardPoolModel
        where TCard : CardModel => Add<TPool, TCard>();

    /// <summary>Adds a relic to a pool. Call from <see cref="SpireMod.OnInitialize"/>.</summary>
    public ContentRegistry AddRelic<TPool, TRelic>()
        where TPool : RelicPoolModel
        where TRelic : RelicModel => Add<TPool, TRelic>();

    /// <summary>Adds a potion to a pool. Call from <see cref="SpireMod.OnInitialize"/>.</summary>
    public ContentRegistry AddPotion<TPool, TPotion>()
        where TPool : PotionPoolModel
        where TPotion : PotionModel => Add<TPool, TPotion>();

    /// <summary>
    /// Sets the run-time availability rule for this mod's cards. Only consulted during runs; the card
    /// library always shows everything. Must be deterministic across peers.
    /// </summary>
    public void SetCardFilter(Func<CardModel, bool> isAllowed) => _cardFilter = isAllowed;

    internal bool HasCardFilter => _cardFilter is not null;

    internal bool IsCardAllowed(CardModel card)
    {
        if (_cardFilter is null || card.GetType().Assembly != _context.Mod.GetType().Assembly)
        {
            return true;
        }

        try
        {
            return _cardFilter(card);
        }
#pragma warning disable CA1031 // A broken filter fails closed for our own content, never for the game's.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _context.Log.Error($"Card filter threw for {card.Id}; hiding it.", ex);
            return false;
        }
    }

    /// <summary>Checks that every model class in the mod starts with <see cref="SpireMod.ModelPrefix"/>.</summary>
    internal void ValidateModelNames(Assembly modAssembly)
    {
        var prefix = _context.Mod.ModelPrefix;
        var offenders = modAssembly.GetTypes()
            .Where(t => t is { IsAbstract: false } && t.IsSubclassOf(typeof(AbstractModel)) && !t.Name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(static t => t.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (offenders.Count == 0)
        {
            _context.Health.Report(_context.Id, NamesFeature, FeatureStatus.Healthy);
            return;
        }

        var detail = $"model classes missing prefix '{prefix}': {string.Join(", ", offenders)}";
        _context.Health.Report(_context.Id, NamesFeature, FeatureStatus.Degraded, detail);
        _context.Log.Error($"{detail}. Model ids come from class names only and may collide with other content.");
    }

    private ContentRegistry Add<TPool, TModel>()
        where TPool : AbstractModel, IPoolModel
        where TModel : AbstractModel
    {
        ModHelper.AddModelToPool<TPool, TModel>();
        _registered.Add(typeof(TModel));
        return this;
    }
}
