using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using SpireKit.Events;
using SpireKit.Logging;

namespace SpireKit.Multiplayer;

/// <summary>Published on the event bus whenever this machine's session role changes.</summary>
public sealed record SessionChanged(SessionRole Role);

/// <summary>Published on the host when a peer's connection is established.</summary>
public sealed record PeerConnected(ulong PeerId);

/// <summary>Published on a client once connected to the host.</summary>
public sealed record ConnectedToHost;

/// <summary>
/// Tracks whether this machine is offline, solo, hosting or a client, and exposes the live network service.
/// </summary>
/// <remarks>
/// The game creates one network service per lobby (host or client) that lives until disconnect; SpireKit
/// observes those constructors. Single-player runs are detected when they start.
/// </remarks>
public sealed class SessionTracker(IEventPublisher events, IModLog log)
{
    public SessionRole Role { get; private set; } = SessionRole.Offline;

    /// <summary>The game's network service for the current lobby/run; null when offline.</summary>
    public INetGameService? Service { get; private set; }

    internal void OnHostServiceCreated(NetHostGameService service)
    {
        service.ClientConnected += peer => events.Publish(new PeerConnected(peer));
        service.Disconnected += _ => OnServiceDisconnected(service);
        Set(SessionRole.Host, service);
    }

    internal void OnClientServiceCreated(NetClientGameService service)
    {
        service.ConnectedToHost += () => events.Publish(new ConnectedToHost());
        service.Disconnected += _ => OnServiceDisconnected(service);
        Set(SessionRole.Client, service);
    }

    /// <summary>Called once the game is initialized; detects single-player runs.</summary>
    internal void AttachToRunManager()
    {
        RunManager.Instance.RunStarted += _ =>
        {
            if (RunManager.Instance.NetService is { Type: NetGameType.Singleplayer } service)
            {
                Set(SessionRole.Solo, service);
            }
        };
    }

    /// <summary>Called when a run is torn down (back to the main menu).</summary>
    internal void OnRunCleanedUp()
    {
        if (Role == SessionRole.Solo)
        {
            Set(SessionRole.Offline, null);
        }
    }

    private void OnServiceDisconnected(INetGameService service)
    {
        if (ReferenceEquals(Service, service))
        {
            Set(SessionRole.Offline, null);
        }
    }

    private void Set(SessionRole role, INetGameService? service)
    {
        if (Role == role && ReferenceEquals(Service, service))
        {
            return;
        }

        Role = role;
        Service = service;
        log.Info($"Session role: {role}.");
        events.Publish(new SessionChanged(role));
    }
}
