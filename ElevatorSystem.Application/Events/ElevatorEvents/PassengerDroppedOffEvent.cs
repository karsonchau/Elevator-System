namespace ElevatorSystem.Application.Events.ElevatorEvents;

/// <summary>
/// Event published when a passenger is dropped off by an elevator
/// </summary>
[Message("passenger.dropped_off", persistent: true)]
public class PassengerDroppedOffEvent : BaseEvent
{
    public PassengerDroppedOffEvent(Guid requestId, int elevatorId, int floor)
    {
        RequestId = requestId;
        ElevatorId = elevatorId;
        Floor = floor;
    }

    /// <summary>
    /// Unique identifier of the completed request
    /// </summary>
    public Guid RequestId { get; }
    
    /// <summary>
    /// ID of the elevator that dropped off the passenger
    /// </summary>
    public int ElevatorId { get; }
    
    /// <summary>
    /// Floor where the passenger was dropped off
    /// </summary>
    public int Floor { get; }
}