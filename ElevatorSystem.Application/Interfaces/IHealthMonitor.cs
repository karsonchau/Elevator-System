namespace ElevatorSystem.Application.Interfaces;

/// <summary>
/// Monitors system health and provides metrics for the elevator system
/// </summary>
public interface IHealthMonitor
{
    /// <summary>
    /// Gets the current health status of the elevator system
    /// </summary>
    /// <returns>System health information</returns>
    SystemHealthStatus GetHealthStatus();

    /// <summary>
    /// Records a successful operation for health tracking
    /// </summary>
    /// <param name="operationType">Type of operation that succeeded</param>
    /// <param name="duration">Time taken for the operation</param>
    void RecordSuccess(string operationType, TimeSpan duration);

    /// <summary>
    /// Records a failed operation for health tracking
    /// </summary>
    /// <param name="operationType">Type of operation that failed</param>
    /// <param name="exception">Exception that caused the failure</param>
    void RecordFailure(string operationType, Exception exception);

    /// <summary>
    /// Gets performance metrics for the system
    /// </summary>
    /// <returns>Performance metrics</returns>
    PerformanceMetrics GetPerformanceMetrics();

    /// <summary>
    /// Checks if the system is healthy based on current metrics
    /// </summary>
    /// <returns>True if system is healthy, false otherwise</returns>
    bool IsHealthy();
}

/// <summary>
/// Overall system health status
/// </summary>
public class SystemHealthStatus
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = "Unknown";
    public DateTime LastChecked { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// Performance metrics for monitoring
/// </summary>
public class PerformanceMetrics
{
    public double RequestSuccessRate { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public int ActiveRequests { get; set; }
    public DateTime MetricsPeriodStart { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, int> OperationCounts { get; set; } = new();
    public Dictionary<string, double> AverageOperationTimes { get; set; } = new();
}