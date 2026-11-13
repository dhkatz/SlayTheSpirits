namespace SpireKit.Events;

/// <summary>Publishes events to subscribers.</summary>
public interface IEventPublisher
{
    /// <summary>
    /// Delivers <paramref name="message"/> to every matching subscriber, synchronously. Async subscribers are
    /// started but not awaited (their failures are still logged).
    /// </summary>
    void Publish<TEvent>(TEvent message)
        where TEvent : notnull;

    /// <summary>
    /// Delivers <paramref name="message"/> to every matching subscriber in subscription order, awaiting each
    /// async subscriber before the next. Use when the caller can wait, e.g. inside a game hook so that mod
    /// commands (apply a power, gain block) run inside the game's lockstep action flow.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent message)
        where TEvent : notnull;
}

/// <summary>Registers interest in events.</summary>
public interface IEventSource
{
    /// <summary>
    /// Subscribes <paramref name="handler"/> to <typeparamref name="TEvent"/> and anything assignable to it.
    /// Dispose the returned handle to unsubscribe.
    /// </summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : notnull;

    /// <summary>Like <see cref="Subscribe{TEvent}"/>, for handlers that need to await game commands.</summary>
    IDisposable SubscribeAsync<TEvent>(Func<TEvent, Task> handler)
        where TEvent : notnull;
}

/// <summary>A bus that both publishes and accepts subscriptions.</summary>
public interface IEventBus : IEventPublisher, IEventSource;
