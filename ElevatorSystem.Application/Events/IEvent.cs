namespace ElevatorSystem.Application.Events;

/// <summary>
/// Base interface for all events in the system
/// </summary>
public interface IEvent
{
    /// <summary>
    /// Unique identifier for this event instance
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// UTC timestamp when the event occurred
    /// </summary>
    DateTime Timestamp { get; }
    
    /// <summary>
    /// Type name of the event for serialization and routing
    /// </summary>
    string EventType { get; }
    
    /// <summary>
    /// Version of the event schema for backward compatibility
    /// </summary>
    int Version { get; }
}