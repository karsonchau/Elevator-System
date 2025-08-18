using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Events;
using ElevatorSystem.Application.Events.ElevatorEvents;
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
    private readonly IEventBus _eventBus;

    public ElevatorMovementService(
        IElevatorRepository elevatorRepository,
        ILogger<ElevatorMovementService> logger,
        IEventBus eventBus)
    {
        _elevatorRepository = elevatorRepository;
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Moves the elevator to the specified target floor.
    /// </summary>
    /// <param name="elevator">The elevator to move.</param>
    /// <param name="targetFloor">The floor to move to.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task MoveToFloorAsync(Elevator elevator, int targetFloor, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 1000;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                elevator.SetStatus(ElevatorStatus.Moving);
                await _elevatorRepository.UpdateAsync(elevator);
                
                while (elevator.CurrentFloor != targetFloor && !cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    await Task.Delay(elevator.FloorMovementTimeMs, cancellationToken);
                    
                    var previousFloor = elevator.CurrentFloor;
                    var nextFloor = elevator.Direction == ElevatorDirection.Up 
                        ? elevator.CurrentFloor + 1 
                        : elevator.CurrentFloor - 1;
                    
                    // Validate floor bounds
                    if (nextFloor < elevator.MinFloor || nextFloor > elevator.MaxFloor)
                    {
                        _logger.LogError("Elevator {ElevatorId} attempted to move to invalid floor {Floor}. Valid range: {MinFloor}-{MaxFloor}", 
                            elevator.Id, nextFloor, elevator.MinFloor, elevator.MaxFloor);
                        throw new InvalidOperationException($"Cannot move elevator to floor {nextFloor}. Valid range: {elevator.MinFloor}-{elevator.MaxFloor}");
                    }
                    
                    elevator.MoveTo(nextFloor);
                    
                    // Retry repository update with exponential backoff
                    await RetryRepositoryUpdateAsync(() => _elevatorRepository.UpdateAsync(elevator), 
                        elevator.Id, "elevator position", cancellationToken);
                    
                    // Publish elevator moved event (Phase 1 event infrastructure)
                    var elevatorMovedEvent = new ElevatorMovedEvent(elevator.Id, previousFloor, nextFloor, elevator.Direction);
                    await _eventBus.PublishAsync(elevatorMovedEvent, cancellationToken);
                }
                
                return; // Success, exit retry loop
            }
            catch (OperationCanceledException)
            {
                throw; // Don't retry cancellation
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Error moving elevator {ElevatorId} to floor {TargetFloor}, attempt {Attempt}/{MaxRetries}", 
                    elevator.Id, targetFloor, attempt + 1, maxRetries);
                
                try
                {
                    await Task.Delay(retryDelayMs * (attempt + 1), cancellationToken); // Exponential backoff
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move elevator {ElevatorId} to floor {TargetFloor} after {MaxRetries} attempts", 
                    elevator.Id, targetFloor, maxRetries);
                
                // Set elevator to maintenance mode on critical failure
                try
                {
                    elevator.SetStatus(ElevatorStatus.OutOfService);
                    await _elevatorRepository.UpdateAsync(elevator);
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Failed to update elevator {ElevatorId} status to OutOfService", elevator.Id);
                }
                
                throw;
            }
        }
    }
    
    private async Task RetryRepositoryUpdateAsync(Func<Task> updateAction, int elevatorId, string operation, CancellationToken cancellationToken)
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
                _logger.LogWarning(ex, "Failed to update {Operation} for elevator {ElevatorId}, attempt {Attempt}/{MaxRetries}", 
                    operation, elevatorId, attempt + 1, maxRetries);
                
                await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt), cancellationToken);
            }
        }
        
        // Final attempt - let exception bubble up
        await updateAction();
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