using ElevatorSystem.Domain.Enums;

namespace ElevatorSystem.Application.Events.ElevatorCommands;

/// <summary>
/// Command to update the status of an elevator request
/// </summary>
public class UpdateRequestStatusCommand : BaseCommand
{
    public Guid RequestId { get; }
    public ElevatorRequestStatus NewStatus { get; }
    public ElevatorRequestStatus? PreviousStatus { get; }
    public string? StatusReason { get; }
    public DateTime UpdatedAt { get; }

    public UpdateRequestStatusCommand(
        Guid requestId, 
        ElevatorRequestStatus newStatus, 
        ElevatorRequestStatus? previousStatus = null,
        string? statusReason = null)
    {
        RequestId = requestId;
        NewStatus = newStatus;
        PreviousStatus = previousStatus;
        StatusReason = statusReason;
        UpdatedAt = DateTime.UtcNow;
    }
}