using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Services;

public class ElevatorController : IElevatorController
{
    private readonly IElevatorRepository _elevatorRepository;
    private readonly IElevatorRequestRepository _requestRepository;
    private readonly ILogger<ElevatorController> _logger;
    private readonly Dictionary<int, List<ElevatorRequest>> _activeRequests = new();
    private readonly Dictionary<int, SortedSet<int>> _floorsNeedingService = new();

    public ElevatorController(
        IElevatorRepository elevatorRepository,
        IElevatorRequestRepository requestRepository,
        ILogger<ElevatorController> logger)
    {
        _elevatorRepository = elevatorRepository;
        _requestRepository = requestRepository;
        _logger = logger;
    }

    public async Task AddRequestAsync(ElevatorRequest request)
    {
        // Immediately assign request to best elevator
        var bestElevator = await FindBestElevatorForRequest(request);
        
        InitializeElevatorCollections(bestElevator.Id);

        _activeRequests[bestElevator.Id].Add(request);
        
        // Add pickup floor to floors needing service
        _floorsNeedingService[bestElevator.Id].Add(request.CurrentFloor);
        
        request.Status = ElevatorRequestStatus.Assigned;
        await _requestRepository.UpdateAsync(request);
        
        _logger.LogInformation("Added and immediately assigned request {RequestId} from floor {CurrentFloor} to {DestinationFloor} to elevator {ElevatorId}", 
            request.Id, request.CurrentFloor, request.DestinationFloor, bestElevator.Id);
    }

