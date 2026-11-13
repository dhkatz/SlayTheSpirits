using System.Buffers.Binary;
using SpireKit.Logging;

namespace SpireKit.Multiplayer;

/// <summary>
/// Implements the <see cref="IAuthorityChannel{T}"/> protocol over a raw <see cref="IMessageChannel"/>.
/// </summary>
/// <remarks>
/// Wire format (big-endian): <c>[kind:1][revision:4][locked:1][payload…]</c>.
/// <c>kind</c> 1 = request (no further fields), 2 = snapshot.
/// </remarks>
public sealed class AuthorityChannel<T> : IAuthorityChannel<T>
    where T : class
{
    private const byte KindRequest = 1;
    private const byte KindSnapshot = 2;
    private const int HeaderLength = 6;

    private readonly IMessageChannel _raw;
    private readonly Func<T, byte[]> _encode;
    private readonly Func<byte[], T?> _decode;
    private readonly IModLog _log;

    /// <param name="raw">Transport.</param>
    /// <param name="encode">Serializes a value.</param>
    /// <param name="decode">Deserializes a value; return null (or throw) to reject it.</param>
    /// <param name="log">Diagnostics.</param>
    public AuthorityChannel(IMessageChannel raw, Func<T, byte[]> encode, Func<byte[], T?> decode, IModLog? log = null)
    {
        _raw = raw ?? throw new ArgumentNullException(nameof(raw));
        _encode = encode ?? throw new ArgumentNullException(nameof(encode));
        _decode = decode ?? throw new ArgumentNullException(nameof(decode));
        _log = log ?? NullModLog.Instance;
        _raw.Received += OnReceived;
    }

    public event Action<AuthoritySnapshot<T>>? SnapshotReceived;

    public event Action<ulong>? SnapshotRequested;

    public void Broadcast(AuthoritySnapshot<T> snapshot) => _raw.Send(EncodeSnapshot(snapshot));

    public void SendTo(ulong peer, AuthoritySnapshot<T> snapshot) => _raw.SendTo(peer, EncodeSnapshot(snapshot));

    public void RequestFromHost() => _raw.Send([KindRequest]);

    private byte[] EncodeSnapshot(AuthoritySnapshot<T> snapshot)
    {
        var body = _encode(snapshot.Value);
        var packet = new byte[HeaderLength + body.Length];
        packet[0] = KindSnapshot;
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(1, 4), snapshot.Revision);
        packet[5] = snapshot.Locked ? (byte)1 : (byte)0;
        body.CopyTo(packet, HeaderLength);
        return packet;
    }

    private void OnReceived(byte[] packet, ulong sender)
    {
        if (packet.Length == 0)
        {
            return;
        }

        switch (packet[0])
        {
            case KindRequest:
                SnapshotRequested?.Invoke(sender);
                break;
            case KindSnapshot when packet.Length >= HeaderLength:
                var revision = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(1, 4));
                var locked = packet[5] != 0;
                T? value;
                try
                {
                    value = _decode(packet[HeaderLength..]);
                }
#pragma warning disable CA1031 // A malformed packet from a peer must never crash this peer.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _log.Warn($"Dropping undecodable snapshot r{revision} from {sender}.", ex);
                    return;
                }

                if (value is null)
                {
                    _log.Warn($"Dropping rejected snapshot r{revision} from {sender}.");
                    return;
                }

                SnapshotReceived?.Invoke(new AuthoritySnapshot<T>(revision, locked, value));
                break;
            default:
                _log.Warn($"Dropping unknown packet kind {packet[0]} from {sender}.");
                break;
        }
    }
}
