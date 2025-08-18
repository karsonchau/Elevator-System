using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ElevatorSystem.Tests.Application;

public class RetryPolicyManagerTests
{
    private readonly Mock<ILogger<RetryPolicyManager>> _mockLogger;
    private readonly Mock<IOptions<RequestProcessingSettings>> _mockOptions;
    private readonly RequestProcessingSettings _settings;
    private readonly RetryPolicyManager _retryPolicyManager;

    public RetryPolicyManagerTests()
    {
        _mockLogger = new Mock<ILogger<RetryPolicyManager>>();
        _mockOptions = new Mock<IOptions<RequestProcessingSettings>>();
        _settings = new RequestProcessingSettings
        {
            MaxRetryAttempts = 3,
            CircuitBreakerFailureThreshold = 5
        };
        _mockOptions.Setup(x => x.Value).Returns(_settings);
        _retryPolicyManager = new RetryPolicyManager(_mockOptions.Object, _mockLogger.Object);
    }

    [Fact]
    public void ShouldRetry_WithinMaxAttempts_ReturnsTrue()
    {
        // Arrange
        var request = new ElevatorRequest(1, 5);
        
        // Act & Assert
        Assert.True(_retryPolicyManager.ShouldRetry(request, 1));
        Assert.True(_retryPolicyManager.ShouldRetry(request, 2));
        Assert.True(_retryPolicyManager.ShouldRetry(request, 3));
    }

    [Fact]
    public void ShouldRetry_ExceedsMaxAttempts_ReturnsFalse()
    {
        // Arrange
        var request = new ElevatorRequest(1, 5);
        
        // Act & Assert
        Assert.False(_retryPolicyManager.ShouldRetry(request, 4));
        Assert.False(_retryPolicyManager.ShouldRetry(request, 10));
    }

    [Fact]
    public void ShouldRetry_ZeroAttempts_ReturnsTrue()
    {
        // Arrange
        var request = new ElevatorRequest(1, 5);
        
        // Act & Assert - 0 attempts is still <= MaxRetryAttempts (3)
        Assert.True(_retryPolicyManager.ShouldRetry(request, 0));
    }

    [Theory]
    [InlineData(1, 1000)] // 1 second (1000 * 2^0)
    [InlineData(2, 2000)] // 2 seconds (1000 * 2^1)
    [InlineData(3, 4000)] // 4 seconds (1000 * 2^2)
    [InlineData(4, 8000)] // 8 seconds (1000 * 2^3, capped at max)
    [InlineData(5, 8000)] // 8 seconds (capped at RetryMaxDelayMs)
    public void CalculateRetryDelay_ExponentialBackoff_ReturnsCorrectDelay(int attemptNumber, int expectedMs)
    {
        // Act
        var delay = _retryPolicyManager.CalculateRetryDelay(attemptNumber);
        
        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), delay);
    }

    [Fact]
    public void CalculateRetryDelay_ZeroAttempts_ReturnsBaseDelay()
    {
        // Act
        var delay = _retryPolicyManager.CalculateRetryDelay(0);
        
        // Assert - For attempt 0, formula gives 1000 * 2^(-1) = 500ms
        Assert.Equal(TimeSpan.FromMilliseconds(500), delay);
    }

    [Fact]
    public void RecordFailure_FirstFailure_DoesNotTriggerCircuitBreaker()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var exception = new InvalidOperationException("Test exception");
        
        // Act
        _retryPolicyManager.RecordFailure(requestId, exception);
        
        // Assert - Should not affect retry logic yet
        var request = new ElevatorRequest(1, 5);
        Assert.True(_retryPolicyManager.ShouldRetry(request, 1));
    }

    [Fact]
    public void RecordFailure_MultipleFailures_TriggersCircuitBreakerEventually()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var request = new ElevatorRequest(1, 5);
        
        // Record failures up to threshold
        for (int i = 0; i < _settings.CircuitBreakerFailureThreshold; i++)
        {
            _retryPolicyManager.RecordFailure(Guid.NewGuid(), exception);
        }
        
        // Act & Assert - Circuit breaker should be open now
        Assert.False(_retryPolicyManager.ShouldRetry(request, 1));
    }

    [Fact]
    public void IsCircuitBreakerOpen_InitialState_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(_retryPolicyManager.IsCircuitBreakerOpen());
    }

    [Fact]
    public void IsCircuitBreakerOpen_AfterThresholdFailures_ReturnsTrue()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        
        // Record failures up to threshold
        for (int i = 0; i < _settings.CircuitBreakerFailureThreshold; i++)
        {
            _retryPolicyManager.RecordFailure(Guid.NewGuid(), exception);
        }
        
        // Act & Assert
        Assert.True(_retryPolicyManager.IsCircuitBreakerOpen());
    }

    [Fact]
    public void RecordSuccess_AfterFailures_ResetsFailureCount()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var exception = new InvalidOperationException("Test exception");
        
        // Record some failures (but not enough to trigger circuit breaker)
        for (int i = 0; i < _settings.CircuitBreakerFailureThreshold - 1; i++)
        {
            _retryPolicyManager.RecordFailure(Guid.NewGuid(), exception);
        }
        
        // Act - Record success
        _retryPolicyManager.RecordSuccess(requestId);
        
        // Assert - Should be able to retry normally
        var request = new ElevatorRequest(1, 5);
        Assert.True(_retryPolicyManager.ShouldRetry(request, 1));
        Assert.False(_retryPolicyManager.IsCircuitBreakerOpen());
    }

    [Fact]
    public void GetStatistics_InitialState_ReturnsZeroValues()
    {
        // Act
        var stats = _retryPolicyManager.GetStatistics();
        
        // Assert
        Assert.Equal(0, stats.TotalFailures);
        Assert.Equal(0, stats.TotalSuccesses);
        Assert.Equal(0, stats.TotalRetries);
        Assert.False(stats.CircuitBreakerOpen);
        Assert.Null(stats.LastFailureTime);
    }

    [Fact]
    public void GetStatistics_AfterOperations_ReturnsCorrectValues()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        
        // Act
        _retryPolicyManager.RecordFailure(Guid.NewGuid(), exception);
        _retryPolicyManager.RecordFailure(Guid.NewGuid(), exception);
        _retryPolicyManager.RecordSuccess(Guid.NewGuid());
        
        var stats = _retryPolicyManager.GetStatistics();
        
        // Assert
        Assert.Equal(2, stats.TotalFailures);
        Assert.Equal(1, stats.TotalSuccesses);
        Assert.NotNull(stats.LastFailureTime);
        Assert.True(stats.SuccessRate > 0);
    }
}