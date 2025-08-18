using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Services;

/// <summary>
/// Service responsible for managing elevator requests and passenger operations.
/// </summary>
public class ElevatorRequestManager : IElevatorRequestManager
{
    private readonly IElevatorRequestRepository _requestRepository;
    private readonly ILogger<ElevatorRequestManager> _logger;

    public ElevatorRequestManager(
        IElevatorRequestRepository requestRepository,
        ILogger<ElevatorRequestManager> logger)
    {
        _requestRepository = requestRepository;
        _logger = logger;
    }

    /// <summary>
    /// Processes passenger pickup and dropoff actions at the current floor.
    /// </summary>
    /// <param name="elevator">The elevator at the current floor.</param>
    /// <param name="requests">List of requests for this elevator.</param>
    /// <param name="floorsNeedingService">Collection tracking floors needing service.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task ProcessCurrentFloorActionsAsync(Elevator elevator, List<ElevatorRequest> requests, 
        SortedSet<int> floorsNeedingService, CancellationToken cancellationToken = default)
    {
        // Constants moved to retry method
        
        elevator.SetStatus(ElevatorStatus.Loading);
        
        var currentFloor = elevator.CurrentFloor;
        var hasActions = false;
        
        // Process dropoffs first
        var dropoffRequests = requests.Where(r => r.Status == ElevatorRequestStatus.InProgress && 
                                                  r.DestinationFloor == currentFloor).ToList();
        
        foreach (var request in dropoffRequests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogInformation("Floor {Floor}: Dropoff passenger", currentFloor);
            request.Status = ElevatorRequestStatus.Completed;
            
            // Retry repository update with exponential backoff
            await RetryRepositoryUpdateAsync(() => _requestRepository.UpdateAsync(request), 
                request.Id, "dropoff completion", cancellationToken);
            
            hasActions = true;
        }
        
        // Process pickups second - only passengers going in same direction and valid timing
        var pickupRequests = requests.Where(r => r.Status == ElevatorRequestStatus.Assigned && 
                                                 r.CurrentFloor == currentFloor &&
                                                 IsPassengerGoingInSameDirection(r, elevator.Direction) &&
                                                 CanPickupPassenger(elevator, r, elevator.Direction)).ToList();
        
        foreach (var request in pickupRequests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogInformation("Floor {Floor}: Pickup passenger going to {DestinationFloor}", 
                currentFloor, request.DestinationFloor);
            request.Status = ElevatorRequestStatus.InProgress;
            
            // Retry repository update with exponential backoff
            await RetryRepositoryUpdateAsync(() => _requestRepository.UpdateAsync(request), 
                request.Id, "pickup assignment", cancellationToken);
            
            // Add destination floor to floors needing service
            floorsNeedingService.Add(request.DestinationFloor);
                
            hasActions = true;
        }
        
        if (hasActions)
        {
            try
            {
                await Task.Delay(elevator.LoadingTimeMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Loading operation cancelled for elevator {ElevatorId} at floor {Floor}", 
                    elevator.Id, currentFloor);
                throw;
            }
        }
    }
    
    private async Task RetryRepositoryUpdateAsync(Func<Task> updateAction, Guid requestId, string operation, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 100;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await updateAction();
                return;
            }
            catch (Exception ex) when (attempt < maxRetries - 1 && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Failed to update {Operation} for request {RequestId}, attempt {Attempt}/{MaxRetries}", 
                    operation, requestId, attempt + 1, maxRetries);
                
                await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt), cancellationToken);
            }
        }
        
        // Final attempt - let exception bubble up
        await updateAction();
    }

    /// <summary>
    /// Checks if the elevator should stop at its current floor.
    /// </summary>
    /// <param name="elevator">The elevator to check.</param>
    /// <param name="requests">List of requests for this elevator.</param>
    /// <returns>True if the elevator should stop at the current floor.</returns>
    public bool ShouldStopAtCurrentFloor(Elevator elevator, List<ElevatorRequest> requests)
    {
        // Check if any requests need pickup at current floor - only if going in same direction and timing is valid
        var needsPickupHere = requests.Any(r => r.Status == ElevatorRequestStatus.Assigned && 
                                                r.CurrentFloor == elevator.CurrentFloor &&
                                                IsPassengerGoingInSameDirection(r, elevator.Direction) &&
                                                CanPickupPassenger(elevator, r, elevator.Direction));
        
        // Check if any passengers need dropoff at current floor
        var needsDropoffHere = requests.Any(r => r.Status == ElevatorRequestStatus.InProgress && r.DestinationFloor == elevator.CurrentFloor);
        
        return needsPickupHere || needsDropoffHere;
    }

    /// <summary>
    /// Removes completed requests from active collections.
    /// </summary>
    /// <param name="requests">List of active requests.</param>
    /// <param name="floorsNeedingService">Collection of floors needing service.</param>
    public void RemoveCompletedRequests(List<ElevatorRequest> requests, SortedSet<int> floorsNeedingService)
    {
        var completedRequests = requests
            .Where(r => r.Status == ElevatorRequestStatus.Completed)
            .ToList();
            
        foreach (var request in completedRequests)
        {
            // Remove from active requests
            requests.Remove(request);
            
            // Only remove floor if no other requests need it
            if (!HasOtherRequestsForFloor(requests, request.CurrentFloor))
            {
                floorsNeedingService.Remove(request.CurrentFloor);
            }
            
            if (!HasOtherRequestsForFloor(requests, request.DestinationFloor))
            {
                floorsNeedingService.Remove(request.DestinationFloor);
            }
        }
    }

    private bool IsPassengerGoingInSameDirection(ElevatorRequest request, ElevatorDirection elevatorDirection)
    {
        var passengerDirection = request.DestinationFloor > request.CurrentFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
        return passengerDirection == elevatorDirection;
    }

    private bool CanPickupPassenger(Elevator elevator, ElevatorRequest request, ElevatorDirection elevatorDirection)
    {
        // If elevator is going up, can only pickup passengers at current floor or above
        if (elevatorDirection == ElevatorDirection.Up)
        {
            return request.CurrentFloor >= elevator.CurrentFloor;
        }
        // If elevator is going down, can only pickup passengers at current floor or below  
        else
        {
            return request.CurrentFloor <= elevator.CurrentFloor;
        }
    }

    private bool HasOtherRequestsForFloor(List<ElevatorRequest> requests, int floor)
    {
        return requests.Any(r => 
            r.Status != ElevatorRequestStatus.Completed && 
            (r.CurrentFloor == floor || r.DestinationFloor == floor));
    }
}