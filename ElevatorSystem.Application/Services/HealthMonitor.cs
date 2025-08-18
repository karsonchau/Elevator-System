using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ElevatorSystem.Application.Services;

/// <summary>
/// Production-ready health monitor for the elevator system
/// </summary>
public class HealthMonitor : IHealthMonitor, IDisposable
{
    private readonly RequestProcessingSettings _settings;
    private readonly IRetryPolicyManager _retryPolicyManager;
    private readonly IRequestStatusTracker _requestStatusTracker;
    private readonly ILogger<HealthMonitor> _logger;
    private readonly Timer _healthCheckTimer;
    
    // Metrics tracking
    private readonly ConcurrentDictionary<string, OperationMetrics> _operationMetrics = new();
    private readonly object _metricsLock = new();
    private readonly DateTime _metricsStartTime = DateTime.UtcNow;
    private volatile bool _disposed = false;
    private SystemHealthStatus _lastHealthStatus = new();

    public HealthMonitor(
        IOptions<RequestProcessingSettings> settings,
        IRetryPolicyManager retryPolicyManager,
        IRequestStatusTracker requestStatusTracker,
        ILogger<HealthMonitor> logger)
    {
        _settings = settings.Value;
        _retryPolicyManager = retryPolicyManager;
        _requestStatusTracker = requestStatusTracker;
        _logger = logger;
        
        // Start periodic health checks
        _healthCheckTimer = new Timer(PerformHealthCheck, null,
            TimeSpan.FromMilliseconds(_settings.HealthCheckIntervalMs),
            TimeSpan.FromMilliseconds(_settings.HealthCheckIntervalMs));
    }

    public SystemHealthStatus GetHealthStatus()
    {
        return _lastHealthStatus;
    }

    public void RecordSuccess(string operationType, TimeSpan duration)
    {
        var metrics = _operationMetrics.GetOrAdd(operationType, _ => new OperationMetrics());
        
        lock (metrics.Lock)
        {
            metrics.SuccessCount++;
            metrics.TotalDuration += duration;
            metrics.LastSuccess = DateTime.UtcNow;
        }

        _logger.LogDebug("Recorded success for {OperationType} in {DurationMs}ms", 
            operationType, duration.TotalMilliseconds);
    }

    public void RecordFailure(string operationType, Exception exception)
    {
        var metrics = _operationMetrics.GetOrAdd(operationType, _ => new OperationMetrics());
        
        lock (metrics.Lock)
        {
            metrics.FailureCount++;
            metrics.LastFailure = DateTime.UtcNow;
            metrics.LastException = exception;
        }

        _logger.LogWarning(exception, "Recorded failure for {OperationType}: {ErrorMessage}", 
            operationType, exception.Message);
    }

    public PerformanceMetrics GetPerformanceMetrics()
    {
        var totalRequests = 0;
        var successfulRequests = 0;
        var failedRequests = 0;
        var totalDuration = TimeSpan.Zero;
        var operationCounts = new Dictionary<string, int>();
        var averageOperationTimes = new Dictionary<string, double>();

        foreach (var kvp in _operationMetrics)
        {
            var operationType = kvp.Key;
            var metrics = kvp.Value;
            
            lock (metrics.Lock)
            {
                var opTotal = metrics.SuccessCount + metrics.FailureCount;
                totalRequests += opTotal;
                successfulRequests += metrics.SuccessCount;
                failedRequests += metrics.FailureCount;
                totalDuration += metrics.TotalDuration;
                
                operationCounts[operationType] = opTotal;
                averageOperationTimes[operationType] = metrics.SuccessCount > 0 
                    ? metrics.TotalDuration.TotalMilliseconds / metrics.SuccessCount 
                    : 0;
            }
        }

        var requestStats = _requestStatusTracker.GetStatistics();

        return new PerformanceMetrics
        {
            RequestSuccessRate = totalRequests > 0 ? (double)successfulRequests / totalRequests : 1.0,
            AverageResponseTimeMs = successfulRequests > 0 ? totalDuration.TotalMilliseconds / successfulRequests : 0,
            TotalRequests = totalRequests,
            SuccessfulRequests = successfulRequests,
            FailedRequests = failedRequests,
            ActiveRequests = requestStats.ActiveRequests,
            MetricsPeriodStart = _metricsStartTime,
            LastUpdated = DateTime.UtcNow,
            OperationCounts = operationCounts,
            AverageOperationTimes = averageOperationTimes
        };
    }

