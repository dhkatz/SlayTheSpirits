using SpireKit.Logging;

namespace SpireKit.Events;

/// <summary>
/// In-process publish/subscribe bus for strongly typed events, with sync and async handlers.
/// </summary>
/// <remarks>
/// <para>
/// Handlers run in subscription order on the publishing thread. A throwing handler (sync, or a faulted
/// async one) is logged and isolated: it never prevents other handlers from running and never propagates
/// into game code.
/// </para>
/// <para>
/// Handlers are matched by exact event type and by any base type or interface the event implements,
/// so subscribing to a marker interface receives every event implementing it.
/// </para>
/// </remarks>
public sealed class EventBus : IEventBus
{
    private readonly IModLog _log;
    private readonly Lock _gate = new();
    private readonly Dictionary<Type, List<Subscription>> _byType = [];
    private long _nextOrder;

    public EventBus(IModLog? log = null)
    {
        _log = log ?? NullModLog.Instance;
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Add(typeof(TEvent), e =>
        {
            handler((TEvent)e);
            return Task.CompletedTask;
        });
    }

    /// <inheritdoc />
    public IDisposable SubscribeAsync<TEvent>(Func<TEvent, Task> handler)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Add(typeof(TEvent), e => handler((TEvent)e));
    }

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent message)
        where TEvent : notnull
    {
        foreach (var subscription in Snapshot(message.GetType()))
        {
            var task = Invoke(subscription, message);
            if (!task.IsCompleted)
            {
                _ = ObserveAsync(task, message);
            }
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent message)
        where TEvent : notnull
    {
        foreach (var subscription in Snapshot(message.GetType()))
        {
            await ObserveAsync(Invoke(subscription, message), message).ConfigureAwait(true);
        }
    }

    private Task Invoke(Subscription subscription, object message)
    {
        try
        {
            return subscription.Invoke(message);
        }
#pragma warning disable CA1031 // Isolation is the point: a broken handler must not break the game.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _log.Error($"Event handler for {message.GetType().Name} threw.", ex);
            return Task.CompletedTask;
        }
    }

    private async Task ObserveAsync(Task task, object message)
    {
        try
        {
            // Continue on the publishing context: game commands must stay on the game's main thread.
            await task.ConfigureAwait(true);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _log.Error($"Async event handler for {message.GetType().Name} failed.", ex);
        }
    }

    private Subscription Add(Type eventType, Func<object, Task> invoke)
    {
        var subscription = new Subscription(this, eventType, invoke, Interlocked.Increment(ref _nextOrder));
        lock (_gate)
        {
            if (!_byType.TryGetValue(eventType, out var list))
            {
                list = [];
                _byType[eventType] = list;
            }

            list.Add(subscription);
        }

        return subscription;
    }

    private List<Subscription> Snapshot(Type eventType)
    {
        var result = new List<Subscription>();
        lock (_gate)
        {
            foreach (var (type, list) in _byType)
            {
                if (type.IsAssignableFrom(eventType))
                {
                    result.AddRange(list);
                }
            }
        }

        result.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        return result;
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            if (_byType.TryGetValue(subscription.EventType, out var list))
            {
                list.Remove(subscription);
            }
        }
    }

    private sealed class Subscription(EventBus owner, Type eventType, Func<object, Task> invoke, long order) : IDisposable
    {
        private EventBus? _owner = owner;

        public Type EventType { get; } = eventType;

        public long Order { get; } = order;

        public Task Invoke(object message) => _owner is null ? Task.CompletedTask : invoke(message);

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Remove(this);
        }
    }
}
