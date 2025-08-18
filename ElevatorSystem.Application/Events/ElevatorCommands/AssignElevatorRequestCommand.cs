namespace ElevatorSystem.Application.Events.ElevatorCommands;

/// <summary>
/// Command to assign an elevator request to a specific elevator
/// </summary>
public class AssignElevatorRequestCommand : BaseCommand
{
    public AssignElevatorRequestCommand(Guid requestId, int elevatorId, string? correlationId = null) : base(correlationId)
    {
        RequestId = requestId;
        ElevatorId = elevatorId;
    }

    /// <summary>
    /// Unique identifier of the request to assign
    /// </summary>
    public Guid RequestId { get; }
    
    /// <summary>
    /// ID of the elevator to assign the request to
    /// </summary>
    public int ElevatorId { get; }
}