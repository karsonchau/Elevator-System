using ElevatorSystem.Application.Events.ElevatorCommands;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ElevatorSystem.Application.Events.CommandHandlers;

/// <summary>
/// Command handler for adding elevator requests
/// </summary>
[CommandHandler("elevator-requests", priority: 1, maxRetries: 3)]
public class AddElevatorRequestCommandHandler : BaseCommandHandler<AddElevatorRequestCommand, bool>
{
    private readonly IElevatorRepository _elevatorRepository;
    private readonly IElevatorRequestRepository _requestRepository;
    private readonly ConcurrentDictionary<int, ConcurrentBag<ElevatorRequest>> _elevatorActiveRequests;
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, bool>> _elevatorFloorsNeedingService;
    private readonly object _requestAssignmentLock = new();

    public AddElevatorRequestCommandHandler(
        IElevatorRepository elevatorRepository,
        IElevatorRequestRepository requestRepository,
        ConcurrentDictionary<int, ConcurrentBag<ElevatorRequest>> elevatorActiveRequests,
        ConcurrentDictionary<int, ConcurrentDictionary<int, bool>> elevatorFloorsNeedingService,
        ILogger<AddElevatorRequestCommandHandler> logger) : base(logger)
    {
        _elevatorRepository = elevatorRepository;
        _requestRepository = requestRepository;
        _elevatorActiveRequests = elevatorActiveRequests;
        _elevatorFloorsNeedingService = elevatorFloorsNeedingService;
    }

    protected override async Task<bool> ExecuteAsync(AddElevatorRequestCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Validate request against available elevators
            await ValidateRequestAsync(command.Request);
            
            // Use lock to prevent race conditions in assignment
            lock (_requestAssignmentLock)
            {
                // Immediately assign request to best elevator
                var bestElevator = FindBestElevatorForRequestSync(command.Request);
                
                InitializeElevatorCollections(bestElevator.Id);

                _elevatorActiveRequests[bestElevator.Id].Add(command.Request);
                
                // Add pickup floor to floors needing service
                _elevatorFloorsNeedingService[bestElevator.Id].TryAdd(command.Request.CurrentFloor, true);
                
                // Also add destination floor since we know it will be needed
                _elevatorFloorsNeedingService[bestElevator.Id].TryAdd(command.Request.DestinationFloor, true);
                
                command.Request.Status = ElevatorRequestStatus.Assigned;
                
                Logger.LogInformation("Added and immediately assigned request {RequestId} from floor {CurrentFloor} to {DestinationFloor} to elevator {ElevatorId}", 
                    command.Request.Id, command.Request.CurrentFloor, command.Request.DestinationFloor, bestElevator.Id);
            }
            
            // Update repository outside lock to minimize lock time
            await _requestRepository.UpdateAsync(command.Request);
            
            return true;
        }
        catch (ArgumentNullException ex)
        {
            Logger.LogError("Null request provided: {Error}", ex.Message);
            return false;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Logger.LogError("Request validation failed for request {RequestId} (floors {CurrentFloor} to {DestinationFloor}): {Error}", 
                command.Request?.Id, command.Request?.CurrentFloor, command.Request?.DestinationFloor, ex.Message);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogError("Cannot process request {RequestId} (floors {CurrentFloor} to {DestinationFloor}): {Error}", 
                command.Request?.Id, command.Request?.CurrentFloor, command.Request?.DestinationFloor, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error adding request {RequestId} (floors {CurrentFloor} to {DestinationFloor})", 
                command.Request?.Id, command.Request?.CurrentFloor, command.Request?.DestinationFloor);
            
            // Set request to failed status if it was partially processed
            if (command.Request?.Status == ElevatorRequestStatus.Assigned)
            {
                try
                {
                    command.Request.Status = ElevatorRequestStatus.Failed;
                    await _requestRepository.UpdateAsync(command.Request);
                }
                catch (Exception updateEx)
                {
                    Logger.LogError(updateEx, "Failed to update request status to failed for request {RequestId}", command.Request.Id);
                }
            }
            
            return false;
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
}