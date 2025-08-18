using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ElevatorSystem.Application.Services;

/// <summary>
/// Production-ready retry policy manager with circuit breaker pattern
/// </summary>
public class RetryPolicyManager : IRetryPolicyManager
{
    private readonly RequestProcessingSettings _settings;
    private readonly ILogger<RetryPolicyManager> _logger;
    
    // Circuit breaker state
    private readonly ConcurrentQueue<DateTime> _recentFailures = new();
    private volatile bool _circuitBreakerOpen = false;
    private DateTime? _circuitBreakerOpenedAt;
    
    // Statistics tracking
    private readonly object _statsLock = new();
    private int _totalFailures = 0;
    private int _totalSuccesses = 0;
    private int _totalRetries = 0;
    private DateTime? _lastFailureTime;

    public RetryPolicyManager(
        IOptions<RequestProcessingSettings> settings,
        ILogger<RetryPolicyManager> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public bool ShouldRetry(ElevatorRequest request, int attemptNumber)
    {
        if (request == null)
            return false;

        // Check if circuit breaker is open
        if (IsCircuitBreakerOpen())
        {
            _logger.LogWarning("Circuit breaker is open, rejecting retry for request {RequestId}", request.Id);
            return false;
        }

        // Check if we've exceeded max retry attempts
        if (attemptNumber > _settings.MaxRetryAttempts)
        {
            _logger.LogWarning("Request {RequestId} has exceeded maximum retry attempts ({MaxAttempts})", 
                request.Id, _settings.MaxRetryAttempts);
            return false;
        }

        _logger.LogInformation("Request {RequestId} will be retried (attempt {AttemptNumber}/{MaxAttempts})", 
            request.Id, attemptNumber, _settings.MaxRetryAttempts);
        
        lock (_statsLock)
        {
            _totalRetries++;
        }
        
        return true;
    }

    public TimeSpan CalculateRetryDelay(int attemptNumber)
    {
        // Exponential backoff: 1s, 2s, 4s, 8s (capped at max delay)
        var delayMs = Math.Min(
            _settings.RetryBaseDelayMs * Math.Pow(2, attemptNumber - 1),
            _settings.RetryMaxDelayMs
        );

        var delay = TimeSpan.FromMilliseconds(delayMs);
        
        _logger.LogDebug("Calculated retry delay for attempt {AttemptNumber}: {DelayMs}ms", 
            attemptNumber, delay.TotalMilliseconds);
        
        return delay;
    }

    public void RecordFailure(Guid requestId, Exception exception)
    {
        var now = DateTime.UtcNow;
        
        _recentFailures.Enqueue(now);
        
        lock (_statsLock)
        {
            _totalFailures++;
            _lastFailureTime = now;
        }

        _logger.LogError(exception, "Recorded failure for request {RequestId}: {ErrorMessage}", 
            requestId, exception.Message);

        // Check if we should open the circuit breaker
        CheckCircuitBreakerState();
    }

    public void RecordSuccess(Guid requestId)
    {
        lock (_statsLock)
        {
            _totalSuccesses++;
        }

        _logger.LogDebug("Recorded success for request {RequestId}", requestId);

        // Success may help close the circuit breaker
        if (_circuitBreakerOpen)
        {
            CheckCircuitBreakerState();
        }
    }

    public bool IsCircuitBreakerOpen()
    {
        // If circuit breaker is closed, check if it should be opened
        if (!_circuitBreakerOpen)
        {
            CheckCircuitBreakerState();
        }
        // If circuit breaker is open, check if it should be closed
        else if (_circuitBreakerOpenedAt.HasValue && 
                 DateTime.UtcNow - _circuitBreakerOpenedAt.Value > TimeSpan.FromMilliseconds(_settings.CircuitBreakerOpenDurationMs))
        {
            _logger.LogInformation("Circuit breaker timeout reached, attempting to close circuit breaker");
            CloseCircuitBreaker();
        }

        return _circuitBreakerOpen;
    }

    public RetryPolicyStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new RetryPolicyStatistics
            {
                TotalFailures = _totalFailures,
                TotalSuccesses = _totalSuccesses,
                TotalRetries = _totalRetries,
                CircuitBreakerOpen = _circuitBreakerOpen,
                LastFailureTime = _lastFailureTime
            };
        }
    }

    private void CheckCircuitBreakerState()
    {
        var now = DateTime.UtcNow;
        var timeWindow = TimeSpan.FromMilliseconds(_settings.CircuitBreakerTimeWindowMs);

        // Clean up old failures outside the time window
        while (_recentFailures.TryPeek(out var oldestFailure) && 
               now - oldestFailure > timeWindow)
        {
            _recentFailures.TryDequeue(out _);
        }

        var recentFailureCount = _recentFailures.Count;

        // Open circuit breaker if threshold exceeded
        if (!_circuitBreakerOpen && recentFailureCount >= _settings.CircuitBreakerFailureThreshold)
        {
            OpenCircuitBreaker();
        }
        // Close circuit breaker if failure count drops below threshold
        else if (_circuitBreakerOpen && recentFailureCount < _settings.CircuitBreakerFailureThreshold)
        {
            CloseCircuitBreaker();
        }
    }

    private void OpenCircuitBreaker()
    {
        _circuitBreakerOpen = true;
        _circuitBreakerOpenedAt = DateTime.UtcNow;
        
        _logger.LogWarning("Circuit breaker opened due to {FailureCount} failures within {TimeWindowMs}ms", 
            _recentFailures.Count, _settings.CircuitBreakerTimeWindowMs);
    }

    private void CloseCircuitBreaker()
    {
        _circuitBreakerOpen = false;
        _circuitBreakerOpenedAt = null;
        
        _logger.LogInformation("Circuit breaker closed - system appears to be recovering");
    }
}