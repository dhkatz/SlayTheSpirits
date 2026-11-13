using System.Text;
using SpireKit.Multiplayer;

namespace SpireKit.Tests;

public sealed class AuthorityChannelTests
{
    private sealed record Doc(string Text);

    /// <summary>Two raw channels wired back to back (peer 1 = host, peer 2 = client).</summary>
    private sealed class Pipe : IMessageChannel
    {
        public Pipe? Other { get; set; }

        public ulong Self { get; init; }

        public event Action<byte[], ulong>? Received;

        public void Send(byte[] payload) => Other!.Received?.Invoke(payload, Self);

        public void SendTo(ulong peer, byte[] payload) => Send(payload);

        public void Inject(byte[] payload) => Received?.Invoke(payload, 99);
    }

    private static (AuthorityChannel<Doc> Host, AuthorityChannel<Doc> Client, Pipe ClientPipe) Connect(RecordingLog? log = null)
    {
        var hostPipe = new Pipe { Self = 1 };
        var clientPipe = new Pipe { Self = 2, Other = hostPipe };
        hostPipe.Other = clientPipe;
        static byte[] Encode(Doc d) => Encoding.UTF8.GetBytes(d.Text);
        static Doc? Decode(byte[] b) => b.Length == 0 ? null : new Doc(Encoding.UTF8.GetString(b));
        return (new(hostPipe, Encode, Decode, log), new(clientPipe, Encode, Decode, log), clientPipe);
    }

    [Fact]
    public void End_to_end_with_host_authoritative_state()
    {
        var (hostChannel, clientChannel, _) = Connect();
        var hostState = new HostAuthoritativeState<Doc>(() => new Doc("house rules"));
        var clientState = new HostAuthoritativeState<Doc>(() => new Doc("ignored"));

        hostState.Enter(SessionRole.Host, hostChannel);
        clientState.Enter(SessionRole.Client, clientChannel);
        hostState.Lock();

        Assert.Equal("house rules", clientState.Effective?.Text);
        Assert.True(clientState.IsLocked);
        Assert.Equal(hostState.Revision, clientState.Revision);
    }

    [Fact]
    public void Request_carries_the_sender_peer_id()
    {
        var (hostChannel, clientChannel, _) = Connect();
        ulong? requester = null;
        hostChannel.SnapshotRequested += p => requester = p;

        clientChannel.RequestFromHost();

        Assert.Equal(2UL, requester);
    }

    [Theory]
    [InlineData(new byte[] { 2, 0, 0, 0, 1, 0 })] // snapshot with empty (rejected) payload
    [InlineData(new byte[] { 2, 0, 0 })] // truncated header
    [InlineData(new byte[] { 77 })] // unknown kind
    [InlineData(new byte[0])]
    public void Malformed_packets_are_dropped_without_throwing(byte[] packet)
    {
        var (_, clientChannel, clientPipe) = Connect(new RecordingLog());
        var received = false;
        clientChannel.SnapshotReceived += _ => received = true;

        clientPipe.Inject(packet);

        Assert.False(received);
    }
}
