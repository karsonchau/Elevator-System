using ElevatorSystem.Domain.Enums;

namespace ElevatorSystem.Domain.Entities;

/// <summary>
/// Represents a request for elevator service from one floor to another.
/// </summary>
public class ElevatorRequest
{
    /// <summary>
    /// Gets the unique identifier for this elevator request.
    /// </summary>
    public Guid Id { get; }
    
    /// <summary>
    /// Gets the floor where the passenger is currently located.
    /// </summary>
    public int CurrentFloor { get; }
    
    /// <summary>
    /// Gets the floor where the passenger wants to go.
    /// </summary>
    public int DestinationFloor { get; }
    
    /// <summary>
    /// Gets the timestamp when this request was created.
    /// </summary>
    public DateTime RequestTime { get; }
    
    /// <summary>
    /// Gets or sets the current status of this elevator request.
    /// </summary>
    public ElevatorRequestStatus Status { get; set; }

    /// <summary>
    /// Initializes a new instance of the ElevatorRequest class.
    /// </summary>
    /// <param name="currentFloor">The floor where the passenger is currently located.</param>
    /// <param name="destinationFloor">The floor where the passenger wants to go.</param>
    /// <exception cref="ArgumentException">Thrown when currentFloor equals destinationFloor.</exception>
    public ElevatorRequest(int currentFloor, int destinationFloor)
    {
        if (currentFloor == destinationFloor)
            throw new ArgumentException("Current floor cannot be the same as destination floor.", nameof(destinationFloor));
            
        Id = Guid.NewGuid();
        CurrentFloor = currentFloor;
        DestinationFloor = destinationFloor;
        RequestTime = DateTime.UtcNow;
        Status = ElevatorRequestStatus.Pending;
    }

    /// <summary>
    /// Gets the direction of travel required for this request.
    /// </summary>
    public ElevatorDirection Direction => 
        DestinationFloor > CurrentFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
}