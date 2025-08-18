using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Events.ElevatorCommands;

/// <summary>
/// Command to process elevator movement and passenger operations
/// </summary>
[Message("elevator.process", persistent: true)]
public class ProcessElevatorCommand : BaseCommand
{
    public Elevator Elevator { get; }

    public ProcessElevatorCommand(Elevator elevator)
    {
        Elevator = elevator ?? throw new ArgumentNullException(nameof(elevator));
    }
}