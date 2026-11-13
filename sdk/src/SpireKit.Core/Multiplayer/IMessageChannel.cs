namespace SpireKit.Multiplayer;

/// <summary>
/// A named, bidirectional byte pipe between peers. The game-bound layer multiplexes any number of
/// channels over a single network message type; Core code builds typed protocols on top.
/// </summary>
public interface IMessageChannel
{
    /// <summary>Raised for each payload received on this channel. Arguments: payload, sender peer id.</summary>
    event Action<byte[], ulong>? Received;

    /// <summary>
    /// Host: deliver to every connected (and fully joined) peer.
    /// Client: deliver to the host.
    /// </summary>
    void Send(byte[] payload);

    /// <summary>Deliver to one specific peer. Clients may only target the host.</summary>
    void SendTo(ulong peer, byte[] payload);
}
