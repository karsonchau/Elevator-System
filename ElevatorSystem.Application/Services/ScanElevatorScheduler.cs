using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Services;

public class ScanElevatorScheduler : IElevatorScheduler
{
    private readonly IElevatorRepository _elevatorRepository;
    private readonly ILogger<ScanElevatorScheduler> _logger;

    public ScanElevatorScheduler(IElevatorRepository elevatorRepository, ILogger<ScanElevatorScheduler> logger)
    {
        _elevatorRepository = elevatorRepository;
        _logger = logger;
    }

    public async Task<Elevator> AssignElevatorAsync(ElevatorRequest request)
    {
        var availableElevators = await GetAvailableElevatorsAsync();
        
        if (!availableElevators.Any())
        {
            throw new InvalidOperationException("No elevators available");
        }

        var bestElevator = FindBestElevatorUsingScan(availableElevators, request);
        
        _logger.LogInformation("Assigned elevator {ElevatorId} to request from floor {CurrentFloor} to {DestinationFloor}", 
            bestElevator.Id, request.CurrentFloor, request.DestinationFloor);
        
        return bestElevator;
    }

    public async Task<IEnumerable<Elevator>> GetAvailableElevatorsAsync()
    {
        var allElevators = await _elevatorRepository.GetAllAsync();
        return allElevators.Where(e => e.Status != ElevatorStatus.OutOfService);
    }

    private Elevator FindBestElevatorUsingScan(IEnumerable<Elevator> elevators, ElevatorRequest request)
    {
        Elevator? bestElevator = null;
        var bestScore = double.MaxValue;

        foreach (var elevator in elevators)
        {
            var score = CalculateScanScore(elevator, request);
            
            if (score < bestScore)
            {
                bestScore = score;
                bestElevator = elevator;
            }
        }

        return bestElevator ?? elevators.First();
    }

    private double CalculateScanScore(Elevator elevator, ElevatorRequest request)
    {
        var pickupFloor = request.CurrentFloor;
        var destinationFloor = request.DestinationFloor;
        var requestDirection = request.Direction;

        if (elevator.Status == ElevatorStatus.Idle)
        {
            return elevator.GetDistanceToFloor(pickupFloor);
        }

        var elevatorDirection = elevator.Direction;
        var currentFloor = elevator.CurrentFloor;

        if (elevatorDirection == requestDirection)
        {
            if (elevatorDirection == ElevatorDirection.Up)
            {
                if (pickupFloor >= currentFloor && destinationFloor >= pickupFloor)
                {
                    return elevator.GetDistanceToFloor(pickupFloor);
                }
            }
            else if (elevatorDirection == ElevatorDirection.Down)
            {
                if (pickupFloor <= currentFloor && destinationFloor <= pickupFloor)
                {
                    return elevator.GetDistanceToFloor(pickupFloor);
                }
            }
        }

        var timeToChangeDirection = CalculateTimeToChangeDirection(elevator);
        var distanceAfterDirectionChange = elevator.GetDistanceToFloor(pickupFloor);
        
        return timeToChangeDirection + distanceAfterDirectionChange + 50;
    }

    private double CalculateTimeToChangeDirection(Elevator elevator)
    {
        if (elevator.Direction == ElevatorDirection.Up)
        {
            return elevator.MaxFloor - elevator.CurrentFloor;
        }
        else if (elevator.Direction == ElevatorDirection.Down)
        {
            return elevator.CurrentFloor - elevator.MinFloor;
        }
        
        return 0;
    }
}