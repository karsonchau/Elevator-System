using ElevatorSystem.Application.Events;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ElevatorSystem.Infrastructure.Events;

/// <summary>
/// In-memory event bus implementation for Phase 1
/// Note: This implementation is not suitable for production horizontal scaling
/// It will be replaced with a distributed event bus (e.g., Azure Service Bus, RabbitMQ) in later phases
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<object>> _handlers = new();
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        var eventType = typeof(T);
        
        _logger.LogDebug("Publishing event {EventType} with ID {EventId}", 
            @event.EventType, @event.EventId);

        if (!_handlers.TryGetValue(eventType, out var handlerBag))
        {
            _logger.LogDebug("No handlers registered for event type {EventType}", eventType.Name);
            return;
        }

        var handlers = handlerBag.Cast<Func<T, CancellationToken, Task>>().ToList();
        
        if (handlers.Count == 0)
        {
            _logger.LogDebug("No handlers found for event type {EventType}", eventType.Name);
            return;
        }

        _logger.LogDebug("Notifying {HandlerCount} handlers for event {EventType}", 
            handlers.Count, eventType.Name);

        // Execute all handlers in parallel for better performance
        var handlerTasks = handlers.Select(handler => 
            ExecuteHandlerSafely(handler, @event, cancellationToken));
        
        await Task.WhenAll(handlerTasks);
        
        _logger.LogDebug("Completed publishing event {EventType} with ID {EventId}", 
            @event.EventType, @event.EventId);
    }

    public void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class, IEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(T);
        var handlerBag = _handlers.GetOrAdd(eventType, _ => new ConcurrentBag<object>());
        handlerBag.Add(handler);

        _logger.LogDebug("Subscribed handler for event type {EventType}. Total handlers: {HandlerCount}", 
            eventType.Name, handlerBag.Count);
    }

    public void Unsubscribe<T>(Func<T, CancellationToken, Task> handler) where T : class, IEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(T);
        
        if (_handlers.TryGetValue(eventType, out var handlerBag))
        {
            // Note: ConcurrentBag doesn't support removal, so we need to replace the entire bag
            // This is a limitation of the in-memory implementation
            var currentHandlers = handlerBag.Cast<Func<T, CancellationToken, Task>>().ToList();
            currentHandlers.Remove(handler);
            
            var newBag = new ConcurrentBag<object>(currentHandlers.Cast<object>());
            _handlers.TryUpdate(eventType, newBag, handlerBag);
            
            _logger.LogDebug("Unsubscribed handler for event type {EventType}. Remaining handlers: {HandlerCount}", 
                eventType.Name, newBag.Count);
        }
    }

    private async Task ExecuteHandlerSafely<T>(Func<T, CancellationToken, Task> handler, T @event, CancellationToken cancellationToken) where T : class, IEvent
    {
        try
        {
            await handler(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing handler for event {EventType} with ID {EventId}", 
                @event.EventType, @event.EventId);
            // Continue processing other handlers even if one fails
        }
    }
}