    public async Task ProcessElevatorAsync(Elevator elevator, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            InitializeElevatorCollections(elevator.Id);
            
            if (!_activeRequests[elevator.Id].Any())
            {
                elevator.SetStatus(ElevatorStatus.Idle);
                elevator.SetDirection(ElevatorDirection.Idle);
                await _elevatorRepository.UpdateAsync(elevator);
                await Task.Delay(100, cancellationToken);
                continue;
            }

            await ExecuteLinearScanMovement(elevator, cancellationToken);
        }
    }


    private async Task ExecuteLinearScanMovement(Elevator elevator, CancellationToken cancellationToken)
    {
        var requests = _activeRequests[elevator.Id];
        
        // Determine direction based on current requests and elevator state
        var direction = DetermineOptimalDirection(elevator, requests);
        elevator.SetDirection(direction);
        
        // Move linearly in the chosen direction, stopping only at floors with pickup/dropoff
        while (!cancellationToken.IsCancellationRequested && requests.Any())
        {
            var currentRequests = _activeRequests[elevator.Id]; // Get fresh list in case new requests were added
            
            // Check if we should stop at current floor
            if (ShouldStopAtCurrentFloor(elevator, currentRequests))
            {
                await ProcessCurrentFloorActions(elevator, currentRequests);
                // Remove completed requests and update sorted collections
                RemoveCompletedRequests(elevator.Id);
                requests = _activeRequests[elevator.Id];
                continue;
            }
            
            // Find next floor in current direction that needs service
            var nextServiceFloor = FindNextFloorInDirection(elevator, currentRequests, direction);
            _logger.LogInformation("Elevator {ElevatorId} at floor {CurrentFloor} going {Direction}, next service floor: {NextFloor}", 
                elevator.Id, elevator.CurrentFloor, direction, nextServiceFloor?.ToString() ?? "none");
            
            if (!nextServiceFloor.HasValue)
            {
                // No more floors in this direction, reverse if needed
                if (currentRequests.Any())
                {
                    direction = direction == ElevatorDirection.Up ? ElevatorDirection.Down : ElevatorDirection.Up;
                    elevator.SetDirection(direction);
                    nextServiceFloor = FindNextFloorInDirection(elevator, currentRequests, direction);
                    
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
            await MoveToFloor(elevator, nextServiceFloor.Value, cancellationToken);
            requests = _activeRequests[elevator.Id]; // Refresh after movement
        }
    }
    
    private ElevatorDirection DetermineOptimalDirection(Elevator elevator, List<ElevatorRequest> requests)
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
    
    private int? FindNextFloorInDirection(Elevator elevator, List<ElevatorRequest> requests, ElevatorDirection direction)
    {
        var elevatorId = elevator.Id;
        var currentFloor = elevator.CurrentFloor;
        
        if (direction == ElevatorDirection.Up)
        {
            // Get next floor > current floor (exclude current floor to avoid infinite loop)
            var candidateFloors = _floorsNeedingService[elevatorId].Where(f => f > currentFloor);
            return candidateFloors.Any() ? candidateFloors.First() : null;
        }
        else
        {
            // Get next floor < current floor (in descending order, exclude current floor)
            var candidateFloors = _floorsNeedingService[elevatorId].Where(f => f < currentFloor);
            return candidateFloors.Any() ? candidateFloors.Last() : null;
        }
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
    
    private async Task MoveToFloor(Elevator elevator, int targetFloor, CancellationToken cancellationToken)
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
    
    private async Task ProcessCurrentFloorActions(Elevator elevator, List<ElevatorRequest> requests)
    {
        elevator.SetStatus(ElevatorStatus.Loading);
        await _elevatorRepository.UpdateAsync(elevator);
        
        var currentFloor = elevator.CurrentFloor;
        var hasActions = false;
        
        // Process dropoffs first
        var dropoffRequests = requests.Where(r => r.Status == ElevatorRequestStatus.InProgress && 
                                                  r.DestinationFloor == currentFloor).ToList();
        foreach (var request in dropoffRequests)
        {
            _logger.LogInformation("Floor {Floor}: Dropoff passenger", currentFloor);
            request.Status = ElevatorRequestStatus.Completed;
            await _requestRepository.UpdateAsync(request);
            hasActions = true;
        }
        
        // Process pickups second - only passengers going in same direction and valid timing
        var pickupRequests = requests.Where(r => r.Status == ElevatorRequestStatus.Assigned && 
                                                 r.CurrentFloor == currentFloor &&
                                                 IsPassengerGoingInSameDirection(r, elevator.Direction) &&
                                                 CanPickupPassenger(elevator, r, elevator.Direction)).ToList();
        foreach (var request in pickupRequests)
        {
            _logger.LogInformation("Floor {Floor}: Pickup passenger going to {DestinationFloor}", 
                currentFloor, request.DestinationFloor);
            request.Status = ElevatorRequestStatus.InProgress;
            await _requestRepository.UpdateAsync(request);
            
            // Add destination floor to floors needing service
            _floorsNeedingService[elevator.Id].Add(request.DestinationFloor);
                
            hasActions = true;
        }
        
        if (hasActions)
        {
            await Task.Delay(elevator.LoadingTimeMs);
        }
    }

    private async Task<Elevator> FindBestElevatorForRequest(ElevatorRequest request)
    {
        var elevators = await _elevatorRepository.GetAllAsync();
        
        return elevators
            .Where(e => e.Status != ElevatorStatus.OutOfService)
            .OrderBy(e => Math.Abs(e.CurrentFloor - request.CurrentFloor))
            .First();
    }

    private bool ShouldStopAtCurrentFloor(Elevator elevator, List<ElevatorRequest> requests)
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
    
    private bool IsPassengerGoingInSameDirection(ElevatorRequest request, ElevatorDirection elevatorDirection)
    {
        var passengerDirection = request.DestinationFloor > request.CurrentFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
        return passengerDirection == elevatorDirection;
    }
    
    private void InitializeElevatorCollections(int elevatorId)
    {
        if (!_activeRequests.ContainsKey(elevatorId))
            _activeRequests[elevatorId] = new List<ElevatorRequest>();
        if (!_floorsNeedingService.ContainsKey(elevatorId))
            _floorsNeedingService[elevatorId] = new SortedSet<int>();
    }
    
    private void RemoveCompletedRequests(int elevatorId)
    {
        var completedRequests = _activeRequests[elevatorId]
            .Where(r => r.Status == ElevatorRequestStatus.Completed)
            .ToList();
            
        foreach (var request in completedRequests)
        {
            // Remove from active requests
            _activeRequests[elevatorId].Remove(request);
            
            // Only remove floor if no other requests need it
            if (!HasOtherRequestsForFloor(elevatorId, request.CurrentFloor))
            {
                _floorsNeedingService[elevatorId].Remove(request.CurrentFloor);
            }
            
            if (!HasOtherRequestsForFloor(elevatorId, request.DestinationFloor))
            {
                _floorsNeedingService[elevatorId].Remove(request.DestinationFloor);
            }
        }
    }
    
    private bool HasOtherRequestsForFloor(int elevatorId, int floor)
    {
        return _activeRequests[elevatorId].Any(r => 
            r.Status != ElevatorRequestStatus.Completed && 
            (r.CurrentFloor == floor || r.DestinationFloor == floor));
    }
}