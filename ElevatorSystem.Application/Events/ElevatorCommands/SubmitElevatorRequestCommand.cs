using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Events.ElevatorCommands;

/// <summary>
/// Command to submit a new elevator request through the robust processing pipeline
/// </summary>
[Message("elevator.request.submit", persistent: true)]
public class SubmitElevatorRequestCommand : BaseCommand
{
    public ElevatorRequest Request { get; }
    public int AttemptNumber { get; }
    public DateTime SubmittedAt { get; }

    public SubmitElevatorRequestCommand(ElevatorRequest request, int attemptNumber = 1)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        AttemptNumber = attemptNumber;
        SubmittedAt = DateTime.UtcNow;
    }
}