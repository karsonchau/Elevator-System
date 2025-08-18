using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using FluentAssertions;

namespace ElevatorSystem.Tests.Domain;

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
    [InlineData(0, 1, 10)]
    [InlineData(-1, 1, 10)]
    public void Constructor_WithInvalidId_ShouldCreateElevatorWithId(int id, int minFloor, int maxFloor)
    {
        // Act
        var elevator = new Elevator(id, minFloor, maxFloor);

        // Assert - Constructor doesn't validate ID, just sets it
        elevator.Id.Should().Be(id);
    }

    [Theory]
    [InlineData(1, 5, 3)]
    [InlineData(1, 10, 10)]
    public void Constructor_WithInvalidFloorRange_ShouldCreateElevator(int id, int minFloor, int maxFloor)
    {
        // Act
        var elevator = new Elevator(id, minFloor, maxFloor);

        // Assert - Constructor doesn't validate floor range, just sets values
        elevator.MinFloor.Should().Be(minFloor);
        elevator.MaxFloor.Should().Be(maxFloor);
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
    [InlineData(1, 5, 1)]
    [InlineData(1, 5, 3)]
    [InlineData(5, 10, 7)]
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
}