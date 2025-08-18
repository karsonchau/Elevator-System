using ElevatorSystem.Application.Events.ElevatorCommands;
using ElevatorSystem.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Events.CommandHandlers;

/// <summary>
/// Command handler for updating elevator request status
/// </summary>
[CommandHandler("status-updates", priority: 1)]
public class UpdateRequestStatusCommandHandler : BaseCommandHandler<UpdateRequestStatusCommand>
{
    private readonly IElevatorRequestRepository _requestRepository;

    public UpdateRequestStatusCommandHandler(
        IElevatorRequestRepository requestRepository,
        ILogger<UpdateRequestStatusCommandHandler> logger) : base(logger)
    {
        _requestRepository = requestRepository;
    }

    protected override async Task ExecuteAsync(UpdateRequestStatusCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var request = await _requestRepository.GetByIdAsync(command.RequestId);
            
            if (request == null)
            {
                Logger.LogWarning("Cannot update status for non-existent request {RequestId}", command.RequestId);
                return;
            }

            var previousStatus = request.Status;
            request.Status = command.NewStatus;
            
            await _requestRepository.UpdateAsync(request);

            Logger.LogInformation("Updated request {RequestId} status from {PreviousStatus} to {NewStatus}. Reason: {Reason}", 
                command.RequestId, previousStatus, command.NewStatus, command.StatusReason ?? "None specified");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating status for request {RequestId} to {NewStatus}: {ErrorMessage}", 
                command.RequestId, command.NewStatus, ex.Message);
            throw;
        }
    }
}