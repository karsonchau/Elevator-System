using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Events.ElevatorCommands;

/// <summary>
/// Command to retry a failed elevator request with exponential backoff
/// </summary>
public class RetryFailedRequestCommand : BaseCommand
{
    public ElevatorRequest Request { get; }
    public int AttemptNumber { get; }
    public Exception? LastException { get; }
    public DateTime LastFailureTime { get; }

    public RetryFailedRequestCommand(ElevatorRequest request, int attemptNumber, Exception? lastException = null)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        AttemptNumber = attemptNumber;
        LastException = lastException;
        LastFailureTime = DateTime.UtcNow;
    }
}