using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpirits.Cards;
using SpireKit.Logging;
using SpireKit.Modding;
using SpireKit.Multiplayer;
using SpireKit.Storage;

namespace SlayTheSpirits.Rules;

/// <summary>
/// Owns the rules in effect: the local host profile on disk, host-authoritative sync, and the run lock.
/// </summary>
/// <remarks>
/// Clients never read their local profile; until the host's rules arrive, <see cref="Effective"/> is null and
/// every feature (penalties, Tavern cards) stays off.
/// </remarks>
internal sealed class RulesService
{
    private const string ProfilePath = "profile.json";
    private const string CustomPath = "custom.json";
    private const string LockedRunsDir = "runs";
    private const int MaxClientRetries = 30;

    private readonly ModContext _context;
    private readonly IModLog _log;
    private JsonDocumentFile<RuleSet>? _profileFile;
    private JsonDocumentFile<RuleSet>? _customFile;
    private RuleSet _profile = Presets.Standard;
    private RuleSet? _custom;
    private AuthorityChannel<RuleSet>? _channel;
    private int _retries;

    public RulesService(ModContext context)
    {
        _context = context;
        _log = context.Log.WithScope("rules");
        State = new HostAuthoritativeState<RuleSet>(() => _profile, _log);
        State.EffectiveChanged += OnEffectiveChanged;
    }

    /// <summary>Raised when the rules in effect change (null = waiting for the host).</summary>
    public event Action<RuleSet?>? EffectiveChanged;

    public HostAuthoritativeState<RuleSet> State { get; }

    /// <summary>The rules every feature should use right now.</summary>
    public RuleSet? Effective => State.Effective;

    /// <summary>This machine's saved profile (what it uses when hosting).</summary>
    public RuleSet Profile => _profile;

    /// <summary>
    /// The last non-preset rules this machine used, kept so trying out presets in the editor doesn't lose them.
    /// Null until the profile has been customized once.
    /// </summary>
    public RuleSet? LastCustom => _custom;

    /// <summary>
    /// Whether this machine's profile decides the rules right now: not while a run is locked, and never as a
    /// client (the host's rules apply; editing would change nothing).
    /// </summary>
    public bool CanEditProfile => !State.IsLocked && State.Role != SessionRole.Client;

    /// <summary>Call from <c>OnInitialize</c>: wires sync to session changes.</summary>
    public void Initialize()
    {
        _channel = new AuthorityChannel<RuleSet>(_context.Net.Channel("rules"), Encode, Decode, _log);
        _context.Events.Subscribe<SessionChanged>(e => OnSessionChanged(e.Role));
        _context.Events.Subscribe<ConnectedToHost>(_ => State.RetryIfWaiting());
    }

    /// <summary>Call from <c>OnGameReady</c>: storage is available from here on.</summary>
    public void LoadProfile()
    {
        _profileFile = new JsonDocumentFile<RuleSet>(_context.Storage, ProfilePath, RuleSetCodec.Format, static () => Presets.Standard, _log);
        _profile = _profileFile.Load();
        _customFile = new JsonDocumentFile<RuleSet>(_context.Storage, CustomPath, RuleSetCodec.Format, () => _profile, _log);
        if (_context.Storage.ReadText(CustomPath) is not null)
        {
            _custom = _customFile.Load();
        }
        else if (RuleEditing.PresetIndex(_profile) < 0)
        {
            RememberCustom(_profile);
        }

        _log.Info($"Loaded profile '{_profile.Name}'.");
    }

    /// <summary>Replaces the local profile (settings UI, share-code import) and syncs it if hosting.</summary>
    public void UpdateProfile(RuleSet rules)
    {
        _profile = rules ?? throw new ArgumentNullException(nameof(rules));
        _profileFile?.Save(rules);
        if (RuleEditing.PresetIndex(rules) < 0)
        {
            RememberCustom(rules);
        }

        State.NotifyLocalChanged();
    }

    private void RememberCustom(RuleSet rules)
    {
        _custom = rules;
        _customFile?.Save(rules);
    }

    /// <summary>
    /// Authority only: freezes the rules for a run. For a resumed run, the rules locked when it began are
    /// restored from disk so a profile edit between sessions can't change a run in progress.
    /// </summary>
    public void LockForRun(string runKey, bool resumed)
    {
        if (!State.Role.IsAuthority())
        {
            return;
        }

        RuleSet? snapshot = null;
        if (resumed && _context.Storage.ReadText(RunFile(runKey)) is { } json)
        {
            var decoded = RuleSetCodec.FromJson(json);
            snapshot = decoded.Status == DecodeStatus.Invalid ? null : decoded.Value;
        }

        var locked = State.Lock(snapshot);
        _context.Storage.WriteText(RunFile(runKey), RuleSetCodec.ToJson(locked));
        _log.Info($"Locked rules '{locked.Name}' for run {runKey}{(snapshot is null ? string.Empty : " (restored)")}.");
    }

    /// <summary>Authority only: run over; forget its lock.</summary>
    public void UnlockAfterRun(string runKey)
    {
        if (!State.Role.IsAuthority())
        {
            return;
        }

        State.Unlock();
        _context.Storage.Delete(RunFile(runKey));
    }

    /// <summary>Whether a mod card may appear in this run. Must be identical on all peers (reads synced rules only).</summary>
    public bool IsCardAllowed(CardModel card)
    {
        if (Effective?.Content is not { } content)
        {
            return false;
        }

        if (content.DisabledCards.Contains(card.Id.Entry, StringComparer.Ordinal))
        {
            return false;
        }

        return card is ISpiritsCharacterCard ? content.CharacterCards : content.TavernCards;
    }

    private static string RunFile(string runKey) => $"{LockedRunsDir}/{runKey}.json";

    private static byte[] Encode(RuleSet rules) => Encoding.UTF8.GetBytes(RuleSetCodec.ToJson(rules));

    private static RuleSet? Decode(byte[] bytes)
    {
        var result = RuleSetCodec.FromJson(Encoding.UTF8.GetString(bytes));
        return result.Status == DecodeStatus.Invalid ? null : result.Value;
    }

    private void OnSessionChanged(SessionRole role)
    {
        _retries = 0;
        State.Enter(role, role is SessionRole.Host or SessionRole.Client ? _channel : null);
        if (role == SessionRole.Client)
        {
            ScheduleRetry();
        }
    }

    /// <summary>Clients re-ask once a second until the host answers (early requests can be lost mid-handshake).</summary>
    private void ScheduleRetry()
    {
        if (Godot.Engine.GetMainLoop() is not SceneTree tree)
        {
            return;
        }

        tree.CreateTimer(1.0).Timeout += () =>
        {
            if (State.Role != SessionRole.Client || State.IsReady)
            {
                return;
            }

            if (++_retries > MaxClientRetries)
            {
                _log.Warn("Host never sent rules (does the host have the same mod version?). Drinking game is off.");
                return;
            }

            State.RetryIfWaiting();
            ScheduleRetry();
        };
    }

    private void OnEffectiveChanged(RuleSet? rules) => EffectiveChanged?.Invoke(rules);
}
