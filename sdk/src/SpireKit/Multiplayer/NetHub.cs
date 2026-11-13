using MegaCrit.Sts2.Core.Multiplayer.Game;
using SpireKit.Logging;

namespace SpireKit.Multiplayer;

/// <summary>
/// Named <see cref="IMessageChannel"/>s for one mod, multiplexed over <see cref="SpireNetEnvelope"/>.
/// Handlers are (re)registered automatically whenever the game creates a new network service.
/// </summary>
public sealed class NetHub
{
    private readonly string _modId;
    private readonly SessionTracker _session;
    private readonly IModLog _log;
    private readonly Dictionary<string, HubChannel> _channels = new(StringComparer.Ordinal);
    private INetGameService? _registeredOn;

    internal NetHub(string modId, SessionTracker session, IModLog log)
    {
        _modId = modId;
        _session = session;
        _log = log;
    }

    /// <summary>Gets (or creates) a channel. Names only need to be unique within this mod.</summary>
    public IMessageChannel Channel(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_channels.TryGetValue(name, out var channel))
        {
            channel = new HubChannel(this, name);
            _channels[name] = channel;
        }

        return channel;
    }

    /// <summary>Called when the session's network service changes.</summary>
    internal void Rebind(INetGameService? service)
    {
        if (ReferenceEquals(_registeredOn, service))
        {
            return;
        }

        if (_registeredOn is not null)
        {
            try
            {
                _registeredOn.UnregisterMessageHandler<SpireNetEnvelope>(OnEnvelope);
            }
#pragma warning disable CA1031 // Old service may already be torn down.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _log.Debug($"Unregister on old service failed: {ex.Message}");
            }
        }

        _registeredOn = null;
        if (service?.Type is NetGameType.Host or NetGameType.Client)
        {
            service.RegisterMessageHandler<SpireNetEnvelope>(OnEnvelope);
            _registeredOn = service;
        }
    }

    private void Send(string channel, byte[] payload, ulong? to)
    {
        var service = _session.Service;
        if (service?.Type is not (NetGameType.Host or NetGameType.Client))
        {
            return;
        }

        // The service exists before its connection does (host still binding, client still handshaking).
        // Sending then makes the game log an error; protocols above us retry, so just skip quietly.
        if (!service.IsConnected)
        {
            _log.Debug($"Not connected yet; skipped send on '{channel}'.");
            return;
        }

        if (payload.Length > SpireNetEnvelope.MaxPayloadBytes)
        {
            _log.Error($"Refusing to send {payload.Length} bytes on '{channel}' (max {SpireNetEnvelope.MaxPayloadBytes}).");
            return;
        }

        var envelope = new SpireNetEnvelope { Channel = channel, Payload = payload };
        try
        {
            if (to is { } peer)
            {
                service.SendMessage(envelope, peer);
            }
            else
            {
                service.SendMessage(envelope);
            }
        }
#pragma warning disable CA1031 // Network failures must not break game flow.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _log.Warn($"Send on '{channel}' failed.", ex);
        }
    }

    private void OnEnvelope(SpireNetEnvelope envelope, ulong senderId)
    {
        if (_channels.TryGetValue(envelope.Channel, out var channel))
        {
            channel.Raise(envelope.Payload, senderId);
        }
        else
        {
            _log.Debug($"[{_modId}] No channel '{envelope.Channel}'; dropped message from {senderId}.");
        }
    }

    private sealed class HubChannel(NetHub hub, string name) : IMessageChannel
    {
        public event Action<byte[], ulong>? Received;

        public void Send(byte[] payload) => hub.Send(name, payload, to: null);

        public void SendTo(ulong peer, byte[] payload) => hub.Send(name, payload, peer);

        public void Raise(byte[] payload, ulong sender)
        {
            try
            {
                Received?.Invoke(payload, sender);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                hub._log.Error($"Handler for channel '{name}' threw.", ex);
            }
        }
    }
}
