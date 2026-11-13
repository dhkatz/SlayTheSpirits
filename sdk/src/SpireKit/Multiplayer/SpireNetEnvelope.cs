using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace SpireKit.Multiplayer;

/// <summary>
/// The single network message type SpireKit registers per mod. Carries a channel name and an opaque payload,
/// so mods can add protocols without adding message types (the game allows at most 256 in total and assigns
/// their wire ids by sorting type names across all mods).
/// </summary>
public sealed class SpireNetEnvelope : INetMessage
{
    /// <summary>Upper bound on payload size; anything larger is a bug, not a use case.</summary>
    public const int MaxPayloadBytes = 256 * 1024;

    public string Channel { get; set; } = string.Empty;

    public byte[] Payload { get; set; } = [];

    public bool ShouldBroadcast => false;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString(Channel);
        writer.WriteInt(Payload.Length);
        writer.WriteBytes(Payload, Payload.Length);
    }

    public void Deserialize(PacketReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        Channel = reader.ReadString();
        var length = reader.ReadInt();
        if (length is < 0 or > MaxPayloadBytes)
        {
            throw new InvalidDataException($"SpireNetEnvelope payload length {length} is out of range.");
        }

        Payload = new byte[length];
        reader.ReadBytes(Payload, length);
    }
}
