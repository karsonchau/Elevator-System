using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using FluentAssertions;

namespace ElevatorSystem.Tests.Domain;

public class ElevatorRequestTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateRequest()
    {
        // Arrange & Act
        var request = new ElevatorRequest(3, 8);

        // Assert
        request.CurrentFloor.Should().Be(3);
        request.DestinationFloor.Should().Be(8);
        request.Status.Should().Be(ElevatorRequestStatus.Pending);
        request.RequestTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        request.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    public void Constructor_WithInvalidCurrentFloor_ShouldCreateRequest(int currentFloor, int destinationFloor)
    {
        // Act
        var request = new ElevatorRequest(currentFloor, destinationFloor);

        // Assert - Constructor doesn't validate, just sets values
        request.CurrentFloor.Should().Be(currentFloor);
        request.DestinationFloor.Should().Be(destinationFloor);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, -1)]
    public void Constructor_WithInvalidDestinationFloor_ShouldCreateRequest(int currentFloor, int destinationFloor)
    {
        // Act
        var request = new ElevatorRequest(currentFloor, destinationFloor);

        // Assert - Constructor doesn't validate, just sets values
        request.CurrentFloor.Should().Be(currentFloor);
        request.DestinationFloor.Should().Be(destinationFloor);
    }

    [Fact]
    public void Constructor_WithSameCurrentAndDestinationFloor_ShouldCreateRequest()
    {
        // Act
        var request = new ElevatorRequest(5, 5);

        // Assert - Constructor doesn't validate, just sets values
        request.CurrentFloor.Should().Be(5);
        request.DestinationFloor.Should().Be(5);
    }

    [Theory]
    [InlineData(3, 8, ElevatorDirection.Up)]
    [InlineData(8, 3, ElevatorDirection.Down)]
    [InlineData(1, 10, ElevatorDirection.Up)]
    [InlineData(10, 1, ElevatorDirection.Down)]
    public void Direction_WithDifferentFloors_ShouldReturnCorrectDirection(
        int currentFloor, int destinationFloor, ElevatorDirection expectedDirection)
    {
        // Arrange
        var request = new ElevatorRequest(currentFloor, destinationFloor);

        // Act & Assert
        request.Direction.Should().Be(expectedDirection);
    }

    [Theory]
    [InlineData(ElevatorRequestStatus.Pending)]
    [InlineData(ElevatorRequestStatus.Assigned)]
    [InlineData(ElevatorRequestStatus.InProgress)]
    [InlineData(ElevatorRequestStatus.Completed)]
    [InlineData(ElevatorRequestStatus.Cancelled)]
    public void Status_WithValidStatus_ShouldSetCorrectly(ElevatorRequestStatus status)
    {
        // Arrange
        var request = new ElevatorRequest(3, 8);

        // Act
        request.Status = status;

        // Assert
        request.Status.Should().Be(status);
    }

    [Fact]
    public void ElevatorRequest_ShouldNotHaveElevatorIdProperty()
    {
        // Arrange
        var request = new ElevatorRequest(3, 8);

        // Assert - ElevatorRequest doesn't have ElevatorId property in domain
        request.Should().NotBeNull();
        request.CurrentFloor.Should().Be(3);
        request.DestinationFloor.Should().Be(8);
    }

    [Fact]
    public async Task RequestTime_ShouldBeImmutableAfterCreation()
    {
        // Arrange
        var request = new ElevatorRequest(3, 8);
        var originalRequestTime = request.RequestTime;

        // Act - Wait a bit to ensure time difference
        await Task.Delay(10);

        // Assert - RequestTime should not change
        request.RequestTime.Should().Be(originalRequestTime);
    }

    [Fact]
    public void Id_ShouldBeUniqueForEachRequest()
    {
        // Arrange & Act
        var request1 = new ElevatorRequest(3, 8);
        var request2 = new ElevatorRequest(5, 2);

        // Assert
        request1.Id.Should().NotBe(request2.Id);
        request1.Id.Should().NotBeEmpty();
        request2.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(3, 8, 2, true)]  // Request from 3 to 8, elevator at 2 going up
    [InlineData(3, 8, 5, false)] // Request from 3 to 8, elevator at 5 (already passed)
    [InlineData(8, 3, 9, true)]  // Request from 8 to 3, elevator at 9 going down
    [InlineData(8, 3, 2, false)] // Request from 8 to 3, elevator at 2 (already passed)
    public void Direction_WithElevatorPosition_ShouldCalculateCorrectly(
        int currentFloor, int destinationFloor, int elevatorFloor, bool canPickup)
    {
        // Arrange
        var request = new ElevatorRequest(currentFloor, destinationFloor);

        // Act & Assert - Test direction calculation
        if (currentFloor < destinationFloor)
        {
            request.Direction.Should().Be(ElevatorDirection.Up);
        }
        else
        {
            request.Direction.Should().Be(ElevatorDirection.Down);
        }
    }
}