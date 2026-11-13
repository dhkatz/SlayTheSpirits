using MegaCrit.Sts2.Core.Localization;
using SpireKit.Logging;

namespace SpireKit.Localization;

/// <summary>
/// Adds localization entries at runtime. Entries survive language changes (the game rebuilds every table
/// on a locale switch, so they are re-applied).
/// </summary>
/// <remarks>
/// Static text belongs in the mod's PCK (<c>localization/&lt;lang&gt;/&lt;table&gt;.json</c>). Use this for entries that
/// are generated, e.g. the <c>{Title}</c>/<c>{Body}</c> templates behind <see cref="SpireTips"/>.
/// Only tables that exist in the base game can be targeted.
/// </remarks>
public sealed class LocInjector(IModLog log)
{
    private readonly Dictionary<string, Dictionary<string, string>> _entries = new(StringComparer.Ordinal);
    private bool _subscribed;

    /// <summary>Registers (or replaces) an entry and applies it immediately if localization is loaded.</summary>
    public void Set(string table, string key, string value)
    {
        if (!_entries.TryGetValue(table, out var entries))
        {
            entries = new Dictionary<string, string>(StringComparer.Ordinal);
            _entries[table] = entries;
        }

        if (entries.TryGetValue(key, out var existing) && existing == value)
        {
            return;
        }

        entries[key] = value;
        ApplyTable(table, new Dictionary<string, string> { [key] = value });
    }

    /// <summary>Called once localization exists; applies everything registered so far.</summary>
    internal void Attach()
    {
        if (LocManager.Instance is not { } manager || _subscribed)
        {
            return;
        }

        manager.SubscribeToLocaleChange(ApplyAll);
        _subscribed = true;
        ApplyAll();
    }

    private void ApplyAll()
    {
        foreach (var (table, entries) in _entries)
        {
            ApplyTable(table, entries);
        }
    }

    private void ApplyTable(string table, Dictionary<string, string> entries)
    {
        if (LocManager.Instance is not { } manager)
        {
            return; // Applied by Attach().
        }

        try
        {
            manager.GetTable(table).MergeWith(new Dictionary<string, string>(entries));
        }
#pragma warning disable CA1031 // A missing table must not break the mod.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            log.Error($"Could not add localization to table '{table}'.", ex);
        }
    }
}
