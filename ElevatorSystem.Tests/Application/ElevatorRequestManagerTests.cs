using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ElevatorSystem.Tests.Application;

/// <summary>
/// Unit tests for ElevatorRequestManager focusing on request handling and passenger operations.
/// </summary>
public class ElevatorRequestManagerTests
{
    private readonly Mock<IElevatorRequestRepository> _mockRepository;
    private readonly Mock<ILogger<ElevatorRequestManager>> _mockLogger;
    private readonly ElevatorRequestManager _requestManager;

    public ElevatorRequestManagerTests()
    {
        _mockRepository = new Mock<IElevatorRequestRepository>();
        _mockLogger = new Mock<ILogger<ElevatorRequestManager>>();
        _requestManager = new ElevatorRequestManager(_mockRepository.Object, _mockLogger.Object);
    }

    [Theory]
    [InlineData(3, ElevatorDirection.Up, true)]   // Pickup at current floor going up
    [InlineData(3, ElevatorDirection.Down, true)] // Dropoff at current floor
    [InlineData(5, ElevatorDirection.Up, false)]  // No requests at current floor
    public void ShouldStopAtCurrentFloor_WithVariousScenarios_ShouldReturnCorrectResult(
        int currentFloor, ElevatorDirection direction, bool expectedStop)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(currentFloor);
        elevator.SetDirection(direction);

        var requests = new List<ElevatorRequest>();
        
        if (expectedStop && direction == ElevatorDirection.Up)
        {
            // Add pickup request at current floor going up
            var pickupRequest = new ElevatorRequest(currentFloor, currentFloor + 2);
            pickupRequest.Status = ElevatorRequestStatus.Assigned;
            requests.Add(pickupRequest);
        }
        else if (expectedStop && direction == ElevatorDirection.Down)
        {
            // Add dropoff request at current floor
            var dropoffRequest = new ElevatorRequest(currentFloor - 2, currentFloor);
            dropoffRequest.Status = ElevatorRequestStatus.InProgress;
            requests.Add(dropoffRequest);
        }

        // Act
        var result = _requestManager.ShouldStopAtCurrentFloor(elevator, requests);

