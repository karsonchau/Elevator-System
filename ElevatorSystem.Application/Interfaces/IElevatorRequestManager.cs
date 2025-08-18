using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Interfaces;

/// <summary>
/// Interface for elevator request management and passenger operations.
/// </summary>
public interface IElevatorRequestManager
{
    /// <summary>
    /// Determines if the elevator should stop at its current floor based on pending requests.
    /// </summary>
    /// <param name="elevator">The elevator to check.</param>
    /// <param name="requests">Current requests for the elevator.</param>
    /// <returns>True if the elevator should stop at the current floor.</returns>
    bool ShouldStopAtCurrentFloor(Elevator elevator, List<ElevatorRequest> requests);
    
    /// <summary>
    /// Processes pickup and dropoff actions at the current floor.
    /// </summary>
    /// <param name="elevator">The elevator at the current floor.</param>
    /// <param name="requests">Current requests for the elevator.</param>
    /// <param name="floorsNeedingService">Collection of floors that need service.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task ProcessCurrentFloorActionsAsync(Elevator elevator, List<ElevatorRequest> requests, 
        SortedSet<int> floorsNeedingService, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes completed requests from the active request list and updates floors needing service.
    /// </summary>
    /// <param name="requests">The list of requests to clean up.</param>
    /// <param name="floorsNeedingService">Collection of floors that need service.</param>
    void RemoveCompletedRequests(List<ElevatorRequest> requests, SortedSet<int> floorsNeedingService);
}