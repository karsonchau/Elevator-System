using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;

namespace ElevatorSystem.Application.Interfaces;

/// <summary>
/// Interface for elevator movement logic and navigation services.
/// </summary>
public interface IElevatorMovementService
{
    /// <summary>
    /// Moves the elevator to the specified target floor.
    /// </summary>
    /// <param name="elevator">The elevator to move.</param>
    /// <param name="targetFloor">The floor to move to.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task MoveToFloorAsync(Elevator elevator, int targetFloor, CancellationToken cancellationToken);
    
    /// <summary>
    /// Determines the optimal direction for the elevator based on current requests.
    /// </summary>
    /// <param name="elevator">The elevator to analyze.</param>
    /// <param name="requests">Current requests for the elevator.</param>
    /// <returns>The optimal direction for the elevator.</returns>
    ElevatorDirection DetermineOptimalDirection(Elevator elevator, List<ElevatorRequest> requests);
    
    /// <summary>
    /// Finds the next floor in the current direction that needs service.
    /// </summary>
    /// <param name="elevator">The elevator to check.</param>
    /// <param name="floorsNeedingService">Collection of floors needing service.</param>
    /// <param name="direction">Current direction of travel.</param>
    /// <returns>The next floor to service, or null if none found.</returns>
    int? FindNextFloorInDirection(Elevator elevator, SortedSet<int> floorsNeedingService, ElevatorDirection direction);
}