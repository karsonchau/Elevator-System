using ElevatorSystem.Application.Events;
using ElevatorSystem.Application.Events.ElevatorCommands;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Services;

/// <summary>
/// Command-based elevator controller that uses CQRS pattern for elevator operations
/// </summary>
public class CommandBasedElevatorController : IElevatorController, IDisposable
{
    private readonly ICommandBus _commandBus;
    private readonly ILogger<CommandBasedElevatorController> _logger;
    private readonly Timer _cleanupTimer;
    private volatile bool _disposed = false;

    public CommandBasedElevatorController(
        ICommandBus commandBus,
        ILogger<CommandBasedElevatorController> logger)
    {
        _commandBus = commandBus;
        _logger = logger;
        
        // Initialize cleanup timer - runs every 30 seconds
        _cleanupTimer = new Timer(PerformPeriodicCleanup, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Adds an elevator request using command pattern
    /// </summary>
    /// <param name="request">The elevator request to add</param>
    /// <returns>True if the request was successfully added, false otherwise</returns>
    public async Task<bool> AddRequestAsync(ElevatorRequest request)
    {
        try
        {
            _logger.LogDebug("Adding elevator request {RequestId} from floor {CurrentFloor} to {DestinationFloor}", 
                request.Id, request.CurrentFloor, request.DestinationFloor);

            var command = new AddElevatorRequestCommand(request);
            var result = await _commandBus.SendAsync<AddElevatorRequestCommand, bool>(command);
            
            _logger.LogInformation("Successfully processed add request command for {RequestId}: {Result}", 
                request.Id, result);
                
            return result;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation failed for request {RequestId}: {Error}", request?.Id, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding request {RequestId}", request?.Id);
            return false;
        }
    }

    /// <summary>
    /// Processes elevator operations using command pattern
    /// </summary>
    /// <param name="elevator">The elevator to process</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    public async Task ProcessElevatorAsync(Elevator elevator, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Starting elevator processing for elevator {ElevatorId}", elevator.Id);

            var command = new ProcessElevatorCommand(elevator);
            await _commandBus.SendAsync(command, cancellationToken);
            
            _logger.LogInformation("Completed elevator processing for elevator {ElevatorId}", elevator.Id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Elevator {ElevatorId} processing was cancelled", elevator.Id);
            throw;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError("Validation failed for elevator {ElevatorId}: {Error}", elevator?.Id, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing elevator {ElevatorId}", elevator?.Id);
            throw;
        }
    }

    // Timer callback for periodic cleanup
    private void PerformPeriodicCleanup(object? state)
    {
        if (_disposed) return;
        
        try
        {
            // In the command-based architecture, cleanup would be handled by a separate command
            // For now, we'll keep this as a placeholder for future cleanup commands
            _logger.LogDebug("Periodic cleanup triggered (command-based implementation)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic cleanup");
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cleanupTimer?.Dispose();
            _logger.LogInformation("CommandBasedElevatorController disposed");
        }
    }
}