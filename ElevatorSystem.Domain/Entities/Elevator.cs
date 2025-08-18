using ElevatorSystem.Domain.Enums;

namespace ElevatorSystem.Domain.Entities;

public class Elevator
{
    public int Id { get; }
    public int CurrentFloor { get; private set; }
    public ElevatorDirection Direction { get; private set; }
    public ElevatorStatus Status { get; private set; }
    public int MaxFloor { get; }
    public int MinFloor { get; }
    public int FloorMovementTimeMs { get; }
    public int LoadingTimeMs { get; }

    public Elevator(int id, int minFloor = 1, int maxFloor = 10, int floorMovementTimeMs = 1000, int loadingTimeMs = 1000)
    {
        Id = id;
        CurrentFloor = minFloor;
        Direction = ElevatorDirection.Idle;
        Status = ElevatorStatus.Idle;
        MinFloor = minFloor;
        MaxFloor = maxFloor;
        FloorMovementTimeMs = floorMovementTimeMs;
        LoadingTimeMs = loadingTimeMs;
    }

    public void MoveTo(int floor)
    {
        if (floor < MinFloor || floor > MaxFloor)
            throw new ArgumentOutOfRangeException(nameof(floor), $"Floor must be between {MinFloor} and {MaxFloor}");

        CurrentFloor = floor;
    }

    public void SetDirection(ElevatorDirection direction)
    {
        Direction = direction;
    }

    public void SetStatus(ElevatorStatus status)
    {
        Status = status;
    }

    public int GetDistanceToFloor(int floor)
    {
        return Math.Abs(CurrentFloor - floor);
    }
}