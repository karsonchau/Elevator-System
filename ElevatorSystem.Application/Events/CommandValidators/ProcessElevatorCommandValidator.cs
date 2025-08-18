using ElevatorSystem.Application.Events.ElevatorCommands;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Events.CommandValidators;

/// <summary>
/// Validator for ProcessElevatorCommand
/// </summary>
public class ProcessElevatorCommandValidator : ICommandValidator<ProcessElevatorCommand>
{
    private readonly ILogger<ProcessElevatorCommandValidator> _logger;

    public ProcessElevatorCommandValidator(ILogger<ProcessElevatorCommandValidator> logger)
    {
        _logger = logger;
    }

    public Task<CommandValidationResult> ValidateAsync(ProcessElevatorCommand command, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Basic null checks
        if (command.Elevator == null)
        {
            errors.Add("Elevator cannot be null");
            return Task.FromResult(CommandValidationResult.Failure(errors));
        }

        var elevator = command.Elevator;

        // Validate elevator ID
        if (elevator.Id <= 0)
        {
            errors.Add($"Elevator ID must be positive, but was {elevator.Id}");
        }

        // Validate elevator is not out of service
        if (elevator.Status == ElevatorStatus.OutOfService)
        {
            errors.Add($"Cannot process elevator {elevator.Id} because it is out of service");
        }

        // Validate floor bounds
        if (elevator.CurrentFloor < elevator.MinFloor || elevator.CurrentFloor > elevator.MaxFloor)
        {
            errors.Add($"Elevator {elevator.Id} current floor {elevator.CurrentFloor} is outside valid range ({elevator.MinFloor} to {elevator.MaxFloor})");
        }

        // Validate movement timing parameters
        if (elevator.FloorMovementTimeMs <= 0)
        {
            errors.Add($"Elevator {elevator.Id} floor movement time must be positive, but was {elevator.FloorMovementTimeMs}ms");
        }

        if (elevator.LoadingTimeMs < 0)
        {
            errors.Add($"Elevator {elevator.Id} loading time cannot be negative, but was {elevator.LoadingTimeMs}ms");
        }

        // Validate floor range consistency
        if (elevator.MinFloor >= elevator.MaxFloor)
        {
            errors.Add($"Elevator {elevator.Id} min floor ({elevator.MinFloor}) must be less than max floor ({elevator.MaxFloor})");
        }

        return Task.FromResult(errors.Any() ? CommandValidationResult.Failure(errors) : CommandValidationResult.Success());
    }
}