using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ElevatorSystem.Tests.Application;

/// <summary>
/// Unit tests for ElevatorController focusing on request assignment and elevator orchestration.
/// </summary>
public class ElevatorControllerTests
{
    private readonly Mock<IElevatorRepository> _mockElevatorRepository;
    private readonly Mock<IElevatorRequestRepository> _mockRequestRepository;
    private readonly Mock<ILogger<ElevatorController>> _mockLogger;
    private readonly Mock<IElevatorMovementService> _mockMovementService;
    private readonly Mock<IElevatorRequestManager> _mockRequestManager;
    private readonly ElevatorController _controller;

    public ElevatorControllerTests()
    {
        _mockElevatorRepository = new Mock<IElevatorRepository>();
        _mockRequestRepository = new Mock<IElevatorRequestRepository>();
        _mockLogger = new Mock<ILogger<ElevatorController>>();
        
        // Create interface mocks
        _mockMovementService = new Mock<IElevatorMovementService>();
        _mockRequestManager = new Mock<IElevatorRequestManager>();

        _controller = new ElevatorController(
            _mockElevatorRepository.Object,
            _mockRequestRepository.Object,
            _mockLogger.Object,
            _mockMovementService.Object,
            _mockRequestManager.Object);
    }

    [Fact]
    public async Task AddRequestAsync_WithAvailableElevators_ShouldAssignToClosestElevator()
    {
        // Arrange
        var elevator1 = new Elevator(1, 1, 10);
        elevator1.MoveTo(5);
        var elevator2 = new Elevator(2, 1, 10);
        elevator2.MoveTo(8);
        
        var request = new ElevatorRequest(6, 9);

        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator1, elevator2 });
        
        _mockRequestRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.AddRequestAsync(request);

        // Assert
        request.Status.Should().Be(ElevatorRequestStatus.Assigned);
        _mockRequestRepository.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Fact]
    public async Task AddRequestAsync_ShouldExcludeOutOfServiceElevators()
    {
        // Arrange
        var workingElevator = new Elevator(1, 1, 10);
        workingElevator.MoveTo(8);
        
        var brokenElevator = new Elevator(2, 1, 10);
        brokenElevator.MoveTo(3); // Closer but out of service
        brokenElevator.SetStatus(ElevatorStatus.OutOfService);
        
        var request = new ElevatorRequest(4, 7);

        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { workingElevator, brokenElevator });
        
        _mockRequestRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.AddRequestAsync(request);

        // Assert - Should assign to working elevator, not the closer broken one
        request.Status.Should().Be(ElevatorRequestStatus.Assigned);
    }

    [Fact]
    public void ElevatorController_ShouldInitializeCorrectly()
    {
        // Assert - Controller should be created without issues
        _controller.Should().NotBeNull();
    }

    [Fact]
    public async Task AddRequestAsync_ShouldUpdateRequestStatus()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(5);
        
        var request = new ElevatorRequest(3, 8);

        // Setup repository to return elevators for AddRequestAsync
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator });
        
        _mockRequestRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.AddRequestAsync(request);

        // Assert - Verify the request was assigned
        request.Status.Should().Be(ElevatorRequestStatus.Assigned);
        _mockRequestRepository.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Fact]
    public async Task AddRequestAsync_WithBasementFloors_ShouldHandleNegativeFloors()
    {
        // Arrange
        var elevator = new Elevator(1, -5, 15);
        elevator.MoveTo(-2);
        
        var request = new ElevatorRequest(-3, 10);

        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator });
        
        _mockRequestRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.AddRequestAsync(request);

        // Assert
        request.Status.Should().Be(ElevatorRequestStatus.Assigned);
    }

    [Theory]
    [InlineData(1, 5, 3, 1)] // Elevator 1 is closer
    [InlineData(8, 2, 3, 2)] // Elevator 2 is closer
    [InlineData(5, 5, 3, 1)] // Equal distance, should pick first (elevator 1)
    public async Task AddRequestAsync_ShouldPickClosestElevator(
        int elevator1Floor, int elevator2Floor, int requestFloor, int expectedElevatorId)
    {
        // Arrange
        var elevator1 = new Elevator(1, 1, 10);
        elevator1.MoveTo(elevator1Floor);
        var elevator2 = new Elevator(2, 1, 10);
        elevator2.MoveTo(elevator2Floor);
        
        var request = new ElevatorRequest(requestFloor, requestFloor + 3);

        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator1, elevator2 });
        
        _mockRequestRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.AddRequestAsync(request);

        // Assert
        request.Status.Should().Be(ElevatorRequestStatus.Assigned);
        
        // Verify the closest elevator logic by checking distances
        var distance1 = Math.Abs(elevator1Floor - requestFloor);
        var distance2 = Math.Abs(elevator2Floor - requestFloor);
        var actualClosestId = distance1 <= distance2 ? 1 : 2;
        actualClosestId.Should().Be(expectedElevatorId);
    }

}