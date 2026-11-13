using SpireKit.Events;

namespace SpireKit.Tests;

public sealed class EventBusTests
{
    private interface IMarker;

    private sealed record Ping(int N) : IMarker;

    [Fact]
    public void Delivers_to_exact_and_assignable_subscribers_in_subscription_order()
    {
        var bus = new EventBus();
        var seen = new List<string>();
        bus.Subscribe<IMarker>(_ => seen.Add("marker"));
        bus.Subscribe<Ping>(p => seen.Add($"ping{p.N}"));
        bus.Subscribe<object>(_ => seen.Add("object"));

        bus.Publish(new Ping(1));

        Assert.Equal(["marker", "ping1", "object"], seen);
    }

    [Fact]
    public void Disposed_subscription_stops_receiving()
    {
        var bus = new EventBus();
        var count = 0;
        var sub = bus.Subscribe<Ping>(_ => count++);
        bus.Publish(new Ping(1));
        sub.Dispose();
        sub.Dispose();
        bus.Publish(new Ping(2));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PublishAsync_awaits_async_handlers_in_order_with_sync_handlers_interleaved()
    {
        var bus = new EventBus();
        var seen = new List<string>();
        var gate = new TaskCompletionSource();
        bus.SubscribeAsync<Ping>(async _ =>
        {
            seen.Add("async-start");
            await gate.Task;
            seen.Add("async-end");
        });
        bus.Subscribe<Ping>(_ => seen.Add("sync"));

        var publishing = bus.PublishAsync(new Ping(1));
        Assert.Equal(["async-start"], seen);

        gate.SetResult();
        await publishing;
        Assert.Equal(["async-start", "async-end", "sync"], seen);
    }

    [Fact]
    public async Task Faulted_async_handler_is_isolated_and_logged()
    {
        var log = new RecordingLog();
        var bus = new EventBus(log);
        var reached = false;
        bus.SubscribeAsync<Ping>(async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        });
        bus.Subscribe<Ping>(_ => reached = true);

        await bus.PublishAsync(new Ping(1));

        Assert.True(reached);
        Assert.Contains(log.Entries, e => e.Exception is InvalidOperationException);
    }

    [Fact]
    public void Throwing_handler_is_isolated_and_logged()
    {
        var log = new RecordingLog();
        var bus = new EventBus(log);
        var reached = false;
        bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
        bus.Subscribe<Ping>(_ => reached = true);

        bus.Publish(new Ping(1));

        Assert.True(reached);
        Assert.Contains(log.Entries, e => e.Exception is InvalidOperationException);
    }
}
