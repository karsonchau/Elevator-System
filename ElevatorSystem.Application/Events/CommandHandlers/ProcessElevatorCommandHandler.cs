using ElevatorSystem.Application.Events.ElevatorCommands;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ElevatorSystem.Application.Events.CommandHandlers;

/// <summary>
/// Command handler for processing elevator operations
/// </summary>
[CommandHandler("elevator-processing", priority: 2, requiresOrdering: true)]
public class ProcessElevatorCommandHandler : BaseCommandHandler<ProcessElevatorCommand>
{
    private readonly IElevatorRepository _elevatorRepository;
    private readonly IElevatorMovementService _movementService;
    private readonly IElevatorRequestManager _requestManager;
    private readonly ConcurrentDictionary<int, ConcurrentBag<ElevatorRequest>> _elevatorActiveRequests;
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, bool>> _elevatorFloorsNeedingService;
    private readonly ConcurrentDictionary<int, object> _elevatorProcessingLocks;

    public ProcessElevatorCommandHandler(
        IElevatorRepository elevatorRepository,
        IElevatorMovementService movementService,
        IElevatorRequestManager requestManager,
        ConcurrentDictionary<int, ConcurrentBag<ElevatorRequest>> elevatorActiveRequests,
        ConcurrentDictionary<int, ConcurrentDictionary<int, bool>> elevatorFloorsNeedingService,
        ConcurrentDictionary<int, object> elevatorProcessingLocks,
        ILogger<ProcessElevatorCommandHandler> logger) : base(logger)
    {
        _elevatorRepository = elevatorRepository;
        _movementService = movementService;
        _requestManager = requestManager;
        _elevatorActiveRequests = elevatorActiveRequests;
        _elevatorFloorsNeedingService = elevatorFloorsNeedingService;
        _elevatorProcessingLocks = elevatorProcessingLocks;
    }

    protected override async Task ExecuteAsync(ProcessElevatorCommand command, CancellationToken cancellationToken)
    {
        var elevator = command.Elevator;
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

                // Check for and clean up invalid requests before processing
                var invalidRequests = activeRequests.Where(r => 
                    r.Status == ElevatorRequestStatus.Assigned &&
                    (!elevator.CanServeFloor(r.CurrentFloor) || !elevator.CanServeFloor(r.DestinationFloor))).ToList();
                    
                if (invalidRequests.Any())
                {
                    Logger.LogWarning("Found {InvalidCount} invalid requests for elevator {ElevatorId} before processing. Cleaning up to prevent infinite loops.", 
                        invalidRequests.Count, elevator.Id);
                        
                    foreach (var invalidRequest in invalidRequests)
                    {
                        Logger.LogWarning("Removing invalid request: elevator {ElevatorId} cannot serve floors {CurrentFloor} to {DestinationFloor}. Valid range: {MinFloor}-{MaxFloor}",
                            elevator.Id, invalidRequest.CurrentFloor, invalidRequest.DestinationFloor, elevator.MinFloor, elevator.MaxFloor);
                        invalidRequest.Status = ElevatorRequestStatus.Completed;
                    }
                    
                    // Remove invalid requests from collections
                    RemoveCompletedRequestsFromCollections(elevator.Id, activeRequests);
                    
                    // Check again if we have any valid requests left
                    activeRequests = GetActiveRequestsList(elevator.Id);
                    if (!activeRequests.Any())
                    {
                        Logger.LogInformation("No valid requests remaining for elevator {ElevatorId} after cleanup", elevator.Id);
                        elevator.SetStatus(ElevatorStatus.Idle);
                        elevator.SetDirection(ElevatorDirection.Idle);
                        await _elevatorRepository.UpdateAsync(elevator);
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }
                }

                await ExecuteLinearScanMovement(elevator, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Elevator {ElevatorId} processing cancelled", elevator.Id);
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing elevator {ElevatorId}, will retry in 5 seconds", elevator.Id);
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
            Logger.LogInformation("Elevator {ElevatorId} at floor {CurrentFloor} going {Direction}, next service floor: {NextFloor}", 
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
                        // Find any unserviced request that the elevator can actually serve
                        var validRequest = currentRequests.FirstOrDefault(r => 
                            r.Status == ElevatorRequestStatus.Assigned &&
                            elevator.CanServeFloor(r.CurrentFloor) &&
                            elevator.CanServeFloor(r.DestinationFloor));
                            
                        if (validRequest != null)
                        {
                            nextServiceFloor = validRequest.CurrentFloor;
                            direction = nextServiceFloor.Value > elevator.CurrentFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
                            elevator.SetDirection(direction);
                        }
                        else
                        {
                            // Remove invalid requests to prevent infinite loop
                            var invalidRequests = currentRequests.Where(r => 
                                r.Status == ElevatorRequestStatus.Assigned &&
                                (!elevator.CanServeFloor(r.CurrentFloor) || !elevator.CanServeFloor(r.DestinationFloor))).ToList();
                                
                            if (invalidRequests.Any())
                            {
                                foreach (var invalidRequest in invalidRequests)
                                {
                                    Logger.LogWarning("Removing invalid request: elevator {ElevatorId} cannot serve floors {CurrentFloor} to {DestinationFloor}. Valid range: {MinFloor}-{MaxFloor}",
                                        elevator.Id, invalidRequest.CurrentFloor, invalidRequest.DestinationFloor, elevator.MinFloor, elevator.MaxFloor);
                                    invalidRequest.Status = ElevatorRequestStatus.Completed;
                                }
                                
                                // Remove invalid requests from collections
                                RemoveCompletedRequestsFromCollections(elevator.Id, currentRequests);
                                
                                // Refresh requests after cleanup to check if we should continue
                                requests = GetActiveRequestsList(elevator.Id);
                            }
                            
                            // If no valid requests remain or no invalid requests were found, exit the loop
                            if (!requests.Any())
                            {
                                Logger.LogInformation("No more valid requests for elevator {ElevatorId}, exiting processing loop", elevator.Id);
                                break;
                            }
                            
                            // If we still have requests but no floors to service, this might indicate a sync issue
                            // Clean up any remaining problematic requests and force exit to prevent infinite loop
                            Logger.LogWarning("Elevator {ElevatorId} has {RequestCount} requests but no serviceable floors. Cleaning up remaining requests and exiting processing loop.", 
                                elevator.Id, requests.Count);
                                
                            // Mark all remaining assigned requests as completed to prevent re-processing
                            var remainingRequests = requests.Where(r => r.Status == ElevatorRequestStatus.Assigned).ToList();
                            foreach (var request in remainingRequests)
                            {
                                Logger.LogWarning("Force-completing stuck request: floors {CurrentFloor} to {DestinationFloor}", 
                                    request.CurrentFloor, request.DestinationFloor);
                                request.Status = ElevatorRequestStatus.Completed;
                            }
                            
                            // Clean up collections
                            RemoveCompletedRequestsFromCollections(elevator.Id, requests);
                            break;
                        }
                    }
                }
                else
                {
                    // No current requests, should exit immediately
                    break;
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
    }
}