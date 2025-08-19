using ElevatorSystem.Application.Events;
using ElevatorSystem.Application.Events.ElevatorCommands;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Services;

/// <summary>
/// Production-ready elevator controller with comprehensive failure handling and monitoring
/// </summary>
public class ElevatorController : IElevatorController, IDisposable
{
    private readonly ICommandBus _commandBus;
    private readonly IRequestStatusTracker _statusTracker;
    private readonly IHealthMonitor _healthMonitor;
    private readonly IElevatorRepository _elevatorRepository;
    private readonly ILogger<ElevatorController> _logger;
    private volatile bool _disposed = false;

    public ElevatorController(
        ICommandBus commandBus,
        IRequestStatusTracker statusTracker,
        IHealthMonitor healthMonitor,
        IElevatorRepository elevatorRepository,
        ILogger<ElevatorController> logger)
    {
        _commandBus = commandBus;
        _statusTracker = statusTracker;
        _healthMonitor = healthMonitor;
        _elevatorRepository = elevatorRepository;
        _logger = logger;
    }

    /// <summary>
    /// Adds an elevator request using the robust processing pipeline
    /// </summary>
    /// <param name="request">The elevator request to add</param>
    /// <returns>True if the request was successfully submitted to the pipeline, false otherwise</returns>
    public async Task<bool> AddRequestAsync(ElevatorRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Submitting elevator request {RequestId} from floor {CurrentFloor} to {DestinationFloor} to processing pipeline", 
                request.Id, request.CurrentFloor, request.DestinationFloor);

            // Start tracking the request for timeout monitoring
            await _statusTracker.StartTrackingAsync(request);

            // Submit through the robust pipeline
            var command = new SubmitElevatorRequestCommand(request);
            var result = await _commandBus.SendAsync<SubmitElevatorRequestCommand, bool>(command);
            
            stopwatch.Stop();
            
            if (result)
            {
                _healthMonitor.RecordSuccess("AddRequest", stopwatch.Elapsed);
                _logger.LogInformation("Successfully submitted elevator request {RequestId} to processing pipeline", request.Id);
            }
            else
            {
                // Check if this is a validation failure by checking if any elevator can serve this request
                var elevators = await _elevatorRepository.GetAllAsync();
                var isValidationFailure = elevators.All(e => !e.CanServeFloor(request.CurrentFloor) || !e.CanServeFloor(request.DestinationFloor));
                
                if (!isValidationFailure)
                {
                    // Only record non-validation failures as health monitor failures
                    _healthMonitor.RecordFailure("AddRequest", new InvalidOperationException("Request submission failed"));
                }
                
                _logger.LogWarning("Failed to submit elevator request {RequestId} to processing pipeline", request.Id);
            }
                
            return result;
        }
        catch (ArgumentException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning("Validation failed for request {RequestId}: {Error}", request?.Id, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _healthMonitor.RecordFailure("AddRequest", ex);
            _logger.LogError(ex, "Unexpected error submitting request {RequestId} to robust pipeline", request?.Id);
            return false;
        }
    }

    /// <summary>
    /// Processes elevator operations using the existing command infrastructure
    /// </summary>
    /// <param name="elevator">The elevator to process</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    public async Task ProcessElevatorAsync(Elevator elevator, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Starting elevator processing for elevator {ElevatorId}", elevator.Id);

            // Check system health before processing
            if (!_healthMonitor.IsHealthy())
            {
                var healthStatus = _healthMonitor.GetHealthStatus();
                _logger.LogWarning("System health is {Status}, but continuing with elevator processing for {ElevatorId}. Issues: {Issues}", 
                    healthStatus.Status, elevator.Id, string.Join(", ", healthStatus.Issues));
            }

            // Process elevator operations via command pipeline
            var command = new ProcessElevatorCommand(elevator);
            await _commandBus.SendAsync(command, cancellationToken);
            
            stopwatch.Stop();
            _healthMonitor.RecordSuccess("ProcessElevator", stopwatch.Elapsed);
            
            _logger.LogInformation("Completed elevator processing for elevator {ElevatorId}", elevator.Id);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogInformation("Elevator {ElevatorId} processing was cancelled", elevator.Id);
            throw;
        }
        catch (ArgumentException ex)
        {
            stopwatch.Stop();
            _healthMonitor.RecordFailure("ProcessElevator", ex);
            _logger.LogError("Validation failed for elevator {ElevatorId}: {Error}", elevator?.Id, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _healthMonitor.RecordFailure("ProcessElevator", ex);
            _logger.LogError(ex, "Unexpected error processing elevator {ElevatorId}", elevator?.Id);
            throw;
        }
    }

    /// <summary>
    /// Gets the current health status of the elevator system
    /// </summary>
    /// <returns>System health information</returns>
    public SystemHealthStatus GetSystemHealth()
    {
        return _healthMonitor.GetHealthStatus();
    }

    /// <summary>
    /// Gets performance metrics for monitoring
    /// </summary>
    /// <returns>Current performance metrics</returns>
    public PerformanceMetrics GetPerformanceMetrics()
    {
        return _healthMonitor.GetPerformanceMetrics();
    }

    /// <summary>
    /// Gets request tracking statistics
    /// </summary>
    /// <returns>Request tracking statistics</returns>
    public RequestTrackingStatistics GetRequestStatistics()
    {
        return _statusTracker.GetStatistics();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogInformation("ElevatorController disposed");
        }
    }
}