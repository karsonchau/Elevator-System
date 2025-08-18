using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Services;

/// <summary>
/// Service responsible for handling elevator movement logic and navigation.
/// </summary>
public class ElevatorMovementService : IElevatorMovementService
{
    private readonly IElevatorRepository _elevatorRepository;
    private readonly ILogger<ElevatorMovementService> _logger;

    public ElevatorMovementService(
        IElevatorRepository elevatorRepository,
        ILogger<ElevatorMovementService> logger)
    {
        _elevatorRepository = elevatorRepository;
        _logger = logger;
    }

    /// <summary>
    /// Moves the elevator to the specified target floor.
    /// </summary>
    /// <param name="elevator">The elevator to move.</param>
    /// <param name="targetFloor">The floor to move to.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task MoveToFloorAsync(Elevator elevator, int targetFloor, CancellationToken cancellationToken)
    {
        elevator.SetStatus(ElevatorStatus.Moving);
        await _elevatorRepository.UpdateAsync(elevator);
        
        while (elevator.CurrentFloor != targetFloor && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(elevator.FloorMovementTimeMs, cancellationToken);
            
            var nextFloor = elevator.Direction == ElevatorDirection.Up 
                ? elevator.CurrentFloor + 1 
                : elevator.CurrentFloor - 1;
            
            elevator.MoveTo(nextFloor);
            await _elevatorRepository.UpdateAsync(elevator);
        }
    }

    /// <summary>
    /// Determines the optimal direction for the elevator based on current requests.
    /// </summary>
    /// <param name="elevator">The elevator to analyze.</param>
    /// <param name="requests">Current requests for the elevator.</param>
    /// <returns>The optimal direction for the elevator.</returns>
    public ElevatorDirection DetermineOptimalDirection(Elevator elevator, List<ElevatorRequest> requests)
    {
        // If elevator is idle, determine direction based on closest request
        if (elevator.Direction == ElevatorDirection.Idle)
        {
            var closestRequest = requests.OrderBy(r => Math.Abs(r.CurrentFloor - elevator.CurrentFloor)).FirstOrDefault();
            if (closestRequest != null)
            {
                return closestRequest.CurrentFloor >= elevator.CurrentFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
            }
        }
        
        return elevator.Direction;
    }

    /// <summary>
    /// Finds the next floor in the current direction that needs service.
    /// </summary>
    /// <param name="elevator">The elevator to check.</param>
    /// <param name="floorsNeedingService">Collection of floors needing service.</param>
    /// <param name="direction">Current direction of travel.</param>
    /// <returns>The next floor to service, or null if none found.</returns>
    public int? FindNextFloorInDirection(Elevator elevator, SortedSet<int> floorsNeedingService, ElevatorDirection direction)
    {
        var currentFloor = elevator.CurrentFloor;
        
        if (direction == ElevatorDirection.Up)
        {
            // Get next floor > current floor (exclude current floor to avoid infinite loop)
            var candidateFloors = floorsNeedingService.Where(f => f > currentFloor);
            return candidateFloors.Any() ? candidateFloors.First() : null;
        }
        else
        {
            // Get next floor < current floor (in descending order, exclude current floor)
            var candidateFloors = floorsNeedingService.Where(f => f < currentFloor);
            return candidateFloors.Any() ? candidateFloors.Last() : null;
        }
    }
}