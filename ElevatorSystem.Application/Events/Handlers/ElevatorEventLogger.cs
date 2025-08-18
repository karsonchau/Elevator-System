using ElevatorSystem.Application.Events.ElevatorEvents;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Events.Handlers;

/// <summary>
/// Event handler that logs all elevator events for monitoring and debugging
/// Demonstrates Phase 1 event infrastructure in action
/// </summary>
[EventHandler("elevator-events", "event-logging")]
public class ElevatorEventLogger
{
    private readonly ILogger<ElevatorEventLogger> _logger;

    public ElevatorEventLogger(ILogger<ElevatorEventLogger> logger)
    {
        _logger = logger;
    }

    public Task HandleElevatorMovedEvent(ElevatorMovedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[EVENT] Elevator {ElevatorId} moved from floor {FromFloor} to {ToFloor} going {Direction} at {Timestamp}", 
            @event.ElevatorId, @event.FromFloor, @event.ToFloor, @event.Direction, @event.Timestamp);
        return Task.CompletedTask;
    }

    public Task HandlePassengerPickedUpEvent(PassengerPickedUpEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[EVENT] Passenger picked up by elevator {ElevatorId} at floor {Floor}, going to {DestinationFloor} (Request: {RequestId}) at {Timestamp}", 
            @event.ElevatorId, @event.Floor, @event.DestinationFloor, @event.RequestId, @event.Timestamp);
        return Task.CompletedTask;
    }

    public Task HandlePassengerDroppedOffEvent(PassengerDroppedOffEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[EVENT] Passenger dropped off by elevator {ElevatorId} at floor {Floor} (Request: {RequestId}) at {Timestamp}", 
            @event.ElevatorId, @event.Floor, @event.RequestId, @event.Timestamp);
        return Task.CompletedTask;
    }

    public Task HandleElevatorRequestReceivedEvent(ElevatorRequestReceivedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[EVENT] New elevator request received: {RequestId} from floor {CurrentFloor} to {DestinationFloor} at {Timestamp}", 
            @event.RequestId, @event.CurrentFloor, @event.DestinationFloor, @event.Timestamp);
        return Task.CompletedTask;
    }

    public Task HandleElevatorRequestAssignedEvent(ElevatorRequestAssignedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[EVENT] Request {RequestId} assigned to elevator {ElevatorId} at {Timestamp}", 
            @event.RequestId, @event.ElevatorId, @event.Timestamp);
        return Task.CompletedTask;
    }

    public Task HandleElevatorStatusChangedEvent(ElevatorStatusChangedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[EVENT] Elevator {ElevatorId} status changed from {PreviousStatus} to {NewStatus} at floor {CurrentFloor} at {Timestamp}", 
            @event.ElevatorId, @event.PreviousStatus, @event.NewStatus, @event.CurrentFloor, @event.Timestamp);
        return Task.CompletedTask;
    }
}