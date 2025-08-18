using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Events.ElevatorCommands;

/// <summary>
/// Command to find the optimal elevator for a request
/// </summary>
public class FindOptimalElevatorCommand : BaseCommand
{
    public FindOptimalElevatorCommand(ElevatorRequest request, string? correlationId = null) : base(correlationId)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        RequestId = request.Id;
        CurrentFloor = request.CurrentFloor;
        DestinationFloor = request.DestinationFloor;
    }

    /// <summary>
    /// The elevator request to find an elevator for
    /// </summary>
    public ElevatorRequest Request { get; }
    
    /// <summary>
    /// Unique identifier of the request
    /// </summary>
    public Guid RequestId { get; }
    
    /// <summary>
    /// Floor where passenger is waiting
    /// </summary>
    public int CurrentFloor { get; }
    
    /// <summary>
    /// Floor where passenger wants to go
    /// </summary>
    public int DestinationFloor { get; }
}

/// <summary>
/// Response containing the optimal elevator assignment
/// </summary>
public class FindOptimalElevatorResponse
{
    public FindOptimalElevatorResponse(int elevatorId, string reason)
    {
        ElevatorId = elevatorId;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>
    /// ID of the optimal elevator
    /// </summary>
    public int ElevatorId { get; }
    
    /// <summary>
    /// Reason why this elevator was selected
    /// </summary>
    public string Reason { get; }
}