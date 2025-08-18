using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Events.ElevatorCommands;

/// <summary>
/// Command to add a new elevator request to the system
/// </summary>
public class AddElevatorRequestCommand : BaseCommand
{
    public ElevatorRequest Request { get; }

    public AddElevatorRequestCommand(ElevatorRequest request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }
}