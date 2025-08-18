using ElevatorSystem.Domain.Enums;

namespace ElevatorSystem.Domain.Entities;

/// <summary>
/// Represents an elevator in the building with its current state and operational capabilities.
/// </summary>
public class Elevator
{
    /// <summary>
    /// Gets the unique identifier for this elevator.
    /// </summary>
    public int Id { get; }
    
    /// <summary>
    /// Gets the current floor where the elevator is located.
    /// </summary>
    public int CurrentFloor { get; private set; }
    
    /// <summary>
    /// Gets the current direction of elevator movement.
    /// </summary>
    public ElevatorDirection Direction { get; private set; }
    
    /// <summary>
    /// Gets the current operational status of the elevator.
    /// </summary>
    public ElevatorStatus Status { get; private set; }
    
    /// <summary>
    /// Gets the highest floor this elevator can reach.
    /// </summary>
    public int MaxFloor { get; }
    
    /// <summary>
    /// Gets the lowest floor this elevator can reach (can be negative for basements).
    /// </summary>
    public int MinFloor { get; }
    
    /// <summary>
    /// Gets the time in milliseconds it takes to move between adjacent floors.
    /// </summary>
    public int FloorMovementTimeMs { get; }
    
    /// <summary>
    /// Gets the time in milliseconds required for loading/unloading passengers.
    /// </summary>
    public int LoadingTimeMs { get; }

    /// <summary>
    /// Initializes a new instance of the Elevator class.
    /// </summary>
    /// <param name="id">The unique identifier for this elevator. Must be positive.</param>
    /// <param name="minFloor">The lowest floor this elevator can reach. Can be negative for basements.</param>
    /// <param name="maxFloor">The highest floor this elevator can reach. Must be greater than minFloor.</param>
    /// <param name="floorMovementTimeMs">The time in milliseconds to move between adjacent floors. Must be positive.</param>
    /// <param name="loadingTimeMs">The time in milliseconds for loading/unloading. Must be positive.</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public Elevator(int id, int minFloor = 1, int maxFloor = 10, int floorMovementTimeMs = 1000, int loadingTimeMs = 1000)
    {
        ValidateConstructorParameters(id, minFloor, maxFloor, floorMovementTimeMs, loadingTimeMs);
        
        Id = id;
        CurrentFloor = minFloor;
        Direction = ElevatorDirection.Idle;
        Status = ElevatorStatus.Idle;
        MinFloor = minFloor;
        MaxFloor = maxFloor;
        FloorMovementTimeMs = floorMovementTimeMs;
        LoadingTimeMs = loadingTimeMs;
    }

    /// <summary>
    /// Moves the elevator to the specified floor.
    /// </summary>
    /// <param name="floor">The target floor number.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the floor is outside the elevator's operating range.</exception>
    public void MoveTo(int floor)
    {
        if (floor < MinFloor || floor > MaxFloor)
            throw new ArgumentOutOfRangeException(nameof(floor), $"Floor must be between {MinFloor} and {MaxFloor}");

        CurrentFloor = floor;
    }

    /// <summary>
    /// Sets the direction of elevator movement.
    /// </summary>
    /// <param name="direction">The new direction for the elevator.</param>
    public void SetDirection(ElevatorDirection direction)
    {
        Direction = direction;
    }

    /// <summary>
    /// Sets the operational status of the elevator.
    /// </summary>
    /// <param name="status">The new status for the elevator.</param>
    public void SetStatus(ElevatorStatus status)
    {
        Status = status;
    }

    /// <summary>
    /// Calculates the distance in floors between the elevator's current position and the target floor.
    /// </summary>
    /// <param name="floor">The target floor number.</param>
    /// <returns>The absolute distance in floors.</returns>
    public int GetDistanceToFloor(int floor)
    {
        return Math.Abs(CurrentFloor - floor);
    }

    /// <summary>
    /// Determines if the elevator can serve the specified floor.
    /// </summary>
    /// <param name="floor">The floor to check.</param>
    /// <returns>True if the elevator can reach the floor, false otherwise.</returns>
    public bool CanServeFloor(int floor)
    {
        return floor >= MinFloor && floor <= MaxFloor;
    }

    /// <summary>
    /// Validates the constructor parameters to ensure they meet business rules.
    /// </summary>
    /// <param name="id">The elevator ID.</param>
    /// <param name="minFloor">The minimum floor.</param>
    /// <param name="maxFloor">The maximum floor.</param>
    /// <param name="floorMovementTimeMs">The floor movement time.</param>
    /// <param name="loadingTimeMs">The loading time.</param>
    /// <exception cref="ArgumentException">Thrown when any parameter is invalid.</exception>
    private static void ValidateConstructorParameters(int id, int minFloor, int maxFloor, int floorMovementTimeMs, int loadingTimeMs)
    {
        if (id <= 0)
            throw new ArgumentException("Elevator ID must be positive.", nameof(id));

        if (maxFloor <= minFloor)
            throw new ArgumentException("Maximum floor must be greater than minimum floor.", nameof(maxFloor));

        if (floorMovementTimeMs <= 0)
            throw new ArgumentException("Floor movement time must be positive.", nameof(floorMovementTimeMs));

        if (loadingTimeMs <= 0)
            throw new ArgumentException("Loading time must be positive.", nameof(loadingTimeMs));
    }
}