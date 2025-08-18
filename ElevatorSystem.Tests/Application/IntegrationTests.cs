using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ElevatorSystem.Tests.Application;

/// <summary>
/// Integration tests to verify Phase 3 components work together correctly
/// </summary>
public class IntegrationTests
{
    [Fact]
    public void RetryPolicyManager_BasicFunctionality_Works()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicyManager>>();
        var mockOptions = new Mock<IOptions<RequestProcessingSettings>>();
        var settings = new RequestProcessingSettings { MaxRetryAttempts = 3 };
        mockOptions.Setup(x => x.Value).Returns(settings);
        
        var retryManager = new RetryPolicyManager(mockOptions.Object, mockLogger.Object);
        var request = new ElevatorRequest(1, 5);
        
        // Act & Assert
        Assert.True(retryManager.ShouldRetry(request, 1));
        Assert.True(retryManager.ShouldRetry(request, 2));
        Assert.True(retryManager.ShouldRetry(request, 3));
        Assert.False(retryManager.ShouldRetry(request, 4));
        
        // Test circuit breaker initially closed
        Assert.False(retryManager.IsCircuitBreakerOpen());
        
        // Test statistics
        var stats = retryManager.GetStatistics();
        Assert.NotNull(stats);
        Assert.Equal(0, stats.TotalFailures);
        Assert.Equal(0, stats.TotalSuccesses);
    }

    [Fact]
    public void HealthMonitor_BasicFunctionality_Works()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<HealthMonitor>>();
        var mockRetryManager = new Mock<IRetryPolicyManager>();
        var mockStatusTracker = new Mock<IRequestStatusTracker>();
        mockStatusTracker.Setup(x => x.GetStatistics()).Returns(new RequestTrackingStatistics
        {
            TotalTrackedRequests = 0,
            ActiveRequests = 0,
            TimedOutRequests = 0
        });
        var mockOptions = new Mock<IOptions<RequestProcessingSettings>>();
        var settings = new RequestProcessingSettings { HealthCheckIntervalMs = 1000 };
        mockOptions.Setup(x => x.Value).Returns(settings);
        
        using var healthMonitor = new HealthMonitor(
            mockOptions.Object, 
            mockRetryManager.Object, 
            mockStatusTracker.Object, 
            mockLogger.Object);
        
        // Act
        healthMonitor.RecordSuccess("TestOperation", TimeSpan.FromMilliseconds(100));
        healthMonitor.RecordFailure("TestOperation", new Exception("Test"));
        
        // Assert
        var metrics = healthMonitor.GetPerformanceMetrics();
        Assert.Equal(2, metrics.TotalRequests);
        Assert.Equal(1, metrics.SuccessfulRequests);
        Assert.Equal(1, metrics.FailedRequests);
        
        var health = healthMonitor.GetHealthStatus();
        Assert.NotNull(health);
        
        var isHealthy = healthMonitor.IsHealthy();
        Assert.IsType<bool>(isHealthy);
    }

    [Fact]
    public void RequestProcessingSettings_Configuration_Works()
    {
        // Arrange & Act
        var settings = new RequestProcessingSettings();
        
        // Assert - Test default values
        Assert.Equal(30000, settings.RequestTimeoutMs);
        Assert.Equal(3, settings.MaxRetryAttempts);
        Assert.Equal(5, settings.CircuitBreakerFailureThreshold);
        
        // Test custom values
        settings.RequestTimeoutMs = 5000;
        settings.MaxRetryAttempts = 5;
        
        Assert.Equal(5000, settings.RequestTimeoutMs);
        Assert.Equal(5, settings.MaxRetryAttempts);
    }

    [Fact]
    public void ElevatorRequest_CreatedCorrectly_HasValidProperties()
    {
        // Arrange & Act
        var request = new ElevatorRequest(1, 10);
        
        // Assert
        Assert.Equal(1, request.CurrentFloor);
        Assert.Equal(10, request.DestinationFloor);
        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.True(request.RequestTime <= DateTime.UtcNow);
    }
}