        // Assert
        result.Should().Be(expectedStop);
    }

    [Fact]
    public void ShouldStopAtCurrentFloor_WithWrongDirectionPickup_ShouldReturnFalse()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(5);
        elevator.SetDirection(ElevatorDirection.Up);

        // Passenger wants to go down but elevator is going up
        var request = new ElevatorRequest(5, 2);
        request.Status = ElevatorRequestStatus.Assigned;
        var requests = new List<ElevatorRequest> { request };

        // Act
        var result = _requestManager.ShouldStopAtCurrentFloor(elevator, requests);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessCurrentFloorActionsAsync_WithDropoffRequest_ShouldCompleteRequest()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(5);
        
        var dropoffRequest = new ElevatorRequest(3, 5);
        dropoffRequest.Status = ElevatorRequestStatus.InProgress;
        var requests = new List<ElevatorRequest> { dropoffRequest };
        var floorsNeedingService = new SortedSet<int>();

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
                      .Returns(Task.CompletedTask);

        // Act
        await _requestManager.ProcessCurrentFloorActionsAsync(elevator, requests, floorsNeedingService);

        // Assert
        dropoffRequest.Status.Should().Be(ElevatorRequestStatus.Completed);
        elevator.Status.Should().Be(ElevatorStatus.Loading);
        _mockRepository.Verify(r => r.UpdateAsync(dropoffRequest), Times.Once);
    }

    [Fact]
    public async Task ProcessCurrentFloorActionsAsync_WithPickupRequest_ShouldMarkInProgress()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(5);
        elevator.SetDirection(ElevatorDirection.Up);
        
        var pickupRequest = new ElevatorRequest(5, 8);
        pickupRequest.Status = ElevatorRequestStatus.Assigned;
        var requests = new List<ElevatorRequest> { pickupRequest };
        var floorsNeedingService = new SortedSet<int>();

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
                      .Returns(Task.CompletedTask);

        // Act
        await _requestManager.ProcessCurrentFloorActionsAsync(elevator, requests, floorsNeedingService);

        // Assert
        pickupRequest.Status.Should().Be(ElevatorRequestStatus.InProgress);
        floorsNeedingService.Should().Contain(8); // Destination floor added
        _mockRepository.Verify(r => r.UpdateAsync(pickupRequest), Times.Once);
    }

    [Fact]
    public async Task ProcessCurrentFloorActionsAsync_WithNoActions_ShouldNotDelay()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(5);
        
        var requests = new List<ElevatorRequest>();
        var floorsNeedingService = new SortedSet<int>();

        var startTime = DateTime.UtcNow;

        // Act
        await _requestManager.ProcessCurrentFloorActionsAsync(elevator, requests, floorsNeedingService);

        // Assert
        var duration = DateTime.UtcNow - startTime;
        duration.TotalMilliseconds.Should().BeLessThan(100); // Should not delay significantly
        elevator.Status.Should().Be(ElevatorStatus.Loading);
    }

    [Fact]
    public void RemoveCompletedRequests_ShouldRemoveOnlyCompletedRequests()
    {
        // Arrange
        var completedRequest = new ElevatorRequest(1, 5);
        completedRequest.Status = ElevatorRequestStatus.Completed;
        
        var activeRequest = new ElevatorRequest(3, 8);
        activeRequest.Status = ElevatorRequestStatus.InProgress;
        
        var requests = new List<ElevatorRequest> { completedRequest, activeRequest };
        var floorsNeedingService = new SortedSet<int> { 1, 3, 5, 8 };

        // Act
        _requestManager.RemoveCompletedRequests(requests, floorsNeedingService);

        // Assert
        requests.Should().HaveCount(1);
        requests.Should().Contain(activeRequest);
        requests.Should().NotContain(completedRequest);
        
        // Floors for active request should remain
        floorsNeedingService.Should().Contain(3); // Current floor of active request
        floorsNeedingService.Should().Contain(8); // Destination of active request
        
        // Floors for completed request should be removed
        floorsNeedingService.Should().NotContain(1); // Current floor of completed request
        floorsNeedingService.Should().NotContain(5); // Destination of completed request
    }

    [Fact]
    public void RemoveCompletedRequests_WithSharedFloors_ShouldKeepFloorsInUse()
    {
        // Arrange
        var completedRequest = new ElevatorRequest(1, 5);
        completedRequest.Status = ElevatorRequestStatus.Completed;
        
        var activeRequest = new ElevatorRequest(3, 5); // Same destination as completed
        activeRequest.Status = ElevatorRequestStatus.InProgress;
        
        var requests = new List<ElevatorRequest> { completedRequest, activeRequest };
        var floorsNeedingService = new SortedSet<int> { 1, 3, 5 };

        // Act
        _requestManager.RemoveCompletedRequests(requests, floorsNeedingService);

        // Assert
        floorsNeedingService.Should().Contain(5); // Kept because active request needs it
        floorsNeedingService.Should().Contain(3); // Kept because active request needs it
        floorsNeedingService.Should().NotContain(1); // Removed because only completed request used it
    }

    [Theory]
    [InlineData(5, 8, ElevatorDirection.Up, true)]   // Going up, destination up
    [InlineData(5, 2, ElevatorDirection.Down, true)] // Going down, destination down
    [InlineData(5, 8, ElevatorDirection.Down, false)] // Going up but elevator going down
    [InlineData(5, 2, ElevatorDirection.Up, false)]   // Going down but elevator going up
    public void ShouldStopAtCurrentFloor_WithDirectionMismatch_ShouldRespectElevatorDirection(
        int currentFloor, int destinationFloor, ElevatorDirection elevatorDirection, bool expectedStop)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(currentFloor);
        elevator.SetDirection(elevatorDirection);

        var request = new ElevatorRequest(currentFloor, destinationFloor);
        request.Status = ElevatorRequestStatus.Assigned;
        var requests = new List<ElevatorRequest> { request };

        // Act
        var result = _requestManager.ShouldStopAtCurrentFloor(elevator, requests);

        // Assert
        result.Should().Be(expectedStop);
    }

    [Fact]
    public async Task ProcessCurrentFloorActionsAsync_WithMultipleActions_ShouldProcessAll()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(5);
        elevator.SetDirection(ElevatorDirection.Up);
        
        var dropoffRequest = new ElevatorRequest(3, 5);
        dropoffRequest.Status = ElevatorRequestStatus.InProgress;
        
        var pickupRequest = new ElevatorRequest(5, 8);
        pickupRequest.Status = ElevatorRequestStatus.Assigned;
        
        var requests = new List<ElevatorRequest> { dropoffRequest, pickupRequest };
        var floorsNeedingService = new SortedSet<int>();

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
                      .Returns(Task.CompletedTask);

        // Act
        await _requestManager.ProcessCurrentFloorActionsAsync(elevator, requests, floorsNeedingService);

        // Assert
        dropoffRequest.Status.Should().Be(ElevatorRequestStatus.Completed);
        pickupRequest.Status.Should().Be(ElevatorRequestStatus.InProgress);
        floorsNeedingService.Should().Contain(8);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(-3, 5, ElevatorDirection.Up)]
    [InlineData(8, -2, ElevatorDirection.Down)]
    public async Task ProcessCurrentFloorActionsAsync_WithBasementFloors_ShouldHandleNegativeFloors(
        int currentFloor, int destinationFloor, ElevatorDirection direction)
    {
        // Arrange
        var elevator = new Elevator(1, -5, 10);
        elevator.MoveTo(currentFloor);
        elevator.SetDirection(direction);
        
        var request = new ElevatorRequest(currentFloor, destinationFloor);
        request.Status = ElevatorRequestStatus.Assigned;
        var requests = new List<ElevatorRequest> { request };
        var floorsNeedingService = new SortedSet<int>();

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
                      .Returns(Task.CompletedTask);

        // Act
        await _requestManager.ProcessCurrentFloorActionsAsync(elevator, requests, floorsNeedingService);

        // Assert
        request.Status.Should().Be(ElevatorRequestStatus.InProgress);
        floorsNeedingService.Should().Contain(destinationFloor);
    }
}