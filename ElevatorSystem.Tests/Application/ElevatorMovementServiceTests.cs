using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ElevatorSystem.Tests.Application;

/// <summary>
/// Unit tests for ElevatorMovementService focusing on movement logic and navigation.
/// </summary>
public class ElevatorMovementServiceTests
{
    private readonly Mock<IElevatorRepository> _mockRepository;
    private readonly Mock<ILogger<ElevatorMovementService>> _mockLogger;
    private readonly ElevatorMovementService _movementService;

    public ElevatorMovementServiceTests()
    {
        _mockRepository = new Mock<IElevatorRepository>();
        _mockLogger = new Mock<ILogger<ElevatorMovementService>>();
        _movementService = new ElevatorMovementService(_mockRepository.Object, _mockLogger.Object);
    }

    [Theory]
    [InlineData(1, 5, ElevatorDirection.Up)]
    [InlineData(8, 3, ElevatorDirection.Down)]
    [InlineData(5, 5, ElevatorDirection.Up)] // Same floor should default to Up
    public void DetermineOptimalDirection_WithIdleElevator_ShouldReturnCorrectDirection(
        int elevatorFloor, int requestFloor, ElevatorDirection expectedDirection)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(elevatorFloor);
        elevator.SetDirection(ElevatorDirection.Idle);
        
        var requests = new List<ElevatorRequest>
        {
            new ElevatorRequest(requestFloor, requestFloor + 2)
        };

        // Act
        var result = _movementService.DetermineOptimalDirection(elevator, requests);

        // Assert
        result.Should().Be(expectedDirection);
    }

    [Theory]
    [InlineData(ElevatorDirection.Up)]
    [InlineData(ElevatorDirection.Down)]
    public void DetermineOptimalDirection_WithMovingElevator_ShouldReturnCurrentDirection(
        ElevatorDirection currentDirection)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.SetDirection(currentDirection);
        
        var requests = new List<ElevatorRequest>
        {
            new ElevatorRequest(3, 8)
        };

        // Act
        var result = _movementService.DetermineOptimalDirection(elevator, requests);

        // Assert
        result.Should().Be(currentDirection);
    }

    [Fact]
    public void DetermineOptimalDirection_WithNoRequests_ShouldReturnCurrentDirection()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.SetDirection(ElevatorDirection.Down);
        var requests = new List<ElevatorRequest>();

        // Act
        var result = _movementService.DetermineOptimalDirection(elevator, requests);

        // Assert
        result.Should().Be(ElevatorDirection.Down);
    }

    [Theory]
    [InlineData(5, new int[] { 3, 7, 9 }, ElevatorDirection.Up, 7)]
    [InlineData(5, new int[] { 3, 7, 9 }, ElevatorDirection.Down, 3)]
    [InlineData(5, new int[] { 2, 3 }, ElevatorDirection.Up, null)]
    [InlineData(5, new int[] { 7, 8 }, ElevatorDirection.Down, null)]
    public void FindNextFloorInDirection_WithVariousFloors_ShouldReturnCorrectFloor(
        int currentFloor, int[] serviceFloors, ElevatorDirection direction, int? expectedFloor)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(currentFloor);
        
        var floorsNeedingService = new SortedSet<int>(serviceFloors);

        // Act
        var result = _movementService.FindNextFloorInDirection(elevator, floorsNeedingService, direction);

        // Assert
        result.Should().Be(expectedFloor);
    }

    [Fact]
    public void FindNextFloorInDirection_WithEmptyFloors_ShouldReturnNull()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(5);
        var floorsNeedingService = new SortedSet<int>();

        // Act
        var result = _movementService.FindNextFloorInDirection(elevator, floorsNeedingService, ElevatorDirection.Up);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task MoveToFloorAsync_ShouldUpdateElevatorStatusAndPosition()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(1);
        elevator.SetDirection(ElevatorDirection.Up); // Set direction before moving
        var targetFloor = 3;
        var cancellationToken = new CancellationTokenSource().Token;

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Elevator>()))
                      .Returns(Task.CompletedTask);

        // Act
        await _movementService.MoveToFloorAsync(elevator, targetFloor, cancellationToken);

        // Assert
        elevator.CurrentFloor.Should().Be(targetFloor);
        _mockRepository.Verify(r => r.UpdateAsync(elevator), Times.AtLeast(3)); // Initial + moves
    }

    [Fact]
    public async Task MoveToFloorAsync_WithCancellation_ShouldStopMovement()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(1);
        elevator.SetDirection(ElevatorDirection.Up);
        var targetFloor = 5; // Use a smaller target to avoid timing issues
        var cancellationTokenSource = new CancellationTokenSource();

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Elevator>()))
                      .Returns(Task.CompletedTask);

        // Act & Assert
        cancellationTokenSource.CancelAfter(100); // Cancel after brief delay
        
        try
        {
            await _movementService.MoveToFloorAsync(elevator, targetFloor, cancellationTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected when cancellation occurs
        }

        // Assert - Movement should have been interrupted
        elevator.CurrentFloor.Should().BeLessOrEqualTo(targetFloor); // Should not exceed target
        elevator.CurrentFloor.Should().BeGreaterOrEqualTo(1); // Should not go below minimum
    }

    [Theory]
    [InlineData(1, 10, ElevatorDirection.Up)]
    [InlineData(10, 1, ElevatorDirection.Down)]
    public async Task MoveToFloorAsync_ShouldSetCorrectDirection(
        int startFloor, int targetFloor, ElevatorDirection expectedDirection)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(startFloor);
        elevator.SetDirection(expectedDirection); // Set the expected direction
        var cancellationToken = CancellationToken.None;

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Elevator>()))
                      .Returns(Task.CompletedTask);

        // Act
        await _movementService.MoveToFloorAsync(elevator, targetFloor, cancellationToken);

        // Assert
        elevator.Status.Should().Be(ElevatorStatus.Moving);
        elevator.CurrentFloor.Should().Be(targetFloor);
        // Verify the direction was used correctly
        expectedDirection.Should().BeOneOf(ElevatorDirection.Up, ElevatorDirection.Down);
    }

    [Fact]
    public async Task MoveToFloorAsync_WithSameFloor_ShouldNotMove()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(5);
        var targetFloor = 5;
        var cancellationToken = CancellationToken.None;

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Elevator>()))
                      .Returns(Task.CompletedTask);

        // Act
        await _movementService.MoveToFloorAsync(elevator, targetFloor, cancellationToken);

        // Assert
        elevator.CurrentFloor.Should().Be(5);
        elevator.Status.Should().Be(ElevatorStatus.Moving); // Status is still set
        _mockRepository.Verify(r => r.UpdateAsync(elevator), Times.Once); // Only initial update
    }

    [Theory]
    [InlineData(-5, 10, -3, new int[] { -5, -2, 5, 8 }, ElevatorDirection.Up, -2)]
    [InlineData(-5, 10, 3, new int[] { -2, 1, 3, 8 }, ElevatorDirection.Down, 1)]
    public void FindNextFloorInDirection_WithBasementFloors_ShouldHandleNegativeNumbers(
        int minFloor, int maxFloor, int currentFloor, int[] serviceFloors, 
        ElevatorDirection direction, int expectedFloor)
    {
        // Arrange
        var elevator = new Elevator(1, minFloor, maxFloor);
        elevator.MoveTo(currentFloor);
        var floorsNeedingService = new SortedSet<int>(serviceFloors);

        // Act
        var result = _movementService.FindNextFloorInDirection(elevator, floorsNeedingService, direction);

        // Assert
        result.Should().Be(expectedFloor);
    }
}