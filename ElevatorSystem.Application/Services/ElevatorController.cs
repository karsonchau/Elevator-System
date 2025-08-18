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
    private readonly ElevatorMovementService _movementService;
    private readonly ElevatorRequestManager _requestManager;
    private readonly Dictionary<int, List<ElevatorRequest>> _elevatorActiveRequests = new();
    private readonly Dictionary<int, SortedSet<int>> _elevatorFloorsNeedingService = new();

    public ElevatorController(
        IElevatorRepository elevatorRepository,
        IElevatorRequestRepository requestRepository,
        ILogger<ElevatorController> logger,
        ElevatorMovementService movementService,
        ElevatorRequestManager requestManager)
    {
        _elevatorRepository = elevatorRepository;
        _requestRepository = requestRepository;
        _logger = logger;
        _movementService = movementService;
        _requestManager = requestManager;
    }

    public async Task AddRequestAsync(ElevatorRequest request)
    {
        // Immediately assign request to best elevator
        var bestElevator = await FindBestElevatorForRequest(request);
        
        InitializeElevatorCollections(bestElevator.Id);

        _elevatorActiveRequests[bestElevator.Id].Add(request);
        
        // Add pickup floor to floors needing service
        _elevatorFloorsNeedingService[bestElevator.Id].Add(request.CurrentFloor);
        
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
            
            if (!_elevatorActiveRequests[elevator.Id].Any())
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
        var requests = _elevatorActiveRequests[elevator.Id];
        
        // Determine direction based on current requests and elevator state
        var direction = _movementService.DetermineOptimalDirection(elevator, requests);
        elevator.SetDirection(direction);
        
        // Move linearly in the chosen direction, stopping only at floors with pickup/dropoff
        while (!cancellationToken.IsCancellationRequested && requests.Any())
        {
            var currentRequests = _elevatorActiveRequests[elevator.Id]; // Get fresh list in case new requests were added
            
            // Check if we should stop at current floor
            if (_requestManager.ShouldStopAtCurrentFloor(elevator, currentRequests))
            {
                await _requestManager.ProcessCurrentFloorActionsAsync(elevator, currentRequests, _elevatorFloorsNeedingService[elevator.Id]);
                // Remove completed requests and update sorted collections
                _requestManager.RemoveCompletedRequests(currentRequests, _elevatorFloorsNeedingService[elevator.Id]);
                requests = _elevatorActiveRequests[elevator.Id];
                continue;
            }
            
            // Find next floor in current direction that needs service
            var nextServiceFloor = _movementService.FindNextFloorInDirection(elevator, _elevatorFloorsNeedingService[elevator.Id], direction);
            _logger.LogInformation("Elevator {ElevatorId} at floor {CurrentFloor} going {Direction}, next service floor: {NextFloor}", 
                elevator.Id, elevator.CurrentFloor, direction, nextServiceFloor?.ToString() ?? "none");
            
            if (!nextServiceFloor.HasValue)
            {
                // No more floors in this direction, reverse if needed
                if (currentRequests.Any())
                {
                    direction = direction == ElevatorDirection.Up ? ElevatorDirection.Down : ElevatorDirection.Up;
                    elevator.SetDirection(direction);
                    nextServiceFloor = _movementService.FindNextFloorInDirection(elevator, _elevatorFloorsNeedingService[elevator.Id], direction);
                    
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
            requests = _elevatorActiveRequests[elevator.Id]; // Refresh after movement
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

    private void InitializeElevatorCollections(int elevatorId)
    {
        if (!_elevatorActiveRequests.ContainsKey(elevatorId))
            _elevatorActiveRequests[elevatorId] = new List<ElevatorRequest>();
        if (!_elevatorFloorsNeedingService.ContainsKey(elevatorId))
            _elevatorFloorsNeedingService[elevatorId] = new SortedSet<int>();
    }
}