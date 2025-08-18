using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using ElevatorSystem.Infrastructure.Repositories;
using FluentAssertions;

namespace ElevatorSystem.Tests.Infrastructure;

/// <summary>
/// Unit tests for InMemoryElevatorRepository focusing on CRUD operations and thread safety.
/// </summary>
public class InMemoryElevatorRepositoryTests
{
    private readonly InMemoryElevatorRepository _repository;

    public InMemoryElevatorRepositoryTests()
    {
        _repository = new InMemoryElevatorRepository();
    }

    [Fact]
    public async Task AddAsync_WithNewElevator_ShouldAddSuccessfully()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);

        // Act
        await _repository.AddAsync(elevator);
        var result = await _repository.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.MinFloor.Should().Be(1);
        result.MaxFloor.Should().Be(10);
    }

    [Fact]
    public async Task AddAsync_WithDuplicateId_ShouldNotReplace()
    {
        // Arrange
        var elevator1 = new Elevator(1, 1, 10);
        var elevator2 = new Elevator(1, -5, 20); // Same ID, different config

        // Act
        await _repository.AddAsync(elevator1);
        await _repository.AddAsync(elevator2); // Should not replace
        var result = await _repository.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.MinFloor.Should().Be(1); // Original elevator should remain
        result.MaxFloor.Should().Be(10);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleElevators_ShouldReturnAll()
    {
        // Arrange
        var elevator1 = new Elevator(1, 1, 10);
        var elevator2 = new Elevator(2, -5, 15);
        var elevator3 = new Elevator(3, 1, 20);

        // Act
        await _repository.AddAsync(elevator1);
        await _repository.AddAsync(elevator2);
        await _repository.AddAsync(elevator3);
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(e => e.Id == 1);
        result.Should().Contain(e => e.Id == 2);
        result.Should().Contain(e => e.Id == 3);
    }

    [Fact]
    public async Task GetAllAsync_WithEmptyRepository_ShouldReturnEmpty()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithExistingElevator_ShouldUpdate()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        await _repository.AddAsync(elevator);

        // Modify elevator state
        elevator.MoveTo(5);
        elevator.SetDirection(ElevatorDirection.Up);
        elevator.SetStatus(ElevatorStatus.Moving);

        // Act
        await _repository.UpdateAsync(elevator);
        var result = await _repository.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentFloor.Should().Be(5);
        result.Direction.Should().Be(ElevatorDirection.Up);
        result.Status.Should().Be(ElevatorStatus.Moving);
    }

    [Fact]
    public async Task UpdateAsync_WithNewElevator_ShouldAdd()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        elevator.MoveTo(3);

        // Act
        await _repository.UpdateAsync(elevator); // Update without prior Add
        var result = await _repository.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentFloor.Should().Be(3);
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var elevatorCount = 100;

        // Act - Add many elevators concurrently
        for (int i = 1; i <= elevatorCount; i++)
        {
            var elevator = new Elevator(i, 1, 10);
            tasks.Add(_repository.AddAsync(elevator));
        }

        await Task.WhenAll(tasks);
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(elevatorCount);
    }

    [Fact]
    public async Task ConcurrentUpdates_ShouldBeThreadSafe()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        await _repository.AddAsync(elevator);

        var updateTasks = new List<Task>();
        
        // Act - Update same elevator concurrently
        for (int i = 0; i < 50; i++)
        {
            updateTasks.Add(Task.Run(async () =>
            {
                var localElevator = new Elevator(1, 1, 10);
                localElevator.MoveTo(i % 10 + 1);
                await _repository.UpdateAsync(localElevator);
            }));
        }

        await Task.WhenAll(updateTasks);
        var result = await _repository.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentFloor.Should().BeInRange(1, 10);
    }

    [Fact]
    public async Task Repository_WithBasementFloors_ShouldHandleNegativeFloors()
    {
        // Arrange
        var elevator = new Elevator(1, -10, 20);
        elevator.MoveTo(-5);

        // Act
        await _repository.AddAsync(elevator);
        var result = await _repository.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.MinFloor.Should().Be(-10);
        result.MaxFloor.Should().Be(20);
        result.CurrentFloor.Should().Be(-5);
    }
}