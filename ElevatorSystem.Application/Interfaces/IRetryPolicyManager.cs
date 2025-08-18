using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Interfaces;

/// <summary>
/// Manages retry policies and circuit breaker logic for failed requests
/// </summary>
public interface IRetryPolicyManager
{
    /// <summary>
    /// Determines if a request should be retried based on failure count and circuit breaker state
    /// </summary>
    /// <param name="request">The elevator request that failed</param>
    /// <param name="attemptNumber">Current attempt number (1-based)</param>
    /// <returns>True if the request should be retried, false otherwise</returns>
    bool ShouldRetry(ElevatorRequest request, int attemptNumber);

    /// <summary>
    /// Calculates the delay before the next retry attempt using exponential backoff
    /// </summary>
    /// <param name="attemptNumber">Current attempt number (1-based)</param>
    /// <returns>Delay in milliseconds before next retry</returns>
    TimeSpan CalculateRetryDelay(int attemptNumber);

    /// <summary>
    /// Records a failure for circuit breaker monitoring
    /// </summary>
    /// <param name="requestId">ID of the failed request</param>
    /// <param name="exception">Exception that caused the failure</param>
    void RecordFailure(Guid requestId, Exception exception);

    /// <summary>
    /// Records a successful operation for circuit breaker monitoring
    /// </summary>
    /// <param name="requestId">ID of the successful request</param>
    void RecordSuccess(Guid requestId);

    /// <summary>
    /// Gets the current state of the circuit breaker
    /// </summary>
    /// <returns>True if circuit breaker is open (blocking requests), false otherwise</returns>
    bool IsCircuitBreakerOpen();

    /// <summary>
    /// Gets retry statistics for monitoring
    /// </summary>
    /// <returns>Retry policy statistics</returns>
    RetryPolicyStatistics GetStatistics();
}

/// <summary>
/// Statistics for retry policy monitoring
/// </summary>
public class RetryPolicyStatistics
{
    public int TotalFailures { get; set; }
    public int TotalSuccesses { get; set; }
    public int TotalRetries { get; set; }
    public bool CircuitBreakerOpen { get; set; }
    public DateTime? LastFailureTime { get; set; }
    public double SuccessRate => TotalSuccesses + TotalFailures > 0 
        ? (double)TotalSuccesses / (TotalSuccesses + TotalFailures) 
        : 0.0;
}