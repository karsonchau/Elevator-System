using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ElevatorSystem.Application.Services;

public class ElevatorController : IElevatorController, IDisposable
{
    private readonly IElevatorRepository _elevatorRepository;
    private readonly IElevatorRequestRepository _requestRepository;
    private readonly ILogger<ElevatorController> _logger;
    private readonly IElevatorMovementService _movementService;
    private readonly IElevatorRequestManager _requestManager;
    private readonly ConcurrentDictionary<int, ConcurrentBag<ElevatorRequest>> _elevatorActiveRequests = new();
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, bool>> _elevatorFloorsNeedingService = new();
    private readonly object _requestAssignmentLock = new();
    private readonly ConcurrentDictionary<int, object> _elevatorProcessingLocks = new();
    private readonly Timer _cleanupTimer;
    private volatile bool _disposed = false;

    public ElevatorController(
        IElevatorRepository elevatorRepository,
        IElevatorRequestRepository requestRepository,
        ILogger<ElevatorController> logger,
        IElevatorMovementService movementService,
        IElevatorRequestManager requestManager)
    {
        _elevatorRepository = elevatorRepository;
        _requestRepository = requestRepository;
        _logger = logger;
        _movementService = movementService;
        _requestManager = requestManager;
        
        // Initialize cleanup timer - runs every 30 seconds
        _cleanupTimer = new Timer(PerformPeriodicCleanup, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task<bool> AddRequestAsync(ElevatorRequest request)
    {
        try
        {
            // Validate request against available elevators
            await ValidateRequestAsync(request);
            
            // Use lock to prevent race conditions in assignment
            lock (_requestAssignmentLock)
            {
                // Immediately assign request to best elevator
                var bestElevator = FindBestElevatorForRequestSync(request);
                
                InitializeElevatorCollections(bestElevator.Id);

                _elevatorActiveRequests[bestElevator.Id].Add(request);
                
                // Add pickup floor to floors needing service
                _elevatorFloorsNeedingService[bestElevator.Id].TryAdd(request.CurrentFloor, true);
                
                // Also add destination floor since we know it will be needed
                // This ensures proper floor tracking for the linear scan algorithm
                _elevatorFloorsNeedingService[bestElevator.Id].TryAdd(request.DestinationFloor, true);
                
                request.Status = ElevatorRequestStatus.Assigned;
                
                _logger.LogInformation("Added and immediately assigned request {RequestId} from floor {CurrentFloor} to {DestinationFloor} to elevator {ElevatorId}", 
                    request.Id, request.CurrentFloor, request.DestinationFloor, bestElevator.Id);
            }
            
            // Update repository outside lock to minimize lock time
            await _requestRepository.UpdateAsync(request);
            
            return true;
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError("Null request provided: {Error}", ex.Message);
            return false;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogError("Request validation failed for request {RequestId} (floors {CurrentFloor} to {DestinationFloor}): {Error}", 
                request?.Id, request?.CurrentFloor, request?.DestinationFloor, ex.Message);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError("Cannot process request {RequestId} (floors {CurrentFloor} to {DestinationFloor}): {Error}", 
                request?.Id, request?.CurrentFloor, request?.DestinationFloor, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding request {RequestId} (floors {CurrentFloor} to {DestinationFloor})", 
                request?.Id, request?.CurrentFloor, request?.DestinationFloor);
            
            // Set request to failed status if it was partially processed
            if (request?.Status == ElevatorRequestStatus.Assigned)
            {
                try
                {
                    request.Status = ElevatorRequestStatus.Failed;
                    await _requestRepository.UpdateAsync(request);
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Failed to update request status to failed for request {RequestId}", request.Id);
                }
            }
            
            return false;
        }
    }

    public async Task ProcessElevatorAsync(Elevator elevator, CancellationToken cancellationToken)
    {
        var processingLock = _elevatorProcessingLocks.GetOrAdd(elevator.Id, _ => new object());
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                InitializeElevatorCollections(elevator.Id);
                
                var activeRequests = GetActiveRequestsList(elevator.Id);
                if (!activeRequests.Any())
                {
                    elevator.SetStatus(ElevatorStatus.Idle);
                    elevator.SetDirection(ElevatorDirection.Idle);
                    await _elevatorRepository.UpdateAsync(elevator);
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                await ExecuteLinearScanMovement(elevator, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Elevator {ElevatorId} processing cancelled", elevator.Id);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing elevator {ElevatorId}, will retry in 5 seconds", elevator.Id);
                try
                {
                    await Task.Delay(5000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
    
    private async Task ExecuteLinearScanMovement(Elevator elevator, CancellationToken cancellationToken)
    {
        var requests = GetActiveRequestsList(elevator.Id);
        
        // Determine direction based on current requests and elevator state
        var direction = _movementService.DetermineOptimalDirection(elevator, requests);
        elevator.SetDirection(direction);
        
        // Move linearly in the chosen direction, stopping only at floors with pickup/dropoff
        while (!cancellationToken.IsCancellationRequested && requests.Any())
        {
            var currentRequests = GetActiveRequestsList(elevator.Id); // Get fresh list in case new requests were added
            
            // Check if we should stop at current floor
            if (_requestManager.ShouldStopAtCurrentFloor(elevator, currentRequests))
            {
                var floorsForStop = GetFloorsNeedingServiceSet(elevator.Id);
                await _requestManager.ProcessCurrentFloorActionsAsync(elevator, currentRequests, floorsForStop, cancellationToken);
                
                // Update the actual floors needing service from the modified set
                UpdateFloorsNeedingServiceFromSet(elevator.Id, floorsForStop);
                
                // Remove completed requests and update collections
                RemoveCompletedRequestsFromCollections(elevator.Id, currentRequests);
                requests = GetActiveRequestsList(elevator.Id);
                continue;
            }
            
            // Find next floor in current direction that needs service
            var floorsNeedingService = GetFloorsNeedingServiceSet(elevator.Id);
            var nextServiceFloor = _movementService.FindNextFloorInDirection(elevator, floorsNeedingService, direction);
            _logger.LogInformation("Elevator {ElevatorId} at floor {CurrentFloor} going {Direction}, next service floor: {NextFloor}", 
                elevator.Id, elevator.CurrentFloor, direction, nextServiceFloor?.ToString() ?? "none");
            
            if (!nextServiceFloor.HasValue)
            {
                // No more floors in this direction, reverse if needed
                if (currentRequests.Any())
                {
                    direction = direction == ElevatorDirection.Up ? ElevatorDirection.Down : ElevatorDirection.Up;
                    elevator.SetDirection(direction);
                    var updatedFloorsNeedingService = GetFloorsNeedingServiceSet(elevator.Id);
                    nextServiceFloor = _movementService.FindNextFloorInDirection(elevator, updatedFloorsNeedingService, direction);
                    
                    // If still no floor to service, check for any unserviced requests
                    if (!nextServiceFloor.HasValue)
                    {
                        // Find any unserviced request and move towards it
                        var anyRequest = currentRequests.FirstOrDefault(r => r.Status == ElevatorRequestStatus.Assigned);
                        if (anyRequest != null)
                        {
                            nextServiceFloor = anyRequest.CurrentFloor;
                            direction = nextServiceFloor.Value > elevator.CurrentFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
                            elevator.SetDirection(direction);
                        }
                    }
                }
            }
            
            if (!nextServiceFloor.HasValue)
            {
                // No more requests to service
                break;
            }
            
            // Move to next service floor
            await _movementService.MoveToFloorAsync(elevator, nextServiceFloor.Value, cancellationToken);
            requests = GetActiveRequestsList(elevator.Id); // Refresh after movement
        }
    }

    private Elevator FindBestElevatorForRequestSync(ElevatorRequest request)
    {
        var elevators = _elevatorRepository.GetAllAsync().GetAwaiter().GetResult();
        
        var bestElevator = elevators
            .Where(e => e.Status != ElevatorStatus.OutOfService)
            .OrderBy(e => Math.Abs(e.CurrentFloor - request.CurrentFloor))
            .FirstOrDefault();
            
        if (bestElevator == null)
            throw new InvalidOperationException("No available elevator found for request");
            
        return bestElevator;
    }

    private async Task ValidateRequestAsync(ElevatorRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var elevators = await _elevatorRepository.GetAllAsync();
        var availableElevators = elevators.Where(e => e.Status != ElevatorStatus.OutOfService).ToList();
        
        if (!availableElevators.Any())
            throw new InvalidOperationException("No elevators are currently available for service.");
        
        // Get the overall building floor range from all elevators
        var minFloor = availableElevators.Min(e => e.MinFloor);
        var maxFloor = availableElevators.Max(e => e.MaxFloor);
        
        if (request.CurrentFloor < minFloor || request.CurrentFloor > maxFloor)
            throw new ArgumentOutOfRangeException(nameof(request), 
                $"Current floor {request.CurrentFloor} is outside the valid range ({minFloor} to {maxFloor}).");
                
        if (request.DestinationFloor < minFloor || request.DestinationFloor > maxFloor)
            throw new ArgumentOutOfRangeException(nameof(request), 
                $"Destination floor {request.DestinationFloor} is outside the valid range ({minFloor} to {maxFloor}).");
        
        // Check if there's at least one elevator that can serve both floors
        var canServeRequest = availableElevators.Any(e => 
            request.CurrentFloor >= e.MinFloor && request.CurrentFloor <= e.MaxFloor &&
            request.DestinationFloor >= e.MinFloor && request.DestinationFloor <= e.MaxFloor);
            
        if (!canServeRequest)
            throw new InvalidOperationException(
                $"No available elevator can serve a request from floor {request.CurrentFloor} to floor {request.DestinationFloor}.");
    }

    private void InitializeElevatorCollections(int elevatorId)
    {
        _elevatorActiveRequests.GetOrAdd(elevatorId, _ => new ConcurrentBag<ElevatorRequest>());
        _elevatorFloorsNeedingService.GetOrAdd(elevatorId, _ => new ConcurrentDictionary<int, bool>());
    }
    
    private List<ElevatorRequest> GetActiveRequestsList(int elevatorId)
    {
        InitializeElevatorCollections(elevatorId);
        return _elevatorActiveRequests[elevatorId].Where(r => r.Status != ElevatorRequestStatus.Completed).ToList();
    }
    
    private SortedSet<int> GetFloorsNeedingServiceSet(int elevatorId)
    {
        InitializeElevatorCollections(elevatorId);
        return new SortedSet<int>(_elevatorFloorsNeedingService[elevatorId].Keys);
    }
    
    private void UpdateFloorsNeedingServiceFromSet(int elevatorId, SortedSet<int> updatedFloors)
    {
        InitializeElevatorCollections(elevatorId);
        var floorsDict = _elevatorFloorsNeedingService[elevatorId];
        
        // Add new floors that were added to the set
        foreach (var floor in updatedFloors)
        {
            floorsDict.TryAdd(floor, true);
        }
    }
    
    private void RemoveCompletedRequestsFromCollections(int elevatorId, List<ElevatorRequest> currentRequests)
    {
        var completedRequests = currentRequests.Where(r => r.Status == ElevatorRequestStatus.Completed).ToList();
        
        // Remove completed floors from floors needing service
        foreach (var completedRequest in completedRequests)
        {
            // Remove pickup floor if no other requests need it
            var otherRequestsNeedingPickup = currentRequests
                .Where(r => r.Status != ElevatorRequestStatus.Completed && r.CurrentFloor == completedRequest.CurrentFloor)
                .Any();
            if (!otherRequestsNeedingPickup)
            {
                _elevatorFloorsNeedingService[elevatorId].TryRemove(completedRequest.CurrentFloor, out _);
            }
            
            // Remove destination floor if no other requests need it
            var otherRequestsNeedingDropoff = currentRequests
                .Where(r => r.Status != ElevatorRequestStatus.Completed && r.DestinationFloor == completedRequest.DestinationFloor)
                .Any();
            if (!otherRequestsNeedingDropoff)
            {
                _elevatorFloorsNeedingService[elevatorId].TryRemove(completedRequest.DestinationFloor, out _);
            }
        }
        
        // Note: ConcurrentBag doesn't support removal, but we filter out completed requests in GetActiveRequestsList
        // For production, consider using a different collection or implementing a cleanup mechanism
    }
    
    // Timer callback for periodic cleanup
    private void PerformPeriodicCleanup(object? state)
    {
        if (_disposed) return;
        
        try
        {
            CleanupCompletedRequests();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic cleanup");
        }
    }
    
    // Memory cleanup method - called internally
    private void CleanupCompletedRequests()
    {
        foreach (var elevatorId in _elevatorActiveRequests.Keys.ToList())
        {
            var currentRequests = _elevatorActiveRequests[elevatorId];
            var activeRequests = currentRequests.Where(r => r.Status != ElevatorRequestStatus.Completed).ToList();
            
            if (activeRequests.Count != currentRequests.Count())
            {
                // Replace the bag with a new one containing only active requests
                _elevatorActiveRequests.TryUpdate(elevatorId, new ConcurrentBag<ElevatorRequest>(activeRequests), currentRequests);
                
                _logger.LogDebug("Cleaned up completed requests for elevator {ElevatorId}. Active requests: {ActiveCount}", 
                    elevatorId, activeRequests.Count);
            }
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cleanupTimer?.Dispose();
            _logger.LogInformation("ElevatorController disposed");
        }
    }
}