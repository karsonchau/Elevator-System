using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using FluentAssertions;

namespace ElevatorSystem.Tests.Domain;

/// <summary>
/// Unit tests for the Elevator domain entity, including edge cases and validation scenarios.
/// </summary>
public class ElevatorTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateElevator()
    {
        // Arrange & Act
        var elevator = new Elevator(1, 1, 10);

        // Assert
        elevator.Id.Should().Be(1);
        elevator.MinFloor.Should().Be(1);
        elevator.MaxFloor.Should().Be(10);
        elevator.CurrentFloor.Should().Be(1);
        elevator.Status.Should().Be(ElevatorStatus.Idle);
        elevator.Direction.Should().Be(ElevatorDirection.Idle);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidId_ShouldThrowArgumentException(int invalidId)
    {
        // Act & Assert
        FluentActions.Invoking(() => new Elevator(invalidId, 1, 10))
            .Should().Throw<ArgumentException>()
            .WithMessage("Elevator ID must be positive.*");
    }

    [Theory]
    [InlineData(1, 5, 3)] // maxFloor < minFloor
    [InlineData(1, 10, 10)] // maxFloor == minFloor
    public void Constructor_WithInvalidFloorRange_ShouldThrowArgumentException(int id, int minFloor, int maxFloor)
    {
        // Act & Assert
        FluentActions.Invoking(() => new Elevator(id, minFloor, maxFloor))
            .Should().Throw<ArgumentException>()
            .WithMessage("Maximum floor must be greater than minimum floor.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Constructor_WithInvalidFloorMovementTime_ShouldThrowArgumentException(int invalidTime)
    {
        // Act & Assert
        FluentActions.Invoking(() => new Elevator(1, 1, 10, invalidTime, 1000))
            .Should().Throw<ArgumentException>()
            .WithMessage("Floor movement time must be positive.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Constructor_WithInvalidLoadingTime_ShouldThrowArgumentException(int invalidTime)
    {
        // Act & Assert
        FluentActions.Invoking(() => new Elevator(1, 1, 10, 1000, invalidTime))
            .Should().Throw<ArgumentException>()
            .WithMessage("Loading time must be positive.*");
    }

    [Theory]
    [InlineData(-3, 15)] // Basement to top floor
    [InlineData(-10, -1)] // Basement only building
    [InlineData(0, 50)] // Ground floor to skyscraper
    public void Constructor_WithBasementFloors_ShouldCreateValidElevator(int minFloor, int maxFloor)
    {
        // Act
        var elevator = new Elevator(1, minFloor, maxFloor);

        // Assert
        elevator.MinFloor.Should().Be(minFloor);
        elevator.MaxFloor.Should().Be(maxFloor);
        elevator.CurrentFloor.Should().Be(minFloor); // Should start at lowest floor
    }

    [Theory]
    [InlineData(1, 10, 5)]
    [InlineData(1, 10, 1)]
    [InlineData(1, 10, 10)]
    public void MoveTo_WithValidFloor_ShouldSetCurrentFloor(int minFloor, int maxFloor, int floor)
    {
        // Arrange
        var elevator = new Elevator(1, minFloor, maxFloor);

        // Act
        elevator.MoveTo(floor);

        // Assert
        elevator.CurrentFloor.Should().Be(floor);
    }

    [Theory]
    [InlineData(1, 10, 0)]
    [InlineData(1, 10, 11)]
    [InlineData(5, 8, 4)]
    [InlineData(5, 8, 9)]
    public void MoveTo_WithInvalidFloor_ShouldThrowArgumentOutOfRangeException(int minFloor, int maxFloor, int floor)
    {
        // Arrange
        var elevator = new Elevator(1, minFloor, maxFloor);

        // Act & Assert
        FluentActions.Invoking(() => elevator.MoveTo(floor))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage($"Floor must be between {minFloor} and {maxFloor}*");
    }

    [Theory]
    [InlineData(ElevatorStatus.Idle)]
    [InlineData(ElevatorStatus.Moving)]
    [InlineData(ElevatorStatus.Loading)]
    [InlineData(ElevatorStatus.OutOfService)]
    public void SetStatus_WithValidStatus_ShouldSetCorrectly(ElevatorStatus status)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);

        // Act
        elevator.SetStatus(status);

        // Assert
        elevator.Status.Should().Be(status);
    }

    [Theory]
    [InlineData(ElevatorDirection.Idle)]
    [InlineData(ElevatorDirection.Up)]
    [InlineData(ElevatorDirection.Down)]
    public void SetDirection_WithValidDirection_ShouldSetCorrectly(ElevatorDirection direction)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);

        // Act
        elevator.SetDirection(direction);

        // Assert
        elevator.Direction.Should().Be(direction);
    }

    [Theory]
    [InlineData(1, 5, 4)]
    [InlineData(5, 1, 4)]
    [InlineData(5, 10, 5)]
    public void GetDistanceToFloor_WithValidFloors_ShouldCalculateCorrectDistance(int currentFloor, int targetFloor, int expectedDistance)
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(currentFloor);

        // Act
        var distance = elevator.GetDistanceToFloor(targetFloor);

        // Assert
        distance.Should().Be(expectedDistance);
    }

    [Theory]
    [InlineData(-5, 10, -5)] // At minimum basement floor
    [InlineData(-5, 10, 10)] // At maximum floor
    [InlineData(1, 2, 1)] // Two floor building, move to first floor
    public void MoveTo_AtBoundaryFloors_ShouldSucceed(int minFloor, int maxFloor, int targetFloor)
    {
        // Arrange
        var elevator = new Elevator(1, minFloor, maxFloor);

        // Act
        elevator.MoveTo(targetFloor);

        // Assert
        elevator.CurrentFloor.Should().Be(targetFloor);
    }

    [Theory]
    [InlineData(-5, 10, -6)] // Below minimum basement floor
    [InlineData(-5, 10, 11)] // Above maximum floor
    [InlineData(0, 5, -1)] // Negative floor when basement not supported
    public void MoveTo_OutsideBounds_ShouldThrowArgumentOutOfRangeException(int minFloor, int maxFloor, int invalidFloor)
    {
        // Arrange
        var elevator = new Elevator(1, minFloor, maxFloor);

        // Act & Assert
        FluentActions.Invoking(() => elevator.MoveTo(invalidFloor))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage($"Floor must be between {minFloor} and {maxFloor}*");
    }

    [Theory]
    [InlineData(-3, 5, -3, true)] // Can serve basement floor
    [InlineData(-3, 5, 5, true)] // Can serve top floor
    [InlineData(-3, 5, 0, true)] // Can serve ground floor
    [InlineData(-3, 5, -4, false)] // Cannot serve below basement
    [InlineData(-3, 5, 6, false)] // Cannot serve above top floor
    public void CanServeFloor_WithVariousFloors_ShouldReturnCorrectResult(int minFloor, int maxFloor, int testFloor, bool expected)
    {
        // Arrange
        var elevator = new Elevator(1, minFloor, maxFloor);

        // Act
        var result = elevator.CanServeFloor(testFloor);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-5, 3, 8)] // From basement to top
    [InlineData(5, -3, 8)] // From top to basement
    [InlineData(0, 0, 0)] // Same floor
    public void GetDistanceToFloor_WithBasementFloors_ShouldCalculateCorrectDistance(int currentFloor, int targetFloor, int expectedDistance)
    {
        // Arrange
        var elevator = new Elevator(1, -10, 20);
        elevator.MoveTo(currentFloor);

        // Act
        var distance = elevator.GetDistanceToFloor(targetFloor);

        // Assert
        distance.Should().Be(expectedDistance);
    }

    [Fact]
    public void SetDirection_MultipleChanges_ShouldMaintainLastValue()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);

        // Act
        elevator.SetDirection(ElevatorDirection.Up);
        elevator.SetDirection(ElevatorDirection.Down);
        elevator.SetDirection(ElevatorDirection.Idle);

        // Assert
        elevator.Direction.Should().Be(ElevatorDirection.Idle);
    }

    [Fact]
    public void SetStatus_MultipleChanges_ShouldMaintainLastValue()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);

        // Act
        elevator.SetStatus(ElevatorStatus.Moving);
        elevator.SetStatus(ElevatorStatus.Loading);
        elevator.SetStatus(ElevatorStatus.OutOfService);

        // Assert
        elevator.Status.Should().Be(ElevatorStatus.OutOfService);
    }
}