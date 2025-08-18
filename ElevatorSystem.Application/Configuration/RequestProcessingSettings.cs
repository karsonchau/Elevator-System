namespace ElevatorSystem.Application.Configuration;

/// <summary>
/// Configuration settings for robust request processing pipeline
/// </summary>
public class RequestProcessingSettings
{
    public const string SectionName = "RequestProcessing";

    /// <summary>
    /// Maximum time to wait for a request to complete before considering it timed out (milliseconds)
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 30000; // 30 seconds

    /// <summary>
    /// Maximum number of retry attempts for failed requests
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential backoff retry strategy (milliseconds)
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 1000; // 1 second

    /// <summary>
    /// Maximum delay between retry attempts (milliseconds)
    /// </summary>
    public int RetryMaxDelayMs { get; set; } = 8000; // 8 seconds

    /// <summary>
    /// Number of failures within the time window that triggers circuit breaker
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time window for circuit breaker failure counting (milliseconds)
    /// </summary>
    public int CircuitBreakerTimeWindowMs { get; set; } = 60000; // 60 seconds

    /// <summary>
    /// How long the circuit breaker stays open before attempting to close (milliseconds)
    /// </summary>
    public int CircuitBreakerOpenDurationMs { get; set; } = 30000; // 30 seconds

    /// <summary>
    /// Interval for health monitoring checks (milliseconds)
    /// </summary>
    public int HealthCheckIntervalMs { get; set; } = 5000; // 5 seconds

    /// <summary>
    /// Interval for checking request timeouts (milliseconds)
    /// </summary>
    public int TimeoutCheckIntervalMs { get; set; } = 10000; // 10 seconds
}