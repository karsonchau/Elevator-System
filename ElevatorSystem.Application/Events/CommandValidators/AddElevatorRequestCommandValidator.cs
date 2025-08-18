using ElevatorSystem.Application.Events.ElevatorCommands;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Events.CommandValidators;

/// <summary>
/// Validator for AddElevatorRequestCommand
/// </summary>
public class AddElevatorRequestCommandValidator : ICommandValidator<AddElevatorRequestCommand>
{
    private readonly IElevatorRepository _elevatorRepository;
    private readonly ILogger<AddElevatorRequestCommandValidator> _logger;

    public AddElevatorRequestCommandValidator(
        IElevatorRepository elevatorRepository,
        ILogger<AddElevatorRequestCommandValidator> logger)
    {
        _elevatorRepository = elevatorRepository;
        _logger = logger;
    }

    public async Task<CommandValidationResult> ValidateAsync(AddElevatorRequestCommand command, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Basic null checks
        if (command.Request == null)
        {
            errors.Add("Request cannot be null");
            return CommandValidationResult.Failure(errors);
        }

        var request = command.Request;

        // Validate request ID
        if (request.Id == Guid.Empty)
        {
            errors.Add("Request ID cannot be empty");
        }

        // Validate floors are different
        if (request.CurrentFloor == request.DestinationFloor)
        {
            errors.Add($"Current floor and destination floor cannot be the same ({request.CurrentFloor})");
        }

        // Validate against available elevators
        try
        {
            var elevators = await _elevatorRepository.GetAllAsync();
            var availableElevators = elevators.Where(e => e.Status != ElevatorStatus.OutOfService).ToList();
            
            if (!availableElevators.Any())
            {
                errors.Add("No elevators are currently available for service");
            }
            else
            {
                // Get the overall building floor range from all elevators
                var minFloor = availableElevators.Min(e => e.MinFloor);
                var maxFloor = availableElevators.Max(e => e.MaxFloor);
                
                if (request.CurrentFloor < minFloor || request.CurrentFloor > maxFloor)
                {
                    errors.Add($"Current floor {request.CurrentFloor} is outside the valid range ({minFloor} to {maxFloor})");
                }
                    
                if (request.DestinationFloor < minFloor || request.DestinationFloor > maxFloor)
                {
                    errors.Add($"Destination floor {request.DestinationFloor} is outside the valid range ({minFloor} to {maxFloor})");
                }
                
                // Check if there's at least one elevator that can serve both floors
                var canServeRequest = availableElevators.Any(e => 
                    request.CurrentFloor >= e.MinFloor && request.CurrentFloor <= e.MaxFloor &&
                    request.DestinationFloor >= e.MinFloor && request.DestinationFloor <= e.MaxFloor);
                    
                if (!canServeRequest)
                {
                    errors.Add($"No available elevator can serve a request from floor {request.CurrentFloor} to floor {request.DestinationFloor}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating elevator request {RequestId}", request.Id);
            errors.Add("Unable to validate request against available elevators");
        }

        // Validate request status
        if (request.Status != ElevatorRequestStatus.Pending)
        {
            errors.Add($"New requests must have Pending status, but status is {request.Status}");
        }

        return errors.Any() ? CommandValidationResult.Failure(errors) : CommandValidationResult.Success();
    }
}