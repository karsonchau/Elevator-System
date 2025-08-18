using ElevatorSystem.Domain.Enums;

namespace ElevatorSystem.Domain.Entities;

public class ElevatorRequest
{
    public Guid Id { get; }
    public int CurrentFloor { get; }
    public int DestinationFloor { get; }
    public DateTime RequestTime { get; }
    public ElevatorRequestStatus Status { get; set; }

    public ElevatorRequest(int currentFloor, int destinationFloor)
    {
        Id = Guid.NewGuid();
        CurrentFloor = currentFloor;
        DestinationFloor = destinationFloor;
        RequestTime = DateTime.UtcNow;
        Status = ElevatorRequestStatus.Pending;
    }

    public ElevatorDirection Direction => 
        DestinationFloor > CurrentFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
}