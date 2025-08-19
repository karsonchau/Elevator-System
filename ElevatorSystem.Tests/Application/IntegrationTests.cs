using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Events;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using ElevatorSystem.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ElevatorSystem.Tests.Application;

/// <summary>
/// Integration tests to verify robust processing components work together correctly
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

    /// <summary>
    /// Edge Case Tests - Invalid Floor Requests and System Resilience
    /// </summary>
    [Theory]
    [InlineData(-10, 5)] // Negative current floor
    [InlineData(5, -10)] // Negative destination floor
    [InlineData(0, 5)]   // Zero current floor
    [InlineData(5, 0)]   // Zero destination floor
    [InlineData(15, 5)]  // Current floor above building range
    [InlineData(5, 15)]  // Destination floor above building range
    public void ElevatorRequest_WithInvalidFloors_ShouldAllowCreationButFailValidation(int currentFloor, int destinationFloor)
    {
        // Arrange & Act - Domain objects should be creatable with any values
        var request = new ElevatorRequest(currentFloor, destinationFloor);
        
        // Assert - Object creation should succeed (validation happens at application layer)
        Assert.Equal(currentFloor, request.CurrentFloor);
        Assert.Equal(destinationFloor, request.DestinationFloor);
        Assert.NotEqual(Guid.Empty, request.Id);
    }

    [Fact]
    public void Elevator_CanServeFloor_WithInvalidFloors_ShouldReturnFalse()
    {
        // Arrange
        var elevator = new Elevator(1, minFloor: 1, maxFloor: 10, floorMovementTimeMs: 1000, loadingTimeMs: 1000);

        // Act & Assert - Test boundary conditions
        Assert.False(elevator.CanServeFloor(-5));  // Below minimum
        Assert.False(elevator.CanServeFloor(0));   // Still below minimum  
        Assert.True(elevator.CanServeFloor(1));    // At minimum
        Assert.True(elevator.CanServeFloor(5));    // Valid middle floor
        Assert.True(elevator.CanServeFloor(10));   // At maximum
        Assert.False(elevator.CanServeFloor(11));  // Above maximum
        Assert.False(elevator.CanServeFloor(15));  // Well above maximum
    }

    [Fact]
    public void Elevator_MoveTo_WithInvalidFloor_ShouldThrowException()
    {
        // Arrange
        var elevator = new Elevator(1, minFloor: 1, maxFloor: 10, floorMovementTimeMs: 1000, loadingTimeMs: 1000);

        // Act & Assert - Moving to invalid floors should throw
        Assert.Throws<ArgumentOutOfRangeException>(() => elevator.MoveTo(-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => elevator.MoveTo(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => elevator.MoveTo(15));
        
        // Valid moves should work
        elevator.MoveTo(5);
        Assert.Equal(5, elevator.CurrentFloor);
    }

    [Fact]
    public async Task ElevatorMovementService_WithInvalidTargetFloor_ShouldThrowException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ElevatorMovementService>>();
        var elevatorRepository = new InMemoryElevatorRepository();
        var mockEventBus = new Mock<IEventBus>();
        
        var elevator = new Elevator(1, minFloor: 1, maxFloor: 10, floorMovementTimeMs: 100, loadingTimeMs: 100);
        await elevatorRepository.AddAsync(elevator);
        
        var movementService = new ElevatorMovementService(elevatorRepository, mockLogger.Object, mockEventBus.Object);

        // Act & Assert - Moving to invalid floor should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            movementService.MoveToFloorAsync(elevator, -5, CancellationToken.None));
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            movementService.MoveToFloorAsync(elevator, 15, CancellationToken.None));
    }

    [Fact]
    public void ElevatorMovementService_FindNextFloorInDirection_WithNoValidFloors_ShouldReturnNull()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ElevatorMovementService>>();
        var elevatorRepository = new InMemoryElevatorRepository();
        var mockEventBus = new Mock<IEventBus>();
        
        var elevator = new Elevator(1, minFloor: 1, maxFloor: 10, floorMovementTimeMs: 100, loadingTimeMs: 100);
        elevator.MoveTo(5); // Set elevator at floor 5
        
        var movementService = new ElevatorMovementService(elevatorRepository, mockLogger.Object, mockEventBus.Object);
        
        // Test with no floors needing service
        var emptyFloors = new SortedSet<int>();

        // Act & Assert
        var nextFloorUp = movementService.FindNextFloorInDirection(elevator, emptyFloors, ElevatorDirection.Up);
        var nextFloorDown = movementService.FindNextFloorInDirection(elevator, emptyFloors, ElevatorDirection.Down);
        
        Assert.Null(nextFloorUp);
        Assert.Null(nextFloorDown);
    }

    [Fact]
    public void ElevatorMovementService_FindNextFloorInDirection_WithOnlyInvalidFloors_ShouldReturnInvalidFloor()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ElevatorMovementService>>();
        var elevatorRepository = new InMemoryElevatorRepository();
        var mockEventBus = new Mock<IEventBus>();
        
        var elevator = new Elevator(1, minFloor: 1, maxFloor: 10, floorMovementTimeMs: 100, loadingTimeMs: 100);
        elevator.MoveTo(5); // Set elevator at floor 5
        
        var movementService = new ElevatorMovementService(elevatorRepository, mockLogger.Object, mockEventBus.Object);
        
        // Test with floors outside elevator range
        var invalidFloors = new SortedSet<int> { -5, 0, 15, 20 };

        // Act & Assert - Method finds next floor regardless of validity (validation happens elsewhere)
        var nextFloorUp = movementService.FindNextFloorInDirection(elevator, invalidFloors, ElevatorDirection.Up);
        var nextFloorDown = movementService.FindNextFloorInDirection(elevator, invalidFloors, ElevatorDirection.Down);
        
        // Should return the invalid floors (15 for up, 0 for down) - validation happens in ProcessElevatorCommandHandler
        Assert.Equal(15, nextFloorUp); // First floor > 5 in the set
        Assert.Equal(0, nextFloorDown); // Last floor < 5 in the set
    }

    [Fact]
    public void ElevatorMovementService_DetermineOptimalDirection_WithInvalidRequest_ShouldHandleGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ElevatorMovementService>>();
        var elevatorRepository = new InMemoryElevatorRepository();
        var mockEventBus = new Mock<IEventBus>();
        
        var elevator = new Elevator(1, minFloor: 1, maxFloor: 10, floorMovementTimeMs: 100, loadingTimeMs: 100);
        elevator.MoveTo(5);
        elevator.SetDirection(ElevatorDirection.Idle);
        
        var movementService = new ElevatorMovementService(elevatorRepository, mockLogger.Object, mockEventBus.Object);
        
        // Test with request that has invalid floors (but should still work for direction calculation)
        var invalidRequest = new ElevatorRequest(-5, 15);
        var requests = new List<ElevatorRequest> { invalidRequest };

        // Act - Should handle gracefully and return direction based on calculation
        var direction = movementService.DetermineOptimalDirection(elevator, requests);
        
        // Assert - Should return Down since -5 < 5 (current floor), closest request is at -5
        Assert.Equal(ElevatorDirection.Down, direction);
    }

    [Fact]
    public void RetryPolicyManager_WithFailures_ShouldEventuallyStopRetrying()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicyManager>>();
        var mockOptions = new Mock<IOptions<RequestProcessingSettings>>();
        var settings = new RequestProcessingSettings { MaxRetryAttempts = 3 };
        mockOptions.Setup(x => x.Value).Returns(settings);
        
        var retryManager = new RetryPolicyManager(mockOptions.Object, mockLogger.Object);
        var request = new ElevatorRequest(-7, 2); // Invalid request

        // Act & Assert - Test retry limits with invalid request
        Assert.True(retryManager.ShouldRetry(request, 1));
        Assert.True(retryManager.ShouldRetry(request, 2));
        Assert.True(retryManager.ShouldRetry(request, 3));
        Assert.False(retryManager.ShouldRetry(request, 4)); // Should stop after max retries
    }

    [Fact]
    public void HealthMonitor_WithManyFailures_ShouldReportUnhealthy()
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

        // Act - Record many failures (simulating invalid requests causing issues)
        for (int i = 0; i < 10; i++)
        {
            healthMonitor.RecordFailure("InvalidRequest", new ArgumentException("Invalid floor"));
        }

        // Assert - System should be unhealthy with high failure rate
        var metrics = healthMonitor.GetPerformanceMetrics();
        Assert.Equal(10, metrics.FailedRequests);
        Assert.Equal(0, metrics.SuccessfulRequests);
        
        var isHealthy = healthMonitor.IsHealthy();
        Assert.False(isHealthy); // Should be unhealthy with 100% failure rate
    }
}