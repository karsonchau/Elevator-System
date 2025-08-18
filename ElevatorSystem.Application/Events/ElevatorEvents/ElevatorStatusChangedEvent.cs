using ElevatorSystem.Domain.Enums;

namespace ElevatorSystem.Application.Events.ElevatorEvents;

/// <summary>
/// Event published when an elevator's status changes
/// </summary>
public class ElevatorStatusChangedEvent : BaseEvent
{
    public ElevatorStatusChangedEvent(int elevatorId, ElevatorStatus previousStatus, ElevatorStatus newStatus, int currentFloor)
    {
        ElevatorId = elevatorId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        CurrentFloor = currentFloor;
    }

    /// <summary>
    /// ID of the elevator whose status changed
    /// </summary>
    public int ElevatorId { get; }
    
    /// <summary>
    /// Previous status of the elevator
    /// </summary>
    public ElevatorStatus PreviousStatus { get; }
    
    /// <summary>
    /// New status of the elevator
    /// </summary>
    public ElevatorStatus NewStatus { get; }
    
    /// <summary>
    /// Current floor of the elevator
    /// </summary>
    public int CurrentFloor { get; }
}