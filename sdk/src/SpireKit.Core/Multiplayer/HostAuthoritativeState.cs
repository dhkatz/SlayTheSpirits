using SpireKit.Logging;

namespace SpireKit.Multiplayer;

/// <summary>A versioned copy of host-owned state, as sent over the wire.</summary>
/// <param name="Revision">Monotonic per host session; clients ignore anything not newer than what they hold.</param>
/// <param name="Locked">True once a run has started and the state is frozen for its duration.</param>
/// <param name="Value">The state itself.</param>
public sealed record AuthoritySnapshot<T>(int Revision, bool Locked, T Value)
    where T : class;

/// <summary>Moves <see cref="AuthoritySnapshot{T}"/>s between peers. Implemented by the game-bound layer.</summary>
public interface IAuthorityChannel<T>
    where T : class
{
    /// <summary>Raised on the receiving side for every snapshot that arrives.</summary>
    event Action<AuthoritySnapshot<T>>? SnapshotReceived;

    /// <summary>Raised on the host when a client asks for the current snapshot. Argument: the requesting peer.</summary>
    event Action<ulong>? SnapshotRequested;

    /// <summary>Host: send to every connected client.</summary>
    void Broadcast(AuthoritySnapshot<T> snapshot);

    /// <summary>Host: send to one peer (answers requests from peers that haven't finished joining yet).</summary>
    void SendTo(ulong peer, AuthoritySnapshot<T> snapshot);

    /// <summary>Client: ask the host to (re)send its snapshot. Safe to call repeatedly.</summary>
    void RequestFromHost();
}

/// <summary>
/// Host-authoritative shared state with an optional run lock.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Offline / Solo / Host</b>: <see cref="Effective"/> is the local value.
/// The host broadcasts every change and answers client requests.</item>
/// <item><b>Client</b>: local settings are ignored entirely. <see cref="Effective"/> is <see langword="null"/>
/// until the host's snapshot arrives; features must stay inert until <see cref="IsReady"/>.</item>
/// <item><b>Locked</b> (a run is in progress): <see cref="Effective"/> is frozen to the lock snapshot for
/// everyone. Local edits on the host still update the local profile but do not propagate until unlocked.</item>
/// </list>
/// </remarks>
public sealed class HostAuthoritativeState<T>
    where T : class
{
    private readonly Func<T> _readLocal;
    private readonly IModLog _log;
    private IAuthorityChannel<T>? _channel;
    private AuthoritySnapshot<T>? _remote;
    private T? _locked;
    private int _revision;

    public HostAuthoritativeState(Func<T> readLocal, IModLog? log = null)
    {
        _readLocal = readLocal ?? throw new ArgumentNullException(nameof(readLocal));
        _log = log ?? NullModLog.Instance;
    }

    /// <summary>Raised whenever <see cref="Effective"/> may have changed. Argument is the new value (null = not ready).</summary>
    public event Action<T?>? EffectiveChanged;

    public SessionRole Role { get; private set; } = SessionRole.Offline;

    /// <summary>Revision of the value currently in effect (host: last broadcast; client: last accepted).</summary>
    public int Revision => Role == SessionRole.Client ? _remote?.Revision ?? 0 : _revision;

    public bool IsLocked => Role == SessionRole.Client ? _remote?.Locked ?? false : _locked is not null;

    /// <summary>The value every feature should read. Null only on a client still waiting for the host.</summary>
    public T? Effective => Role switch
    {
        SessionRole.Client => _remote?.Value,
        _ => _locked ?? _readLocal(),
    };

    public bool IsReady => Effective is not null;

    /// <summary>Switches role and (re)binds the transport. Call on lobby create/join/leave.</summary>
    public void Enter(SessionRole role, IAuthorityChannel<T>? channel)
    {
        if (role is SessionRole.Host or SessionRole.Client && channel is null)
        {
            throw new ArgumentNullException(nameof(channel), $"A channel is required for role {role}.");
        }

        Unbind();
        Role = role;
        _remote = null;
        _locked = null;
        _revision = 0;
        _channel = channel;
        if (_channel is not null)
        {
            _channel.SnapshotReceived += OnSnapshotReceived;
            _channel.SnapshotRequested += OnSnapshotRequested;
        }

        _log.Debug($"Entered role {role}.");
        if (role == SessionRole.Client)
        {
            _channel!.RequestFromHost();
        }
        else if (role == SessionRole.Host)
        {
            Publish();
        }

        EffectiveChanged?.Invoke(Effective);
    }

    /// <summary>Back to <see cref="SessionRole.Offline"/>, dropping any remote/locked state.</summary>
    public void Leave() => Enter(SessionRole.Offline, null);

    /// <summary>Host/Solo/Offline: call after the local value changed (e.g. settings UI edit).</summary>
    public void NotifyLocalChanged()
    {
        if (!Role.IsAuthority() || _locked is not null)
        {
            return;
        }

        Publish();
        EffectiveChanged?.Invoke(Effective);
    }

    /// <summary>
    /// Authority only: freezes the state for a run. Pass <paramref name="snapshot"/> to restore a lock
    /// persisted with a saved run; otherwise the current local value is captured.
    /// </summary>
    public T Lock(T? snapshot = null)
    {
        if (!Role.IsAuthority())
        {
            throw new InvalidOperationException("Only the authority can lock shared state.");
        }

        _locked = snapshot ?? _readLocal();
        Publish();
        EffectiveChanged?.Invoke(Effective);
        return _locked;
    }

    /// <summary>Authority only: releases the run lock (run ended / back to lobby).</summary>
    public void Unlock()
    {
        if (!Role.IsAuthority() || _locked is null)
        {
            return;
        }

        _locked = null;
        Publish();
        EffectiveChanged?.Invoke(Effective);
    }

    private void Publish()
    {
        _revision++;
        if (Role != SessionRole.Host || _channel is null)
        {
            return;
        }

        _channel.Broadcast(new AuthoritySnapshot<T>(_revision, _locked is not null, Effective!));
    }

    /// <summary>
    /// Client: re-asks the host if still waiting. Call periodically (e.g. once a second) from the game layer;
    /// early requests can be lost while a peer is still completing the join handshake.
    /// </summary>
    public void RetryIfWaiting()
    {
        if (Role == SessionRole.Client && _remote is null)
        {
            _channel?.RequestFromHost();
        }
    }

    private void OnSnapshotRequested(ulong peer)
    {
        if (Role != SessionRole.Host || _channel is null)
        {
            return;
        }

        // Re-send without bumping the revision: nothing changed, a peer just needs it.
        _channel.SendTo(peer, new AuthoritySnapshot<T>(_revision, _locked is not null, Effective!));
    }

    private void OnSnapshotReceived(AuthoritySnapshot<T> snapshot)
    {
        if (Role != SessionRole.Client)
        {
            _log.Debug($"Ignoring snapshot r{snapshot.Revision}: role is {Role}.");
            return;
        }

        if (_remote is not null && snapshot.Revision <= _remote.Revision)
        {
            return;
        }

        _remote = snapshot;
        _log.Info($"Applied host state r{snapshot.Revision}{(snapshot.Locked ? " (locked)" : string.Empty)}.");
        EffectiveChanged?.Invoke(Effective);
    }

    private void Unbind()
    {
        if (_channel is null)
        {
            return;
        }

        _channel.SnapshotReceived -= OnSnapshotReceived;
        _channel.SnapshotRequested -= OnSnapshotRequested;
        _channel = null;
    }
}
