namespace ElevatorSystem.Application.Events;

/// <summary>
/// Event bus interface for publishing and subscribing to events
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publish an event to all registered handlers
    /// </summary>
    /// <typeparam name="T">Type of event to publish</typeparam>
    /// <param name="event">The event instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class, IEvent;
    
    /// <summary>
    /// Subscribe to events of a specific type
    /// </summary>
    /// <typeparam name="T">Type of event to subscribe to</typeparam>
    /// <param name="handler">Handler function to process events</param>
    void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class, IEvent;
    
    /// <summary>
    /// Unsubscribe from events of a specific type
    /// </summary>
    /// <typeparam name="T">Type of event to unsubscribe from</typeparam>
    /// <param name="handler">Handler function to remove</param>
    void Unsubscribe<T>(Func<T, CancellationToken, Task> handler) where T : class, IEvent;
}