using ElevatorSystem.Application.Events.ElevatorCommands;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Events.CommandHandlers;

/// <summary>
/// Robust command handler for submitting elevator requests with failure handling
/// </summary>
[CommandHandler("elevator-submissions", priority: 0, maxRetries: 5)]
public class SubmitElevatorRequestCommandHandler : BaseCommandHandler<SubmitElevatorRequestCommand, bool>
{
    private readonly ICommandBus _commandBus;
    private readonly IRetryPolicyManager _retryPolicyManager;
    private readonly IElevatorRequestRepository _requestRepository;

    public SubmitElevatorRequestCommandHandler(
        ICommandBus commandBus,
        IRetryPolicyManager retryPolicyManager,
        IElevatorRequestRepository requestRepository,
        ILogger<SubmitElevatorRequestCommandHandler> logger) : base(logger)
    {
        _commandBus = commandBus;
        _retryPolicyManager = retryPolicyManager;
        _requestRepository = requestRepository;
    }

    protected override async Task<bool> ExecuteAsync(SubmitElevatorRequestCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        
        try
        {
            Logger.LogInformation("Processing elevator request {RequestId} from floor {CurrentFloor} to {DestinationFloor} (attempt {AttemptNumber})", 
                request.Id, request.CurrentFloor, request.DestinationFloor, command.AttemptNumber);

            // Update status to validated
            await UpdateRequestStatusAsync(request.Id, ElevatorRequestStatus.Validated, 
                ElevatorRequestStatus.Pending, "Request validated and entering processing pipeline");

            // Use the existing AddElevatorRequestCommand for actual processing
            var addRequestCommand = new AddElevatorRequestCommand(request);
            var success = await _commandBus.SendAsync<AddElevatorRequestCommand, bool>(addRequestCommand, cancellationToken);

            if (success)
            {
                // Record success for circuit breaker
                _retryPolicyManager.RecordSuccess(request.Id);
                
                Logger.LogInformation("Successfully processed elevator request {RequestId}", request.Id);
                return true;
            }
            else
            {
                // Handle failure
                await HandleRequestFailure(request, command.AttemptNumber, 
                    new InvalidOperationException("Request processing failed"), cancellationToken);
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing elevator request {RequestId} (attempt {AttemptNumber}): {ErrorMessage}", 
                request.Id, command.AttemptNumber, ex.Message);

            await HandleRequestFailure(request, command.AttemptNumber, ex, cancellationToken);
            return false;
        }
    }

    private async Task HandleRequestFailure(ElevatorRequest request, int attemptNumber, Exception exception, CancellationToken cancellationToken)
    {
        // Record failure for circuit breaker
        _retryPolicyManager.RecordFailure(request.Id, exception);

        // Check if we should retry
        if (_retryPolicyManager.ShouldRetry(request, attemptNumber))
        {
            // Update status to retrying
            await UpdateRequestStatusAsync(request.Id, ElevatorRequestStatus.Retrying, 
                request.Status, $"Retrying after failure: {exception.Message}");

            // Schedule retry with exponential backoff
            var retryDelay = _retryPolicyManager.CalculateRetryDelay(attemptNumber + 1);
            
            Logger.LogWarning("Scheduling retry for request {RequestId} in {DelayMs}ms (attempt {NextAttempt})", 
                request.Id, retryDelay.TotalMilliseconds, attemptNumber + 1);

            // Create retry command (this would typically be scheduled via a background service)
            var retryCommand = new RetryFailedRequestCommand(request, attemptNumber + 1, exception);
            
            // For now, we'll delay and retry inline (in production, use background service)
            _ = Task.Run(async () =>
            {
                await Task.Delay(retryDelay, cancellationToken);
                
                try
                {
                    // Convert retry command back to submit command
                    var submitCommand = new SubmitElevatorRequestCommand(request, attemptNumber + 1);
                    await _commandBus.SendAsync<SubmitElevatorRequestCommand, bool>(submitCommand, cancellationToken);
                }
                catch (Exception retryEx)
                {
                    Logger.LogError(retryEx, "Error during retry of request {RequestId}", request.Id);
                }
            }, cancellationToken);
        }
        else
        {
            // Mark as permanently failed
            await UpdateRequestStatusAsync(request.Id, ElevatorRequestStatus.Failed, 
                request.Status, $"Request failed permanently after {attemptNumber} attempts. Last error: {exception.Message}");
            
            Logger.LogError("Request {RequestId} marked as permanently failed after {AttemptNumber} attempts", 
                request.Id, attemptNumber);
        }
    }

    private async Task UpdateRequestStatusAsync(Guid requestId, ElevatorRequestStatus newStatus, 
        ElevatorRequestStatus? previousStatus, string reason)
    {
        try
        {
            var updateCommand = new UpdateRequestStatusCommand(requestId, newStatus, previousStatus, reason);
            await _commandBus.SendAsync(updateCommand);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update status for request {RequestId} to {NewStatus}", requestId, newStatus);
        }
    }
}