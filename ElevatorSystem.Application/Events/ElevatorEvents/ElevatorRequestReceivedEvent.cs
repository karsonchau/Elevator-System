using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Events.ElevatorEvents;

/// <summary>
/// Event published when a new elevator request is received
/// </summary>
public class ElevatorRequestReceivedEvent : BaseEvent
{
    public ElevatorRequestReceivedEvent(ElevatorRequest request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        RequestId = request.Id;
        CurrentFloor = request.CurrentFloor;
        DestinationFloor = request.DestinationFloor;
    }

    /// <summary>
    /// The full elevator request object
    /// </summary>
    public ElevatorRequest Request { get; }
    
    /// <summary>
    /// Unique identifier of the request
    /// </summary>
    public Guid RequestId { get; }
    
    /// <summary>
    /// Floor where passenger is waiting
    /// </summary>
    public int CurrentFloor { get; }
    
    /// <summary>
    /// Floor where passenger wants to go
    /// </summary>
    public int DestinationFloor { get; }
}