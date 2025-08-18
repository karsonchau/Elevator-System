namespace ElevatorSystem.Application.Events.ElevatorEvents;

/// <summary>
/// Event published when a passenger is picked up by an elevator
/// </summary>
[Message("passenger.picked_up", persistent: true)]
public class PassengerPickedUpEvent : BaseEvent
{
    public PassengerPickedUpEvent(Guid requestId, int elevatorId, int floor, int destinationFloor)
    {
        RequestId = requestId;
        ElevatorId = elevatorId;
        Floor = floor;
        DestinationFloor = destinationFloor;
    }

    /// <summary>
    /// Unique identifier of the request
    /// </summary>
    public Guid RequestId { get; }
    
    /// <summary>
    /// ID of the elevator that picked up the passenger
    /// </summary>
    public int ElevatorId { get; }
    
    /// <summary>
    /// Floor where the passenger was picked up
    /// </summary>
    public int Floor { get; }
    
    /// <summary>
    /// Floor where the passenger wants to go
    /// </summary>
    public int DestinationFloor { get; }
}