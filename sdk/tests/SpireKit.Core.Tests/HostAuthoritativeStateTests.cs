using SpireKit.Multiplayer;

namespace SpireKit.Tests;

public sealed class HostAuthoritativeStateTests
{
    private sealed record Config(string Name);

    /// <summary>Connects one host channel to N client channels, delivering synchronously.</summary>
    private sealed class LoopbackNetwork
    {
        public Channel Host { get; }

        public List<Channel> Clients { get; } = [];

        public LoopbackNetwork() => Host = new Channel(this, isHost: true);

        /// <summary>When false, broadcasts skip clients (models peers that haven't finished joining).</summary>
        public bool ClientsReadyForBroadcast { get; set; } = true;

        public Channel AddClient()
        {
            var c = new Channel(this, isHost: false, peerId: (ulong)Clients.Count + 2);
            Clients.Add(c);
            return c;
        }

        public sealed class Channel(LoopbackNetwork net, bool isHost, ulong peerId = 1) : IAuthorityChannel<Config>
        {
            public event Action<AuthoritySnapshot<Config>>? SnapshotReceived;

            public event Action<ulong>? SnapshotRequested;

            public ulong PeerId { get; } = peerId;

            public void Broadcast(AuthoritySnapshot<Config> snapshot)
            {
                Assert.True(isHost);
                if (!net.ClientsReadyForBroadcast)
                {
                    return;
                }

                foreach (var client in net.Clients)
                {
                    client.SnapshotReceived?.Invoke(snapshot);
                }
            }

            public void SendTo(ulong peer, AuthoritySnapshot<Config> snapshot)
            {
                Assert.True(isHost);
                net.Clients.Single(c => c.PeerId == peer).SnapshotReceived?.Invoke(snapshot);
            }

            public void RequestFromHost()
            {
                Assert.False(isHost);
                net.Host.SnapshotRequested?.Invoke(PeerId);
            }

            /// <summary>Delivers a packet directly, e.g. to simulate reordering.</summary>
            public void Deliver(AuthoritySnapshot<Config> snapshot) => SnapshotReceived?.Invoke(snapshot);
        }
    }

    [Fact]
    public void Offline_and_solo_use_local_value()
    {
        var local = new Config("mine");
        var state = new HostAuthoritativeState<Config>(() => local);

        Assert.Same(local, state.Effective);
        state.Enter(SessionRole.Solo, null);
        Assert.Same(local, state.Effective);
    }

    [Fact]
    public void Client_ignores_local_settings_until_host_snapshot_arrives()
    {
        var net = new LoopbackNetwork();
        var clientChannel = net.AddClient();
        var client = new HostAuthoritativeState<Config>(() => new Config("client-local"));

        client.Enter(SessionRole.Client, clientChannel);
        Assert.False(client.IsReady);
        Assert.Null(client.Effective);

        var host = new HostAuthoritativeState<Config>(() => new Config("host"));
        host.Enter(SessionRole.Host, net.Host);

        Assert.Equal("host", client.Effective?.Name);
    }

    [Fact]
    public void Late_joining_client_requests_and_receives_current_state()
    {
        var net = new LoopbackNetwork();
        var host = new HostAuthoritativeState<Config>(() => new Config("host"));
        host.Enter(SessionRole.Host, net.Host);

        var client = new HostAuthoritativeState<Config>(() => new Config("client-local"));
        client.Enter(SessionRole.Client, net.AddClient());

        Assert.Equal("host", client.Effective?.Name);
    }

    [Fact]
    public void Client_retries_until_the_host_answers_directly()
    {
        var net = new LoopbackNetwork { ClientsReadyForBroadcast = false };
        var clientChannel = net.AddClient();
        var client = new HostAuthoritativeState<Config>(() => new Config("client-local"));

        // Request is lost: no host yet.
        var host = new HostAuthoritativeState<Config>(() => new Config("host"));
        client.Enter(SessionRole.Client, clientChannel);
        Assert.Null(client.Effective);

        // Host starts; its broadcast doesn't reach the not-yet-ready client.
        host.Enter(SessionRole.Host, net.Host);
        Assert.Null(client.Effective);

        // The retry gets a direct reply.
        client.RetryIfWaiting();
        Assert.Equal("host", client.Effective?.Name);
    }

    [Fact]
    public void Host_edits_propagate_until_locked_then_freeze_for_everyone()
    {
        var net = new LoopbackNetwork();
        var hostLocal = new Config("v1");
        var host = new HostAuthoritativeState<Config>(() => hostLocal);
        var client = new HostAuthoritativeState<Config>(() => new Config("client-local"));
        host.Enter(SessionRole.Host, net.Host);
        client.Enter(SessionRole.Client, net.AddClient());

        hostLocal = new Config("v2");
        host.NotifyLocalChanged();
        Assert.Equal("v2", client.Effective?.Name);

        host.Lock();
        Assert.True(client.IsLocked);

        hostLocal = new Config("v3");
        host.NotifyLocalChanged();
        Assert.Equal("v2", host.Effective?.Name);
        Assert.Equal("v2", client.Effective?.Name);

        host.Unlock();
        Assert.Equal("v3", client.Effective?.Name);
        Assert.False(client.IsLocked);
    }

    [Fact]
    public void Lock_can_restore_a_persisted_snapshot()
    {
        var net = new LoopbackNetwork();
        var host = new HostAuthoritativeState<Config>(() => new Config("current-profile"));
        var client = new HostAuthoritativeState<Config>(() => new Config("client-local"));
        host.Enter(SessionRole.Host, net.Host);
        client.Enter(SessionRole.Client, net.AddClient());

        host.Lock(new Config("from-save"));

        Assert.Equal("from-save", host.Effective?.Name);
        Assert.Equal("from-save", client.Effective?.Name);
    }

    [Fact]
    public void Client_drops_stale_revisions()
    {
        var net = new LoopbackNetwork();
        var clientChannel = net.AddClient();
        var client = new HostAuthoritativeState<Config>(() => new Config("client-local"));
        client.Enter(SessionRole.Client, clientChannel);

        var host = new HostAuthoritativeState<Config>(() => new Config("new"));
        host.Enter(SessionRole.Host, net.Host);
        clientChannel.Deliver(new AuthoritySnapshot<Config>(client.Revision, false, new Config("duplicate")));
        clientChannel.Deliver(new AuthoritySnapshot<Config>(client.Revision - 1, false, new Config("stale")));

        Assert.Equal("new", client.Effective?.Name);
    }

    [Fact]
    public void Only_authority_can_lock()
    {
        var net = new LoopbackNetwork();
        var client = new HostAuthoritativeState<Config>(() => new Config("x"));
        client.Enter(SessionRole.Client, net.AddClient());

        Assert.Throws<InvalidOperationException>(() => client.Lock());
    }
}
