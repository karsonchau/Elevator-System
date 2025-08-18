using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using ElevatorSystem.Infrastructure.Repositories;
using FluentAssertions;

namespace ElevatorSystem.Tests.Infrastructure;

/// <summary>
/// Unit tests for InMemoryElevatorRequestRepository focusing on CRUD operations and status filtering.
/// </summary>
public class InMemoryElevatorRequestRepositoryTests
{
    private readonly InMemoryElevatorRequestRepository _repository;

    public InMemoryElevatorRequestRepositoryTests()
    {
        _repository = new InMemoryElevatorRequestRepository();
    }

    [Fact]
    public async Task AddAsync_WithNewRequest_ShouldAddSuccessfully()
    {
        // Arrange
        var request = new ElevatorRequest(5, 10);

        // Act
        await _repository.AddAsync(request);
        var result = await _repository.GetByIdAsync(request.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(request.Id);
        result.CurrentFloor.Should().Be(5);
        result.DestinationFloor.Should().Be(10);
        result.Status.Should().Be(ElevatorRequestStatus.Pending);
    }

    [Fact]
    public async Task AddAsync_WithSameRequest_ShouldNotDuplicate()
    {
        // Arrange
        var request = new ElevatorRequest(5, 10);
        
        // Act
        await _repository.AddAsync(request);
        await _repository.AddAsync(request); // Try to add same request again
        var result = await _repository.GetByIdAsync(request.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentFloor.Should().Be(5);
        result.DestinationFloor.Should().Be(10);
        result.Status.Should().Be(ElevatorRequestStatus.Pending);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithExistingRequest_ShouldUpdate()
    {
        // Arrange
        var request = new ElevatorRequest(5, 10);
        await _repository.AddAsync(request);

        // Modify request status
        request.Status = ElevatorRequestStatus.Assigned;

        // Act
        await _repository.UpdateAsync(request);
        var result = await _repository.GetByIdAsync(request.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(ElevatorRequestStatus.Assigned);
    }

    [Fact]
    public async Task UpdateAsync_WithNewRequest_ShouldAdd()
    {
        // Arrange
        var request = new ElevatorRequest(5, 10);
        request.Status = ElevatorRequestStatus.InProgress;

        // Act
        await _repository.UpdateAsync(request); // Update without prior Add
        var result = await _repository.GetByIdAsync(request.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(ElevatorRequestStatus.InProgress);
    }

    [Fact]
    public async Task GetPendingRequestsAsync_ShouldReturnOnlyPendingRequests()
    {
        // Arrange
        var pendingRequest1 = new ElevatorRequest(1, 5);
        var pendingRequest2 = new ElevatorRequest(3, 8);
        var assignedRequest = new ElevatorRequest(2, 7);
        assignedRequest.Status = ElevatorRequestStatus.Assigned;
        var completedRequest = new ElevatorRequest(4, 9);
        completedRequest.Status = ElevatorRequestStatus.Completed;

        // Act
        await _repository.AddAsync(pendingRequest1);
        await _repository.AddAsync(pendingRequest2);
        await _repository.AddAsync(assignedRequest);
        await _repository.AddAsync(completedRequest);

        var result = await _repository.GetPendingRequestsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Id == pendingRequest1.Id);
        result.Should().Contain(r => r.Id == pendingRequest2.Id);
        result.Should().NotContain(r => r.Id == assignedRequest.Id);
        result.Should().NotContain(r => r.Id == completedRequest.Id);
    }

    [Fact]
    public async Task GetPendingRequestsAsync_WithNoPendingRequests_ShouldReturnEmpty()
    {
        // Arrange
        var assignedRequest = new ElevatorRequest(2, 7);
        assignedRequest.Status = ElevatorRequestStatus.Assigned;
        await _repository.AddAsync(assignedRequest);

        // Act
        var result = await _repository.GetPendingRequestsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ElevatorRequestStatus.Pending)]
    [InlineData(ElevatorRequestStatus.Assigned)]
    [InlineData(ElevatorRequestStatus.InProgress)]
    [InlineData(ElevatorRequestStatus.Completed)]
    public async Task StatusTransitions_ShouldUpdateCorrectly(ElevatorRequestStatus status)
    {
        // Arrange
        var request = new ElevatorRequest(5, 10);
        await _repository.AddAsync(request);

        // Act
        request.Status = status;
        await _repository.UpdateAsync(request);
        var result = await _repository.GetByIdAsync(request.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(status);
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var requestCount = 100;

        // Act - Add many requests concurrently
        for (int i = 0; i < requestCount; i++)
        {
            var request = new ElevatorRequest(i % 10 + 1, (i % 10) + 5);
            tasks.Add(_repository.AddAsync(request));
        }

        await Task.WhenAll(tasks);
        var pendingRequests = await _repository.GetPendingRequestsAsync();

        // Assert
        pendingRequests.Should().HaveCount(requestCount);
    }

    [Fact]
    public async Task ConcurrentStatusUpdates_ShouldBeThreadSafe()
    {
        // Arrange
        var request = new ElevatorRequest(5, 10);
        await _repository.AddAsync(request);

        var updateTasks = new List<Task>();
        var statuses = new[]
        {
            ElevatorRequestStatus.Assigned,
            ElevatorRequestStatus.InProgress,
            ElevatorRequestStatus.Completed
        };

        // Act - Update same request concurrently with different statuses
        for (int i = 0; i < 50; i++)
        {
            var status = statuses[i % statuses.Length];
            updateTasks.Add(Task.Run(async () =>
            {
                // Get the request from repository and update its status
                var currentRequest = await _repository.GetByIdAsync(request.Id);
                if (currentRequest != null)
                {
                    currentRequest.Status = status;
                    await _repository.UpdateAsync(currentRequest);
                }
            }));
        }

        await Task.WhenAll(updateTasks);
        var result = await _repository.GetByIdAsync(request.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().BeOneOf(statuses);
    }

    [Fact]
    public async Task Repository_WithBasementFloors_ShouldHandleNegativeFloors()
    {
        // Arrange
        var request = new ElevatorRequest(-5, 10);

        // Act
        await _repository.AddAsync(request);
        var result = await _repository.GetByIdAsync(request.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentFloor.Should().Be(-5);
        result.DestinationFloor.Should().Be(10);
    }

    [Fact]
    public async Task GetPendingRequestsAsync_WithMixedFloorsIncludingBasement_ShouldReturnCorrectly()
    {
        // Arrange
        var basementRequest = new ElevatorRequest(-3, 5);
        var groundRequest = new ElevatorRequest(1, 8);
        var upperRequest = new ElevatorRequest(10, 15);

        // Act
        await _repository.AddAsync(basementRequest);
        await _repository.AddAsync(groundRequest);
        await _repository.AddAsync(upperRequest);

        var result = await _repository.GetPendingRequestsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(r => r.CurrentFloor == -3);
        result.Should().Contain(r => r.CurrentFloor == 1);
        result.Should().Contain(r => r.CurrentFloor == 10);
    }
}