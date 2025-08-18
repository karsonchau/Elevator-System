namespace ElevatorSystem.Application.Events;

/// <summary>
/// Base implementation for all events with common properties
/// </summary>
public abstract class BaseEvent : IEvent
{
    protected BaseEvent()
    {
        EventId = Guid.NewGuid();
        Timestamp = DateTime.UtcNow;
        EventType = GetType().Name;
        Version = 1;
    }
    
    public Guid EventId { get; }
    public DateTime Timestamp { get; }
    public string EventType { get; }
    public virtual int Version { get; } = 1;
}