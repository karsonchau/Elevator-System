using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Events.ElevatorEvents;

/// <summary>
/// Event published when an elevator request is assigned to a specific elevator
/// </summary>
public class ElevatorRequestAssignedEvent : BaseEvent
{
    public ElevatorRequestAssignedEvent(Guid requestId, int elevatorId, string? correlationId = null)
    {
        RequestId = requestId;
        ElevatorId = elevatorId;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// Unique identifier of the request
    /// </summary>
    public Guid RequestId { get; }
    
    /// <summary>
    /// ID of the elevator assigned to handle the request
    /// </summary>
    public int ElevatorId { get; }
    
    /// <summary>
    /// Optional correlation ID to track related events
    /// </summary>
    public string? CorrelationId { get; }
}