    public bool IsHealthy()
    {
        var healthStatus = GenerateHealthStatus();
        return healthStatus.IsHealthy;
    }

    private void PerformHealthCheck(object? state)
    {
        if (_disposed)
            return;

        try
        {
            _lastHealthStatus = GenerateHealthStatus();
            
            if (!_lastHealthStatus.IsHealthy)
            {
                _logger.LogWarning("System health check failed: {Status}. Issues: {Issues}", 
                    _lastHealthStatus.Status, string.Join(", ", _lastHealthStatus.Issues));
            }
            else
            {
                _logger.LogDebug("System health check passed: {Status}", _lastHealthStatus.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing health check");
        }
    }

    private SystemHealthStatus GenerateHealthStatus()
    {
        var status = new SystemHealthStatus
        {
            LastChecked = DateTime.UtcNow,
            Details = new Dictionary<string, object>(),
            Issues = new List<string>()
        };

        try
        {
            // Check retry policy health
            var retryStats = _retryPolicyManager.GetStatistics();
            status.Details["RetryPolicy"] = retryStats;
            
            if (retryStats.CircuitBreakerOpen)
            {
                status.Issues.Add("Circuit breaker is open - system may be experiencing failures");
            }

            if (retryStats.SuccessRate < 0.8) // Less than 80% success rate
            {
                status.Issues.Add($"Low success rate: {retryStats.SuccessRate:P1}");
            }

            // Check request tracking health
            var requestStats = _requestStatusTracker.GetStatistics();
            status.Details["RequestTracking"] = requestStats;
            
            if (requestStats.TimedOutRequests > 0)
            {
                status.Issues.Add($"{requestStats.TimedOutRequests} requests have timed out");
            }

            if (requestStats.ActiveRequests > 100) // Arbitrary threshold
            {
                status.Issues.Add($"High number of active requests: {requestStats.ActiveRequests}");
            }

            // Check performance metrics
            var performance = GetPerformanceMetrics();
            status.Details["Performance"] = performance;
            
            if (performance.RequestSuccessRate < 0.9) // Less than 90% success rate
            {
                status.Issues.Add($"Low request success rate: {performance.RequestSuccessRate:P1}");
            }

            if (performance.AverageResponseTimeMs > 10000) // More than 10 seconds average
            {
                status.Issues.Add($"High average response time: {performance.AverageResponseTimeMs:F0}ms");
            }

            // Determine overall health status
            if (status.Issues.Count == 0)
            {
                status.IsHealthy = true;
                status.Status = "Healthy";
            }
            else if (status.Issues.Count <= 2)
            {
                status.IsHealthy = false;
                status.Status = "Degraded";
            }
            else
            {
                status.IsHealthy = false;
                status.Status = "Unhealthy";
            }
        }
        catch (Exception ex)
        {
            status.IsHealthy = false;
            status.Status = "Error";
            status.Issues.Add($"Health check error: {ex.Message}");
            
            _logger.LogError(ex, "Error generating health status");
        }

        return status;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _healthCheckTimer?.Dispose();
            _logger.LogInformation("HealthMonitor disposed");
        }
    }

    private class OperationMetrics
    {
        public readonly object Lock = new();
        public int SuccessCount = 0;
        public int FailureCount = 0;
        public TimeSpan TotalDuration = TimeSpan.Zero;
        public DateTime? LastSuccess;
        public DateTime? LastFailure;
        public Exception? LastException;
    }
}