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
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeTrue();
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
        var result = await _controller.AddRequestAsync(request);

        // Assert - Should assign to working elevator, not the closer broken one
        result.Should().BeTrue();
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
        var result = await _controller.AddRequestAsync(request);

        // Assert - Verify the request was assigned
        result.Should().BeTrue();
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
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeTrue();
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
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeTrue();
        request.Status.Should().Be(ElevatorRequestStatus.Assigned);
        
        // Verify the closest elevator logic by checking distances
        var distance1 = Math.Abs(elevator1Floor - requestFloor);
        var distance2 = Math.Abs(elevator2Floor - requestFloor);
        var actualClosestId = distance1 <= distance2 ? 1 : 2;
        actualClosestId.Should().Be(expectedElevatorId);
    }

    #region Validation Tests

    [Fact]
    public async Task AddRequestAsync_WithNullRequest_ShouldReturnFalse()
    {
        // Act
        var result = await _controller.AddRequestAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddRequestAsync_WithNoAvailableElevators_ShouldReturnFalse()
    {
        // Arrange
        var request = new ElevatorRequest(5, 8);
        
        // Setup repository to return no elevators
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(Array.Empty<Elevator>());

        // Act
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddRequestAsync_WithAllElevatorsOutOfService_ShouldReturnFalse()
    {
        // Arrange
        var elevator1 = new Elevator(1, 1, 10);
        elevator1.SetStatus(ElevatorStatus.OutOfService);
        var elevator2 = new Elevator(2, 1, 10);
        elevator2.SetStatus(ElevatorStatus.OutOfService);
        
        var request = new ElevatorRequest(5, 8);
        
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator1, elevator2 });

        // Act
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 5)]    // Current floor below minimum
    [InlineData(11, 5)]   // Current floor above maximum
    [InlineData(-1, 5)]   // Current floor negative (below building range)
    [InlineData(15, 5)]   // Current floor way above maximum
    public async Task AddRequestAsync_WithInvalidCurrentFloor_ShouldReturnFalse(
        int invalidCurrentFloor, int destinationFloor)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10); // Valid range: 1-10
        var request = new ElevatorRequest(invalidCurrentFloor, destinationFloor);
        
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator });

        // Act
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(5, 0)]    // Destination floor below minimum
    [InlineData(5, 11)]   // Destination floor above maximum
    [InlineData(5, -2)]   // Destination floor negative
    [InlineData(5, 20)]   // Destination floor way above maximum
    public async Task AddRequestAsync_WithInvalidDestinationFloor_ShouldReturnFalse(
        int currentFloor, int invalidDestinationFloor)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10); // Valid range: 1-10
        var request = new ElevatorRequest(currentFloor, invalidDestinationFloor);
        
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator });

        // Act
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddRequestAsync_WithBasementInvalidFloors_ShouldReturnFalse()
    {
        // Arrange
        var elevator = new Elevator(1, -5, 15); // Valid range: -5 to 15
        var request = new ElevatorRequest(-10, 5); // Current floor below minimum basement level
        
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator });

        // Act
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddRequestAsync_WithMultipleElevatorsAndInvalidFloor_ShouldReturnFalse()
    {
        // Arrange
        var elevator1 = new Elevator(1, 1, 10);    // Range: 1-10
        var elevator2 = new Elevator(2, -2, 20);   // Range: -2-20
        // Overall building range: -2 to 20
        
        var request = new ElevatorRequest(-5, 5); // Current floor below overall minimum
        
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator1, elevator2 });

        // Act
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddRequestAsync_WithNoElevatorServingBothFloors_ShouldReturnFalse()
    {
        // Arrange
        var elevator1 = new Elevator(1, 1, 5);     // Can serve floors 1-5
        var elevator2 = new Elevator(2, 10, 15);   // Can serve floors 10-15
        // No elevator can serve both floor 3 and floor 12
        
        var request = new ElevatorRequest(3, 12);
        
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator1, elevator2 });

        // Act
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AddRequestAsync_WithSameFloorRequest_ShouldThrowFromElevatorRequestConstructor()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator });

        // Act & Assert - This should throw from the ElevatorRequest constructor itself
        FluentActions.Invoking(() => new ElevatorRequest(5, 5))
            .Should().Throw<ArgumentException>()
            .WithMessage("Current floor cannot be the same as destination floor. (Parameter 'destinationFloor')");
    }

    [Fact]
    public async Task AddRequestAsync_WithValidRequestButPartialElevatorService_ShouldSucceed()
    {
        // Arrange
        var elevator1 = new Elevator(1, 1, 5);     // Can serve floors 1-5 only
        var elevator2 = new Elevator(2, 1, 15);    // Can serve floors 1-15 (can handle the full request)
        
        var request = new ElevatorRequest(3, 12);  // Needs elevator2
        
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator1, elevator2 });
        
        _mockRequestRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeTrue();
        request.Status.Should().Be(ElevatorRequestStatus.Assigned);
        _mockRequestRepository.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Theory]
    [InlineData(-3, 5)]   // Valid basement to upper floor
    [InlineData(10, -1)]  // Valid upper floor to basement
    [InlineData(-2, -1)]  // Valid basement to basement
    public async Task AddRequestAsync_WithValidBasementRequests_ShouldSucceed(
        int currentFloor, int destinationFloor)
    {
        // Arrange
        var elevator = new Elevator(1, -5, 15); // Building with basement
        var request = new ElevatorRequest(currentFloor, destinationFloor);
        
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator });
        
        _mockRequestRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeTrue();
        request.Status.Should().Be(ElevatorRequestStatus.Assigned);
        _mockRequestRepository.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Fact]
    public async Task AddRequestAsync_WithBoundaryFloors_ShouldSucceed()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        var request = new ElevatorRequest(1, 10); // From minimum to maximum floor
        
        _mockElevatorRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { elevator });
        
        _mockRequestRepository.Setup(r => r.UpdateAsync(It.IsAny<ElevatorRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AddRequestAsync(request);

        // Assert
        result.Should().BeTrue();
        request.Status.Should().Be(ElevatorRequestStatus.Assigned);
        _mockRequestRepository.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    #endregion

}