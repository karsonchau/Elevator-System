using ElevatorSystem.Domain.Enums;

namespace ElevatorSystem.Application.Events.ElevatorEvents;

/// <summary>
/// Event published when an elevator moves to a new floor
/// </summary>
public class ElevatorMovedEvent : BaseEvent
{
    public ElevatorMovedEvent(int elevatorId, int fromFloor, int toFloor, ElevatorDirection direction)
    {
        ElevatorId = elevatorId;
        FromFloor = fromFloor;
        ToFloor = toFloor;
        Direction = direction;
    }

    /// <summary>
    /// ID of the elevator that moved
    /// </summary>
    public int ElevatorId { get; }
    
    /// <summary>
    /// Floor the elevator moved from
    /// </summary>
    public int FromFloor { get; }
    
    /// <summary>
    /// Floor the elevator moved to
    /// </summary>
    public int ToFloor { get; }
    
    /// <summary>
    /// Direction of elevator movement
    /// </summary>
    public ElevatorDirection Direction { get; }
}