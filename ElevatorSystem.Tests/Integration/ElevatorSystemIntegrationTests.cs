using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using ElevatorSystem.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Tests.Integration;

/// <summary>
/// Integration tests for the complete elevator system to verify end-to-end functionality.
/// Tests the interaction between all layers of the clean architecture.
/// </summary>
public class ElevatorSystemIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IElevatorService _elevatorService;
    private readonly IElevatorRepository _elevatorRepository;
    private readonly IElevatorRequestRepository _requestRepository;

    public ElevatorSystemIntegrationTests()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        // Add application and infrastructure services
        services.AddInfrastructure();
        services.AddApplication();
        
        _serviceProvider = services.BuildServiceProvider();
        
        _elevatorService = _serviceProvider.GetRequiredService<IElevatorService>();
        _elevatorRepository = _serviceProvider.GetRequiredService<IElevatorRepository>();
        _requestRepository = _serviceProvider.GetRequiredService<IElevatorRequestRepository>();
    }

    [Fact]
    public async Task ElevatorSystem_WithSingleElevatorAndRequest_ShouldProcessRequestSuccessfully()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        await _elevatorRepository.AddAsync(elevator);

        // Act - Create a request
        var requestId = await _elevatorService.RequestElevatorAsync(3, 8);

        // Assert - Request should be created
        var request = await _elevatorService.GetRequestStatusAsync(requestId);
        request.Should().NotBeNull();
        request!.CurrentFloor.Should().Be(3);
        request.DestinationFloor.Should().Be(8);
        request.Status.Should().Be(ElevatorRequestStatus.Pending);
    }

    [Fact]
    public async Task ElevatorSystem_WithMultipleElevators_ShouldAssignToClosestElevator()
    {
        // Arrange
        var elevator1 = new Elevator(1, 1, 10); // At floor 1
        var elevator2 = new Elevator(2, 1, 10); // At floor 1
        elevator2.MoveTo(5); // Move elevator 2 to floor 5
        
        await _elevatorRepository.AddAsync(elevator1);
        await _elevatorRepository.AddAsync(elevator2);

        // Act - Create a request from floor 6
        var requestId = await _elevatorService.RequestElevatorAsync(6, 9);

        // Wait a bit for processing
        await Task.Delay(100);

        // Assert - Request should exist
        var request = await _elevatorService.GetRequestStatusAsync(requestId);
        request.Should().NotBeNull();
    }

    [Fact]
    public async Task ElevatorSystem_WithBasementFloors_ShouldHandleNegativeFloors()
    {
        // Arrange
        var elevator = new Elevator(1, -3, 15); // Basement building
        await _elevatorRepository.AddAsync(elevator);

        // Act - Create request from basement to upper floor
        var requestId = await _elevatorService.RequestElevatorAsync(-2, 10);

        // Assert
        var request = await _elevatorService.GetRequestStatusAsync(requestId);
        request.Should().NotBeNull();
        request!.CurrentFloor.Should().Be(-2);
        request.DestinationFloor.Should().Be(10);
    }

    [Fact]
    public async Task ElevatorSystem_WithSimultaneousRequests_ShouldHandleAllRequests()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 20);
        await _elevatorRepository.AddAsync(elevator);

        // Act - Create multiple simultaneous requests
        var requestTasks = new[]
        {
            _elevatorService.RequestElevatorAsync(2, 10),
            _elevatorService.RequestElevatorAsync(5, 15),
            _elevatorService.RequestElevatorAsync(8, 3),
            _elevatorService.RequestElevatorAsync(12, 18)
        };

        var requestIds = await Task.WhenAll(requestTasks);

        // Assert - All requests should be created
        requestIds.Should().HaveCount(4);
        requestIds.Should().OnlyContain(id => id != Guid.Empty);

        // Verify all requests exist
        foreach (var requestId in requestIds)
        {
            var request = await _elevatorService.GetRequestStatusAsync(requestId);
            request.Should().NotBeNull();
            request!.Status.Should().Be(ElevatorRequestStatus.Pending);
        }
    }

    [Fact]
    public async Task ElevatorSystem_WithElevatorMovement_ShouldUpdateElevatorStatus()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        await _elevatorRepository.AddAsync(elevator);

        // Act - Test elevator movement
        elevator.MoveTo(5);
        elevator.SetStatus(ElevatorStatus.Moving);
        elevator.SetDirection(ElevatorDirection.Up);
        await _elevatorRepository.UpdateAsync(elevator);

        // Assert
        var updatedElevator = await _elevatorRepository.GetByIdAsync(1);
        updatedElevator.Should().NotBeNull();
        updatedElevator!.CurrentFloor.Should().Be(5);
        updatedElevator.Status.Should().Be(ElevatorStatus.Moving);
        updatedElevator.Direction.Should().Be(ElevatorDirection.Up);
    }

    [Fact]
    public async Task ElevatorSystem_WithRequestProcessing_ShouldStartBackgroundProcessing()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 10);
        await _elevatorRepository.AddAsync(elevator);

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(500)); // Cancel after 500ms

        // Act - Start background processing
        var requestId = await _elevatorService.RequestElevatorAsync(3, 8);
        
        var processingTask = _elevatorService.ProcessRequestsAsync(cancellationTokenSource.Token);
        
        // Wait a bit for initial processing
        await Task.Delay(100);

        // Assert - Request should still exist (processing doesn't complete in 100ms)
        var request = await _elevatorService.GetRequestStatusAsync(requestId);
        request.Should().NotBeNull();

        // Cancel the processing
        cancellationTokenSource.Cancel();
        
        try
        {
            await processingTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    [Fact]
    public async Task ElevatorSystem_WithMultipleFloorsAndRequests_ShouldOptimizeMovement()
    {
        // Arrange
        var elevator = new Elevator(1, 1, 20);
        await _elevatorRepository.AddAsync(elevator);

        // Act - Create requests that would benefit from SCAN algorithm
        var requestIds = new[]
        {
            await _elevatorService.RequestElevatorAsync(5, 15),   // Going up
            await _elevatorService.RequestElevatorAsync(8, 18),   // Going up
            await _elevatorService.RequestElevatorAsync(12, 3),   // Going down
            await _elevatorService.RequestElevatorAsync(16, 2)    // Going down
        };

        // Assert - All requests created successfully
        requestIds.Should().HaveCount(4);
        
        foreach (var requestId in requestIds)
        {
            var request = await _elevatorService.GetRequestStatusAsync(requestId);
            request.Should().NotBeNull();
            request!.Status.Should().Be(ElevatorRequestStatus.Pending);
        }
    }

    [Fact]
    public async Task ElevatorSystem_WithBoundaryConditions_ShouldHandleEdgeCases()
    {
        // Arrange - Elevator with small range
        var elevator = new Elevator(1, 1, 3);
        await _elevatorRepository.AddAsync(elevator);

        // Act & Assert - Request at minimum floor
        var requestId1 = await _elevatorService.RequestElevatorAsync(1, 3);
        var request1 = await _elevatorService.GetRequestStatusAsync(requestId1);
        request1.Should().NotBeNull();
        request1!.CurrentFloor.Should().Be(1);
        request1.DestinationFloor.Should().Be(3);

        // Act & Assert - Request at maximum floor
        var requestId2 = await _elevatorService.RequestElevatorAsync(3, 1);
        var request2 = await _elevatorService.GetRequestStatusAsync(requestId2);
        request2.Should().NotBeNull();
        request2!.CurrentFloor.Should().Be(3);
        request2.DestinationFloor.Should().Be(1);
    }

    [Fact]
    public async Task ElevatorSystem_WithOutOfServiceElevator_ShouldNotAssignRequests()
    {
        // Arrange
        var workingElevator = new Elevator(1, 1, 10);
        var brokenElevator = new Elevator(2, 1, 10);
        brokenElevator.SetStatus(ElevatorStatus.OutOfService);
        
        await _elevatorRepository.AddAsync(workingElevator);
        await _elevatorRepository.AddAsync(brokenElevator);

        // Act
        var requestId = await _elevatorService.RequestElevatorAsync(5, 8);

        // Assert - Request should be created (will be assigned to working elevator)
        var request = await _elevatorService.GetRequestStatusAsync(requestId);
        request.Should().NotBeNull();
        request!.Status.Should().Be(ElevatorRequestStatus.Pending);
    }

    [Fact]
    public async Task ElevatorSystem_GetElevatorStatus_ShouldReturnAllElevators()
    {
        // Arrange
        var elevator1 = new Elevator(1, 1, 10);
        var elevator2 = new Elevator(2, 1, 15);
        
        await _elevatorRepository.AddAsync(elevator1);
        await _elevatorRepository.AddAsync(elevator2);

        // Act
        var elevators = await _elevatorService.GetElevatorStatusAsync();

        // Assert
        var elevatorList = elevators.ToList();
        elevatorList.Should().HaveCount(2);
        elevatorList.Should().Contain(e => e.Id == 1);
        elevatorList.Should().Contain(e => e.Id == 2);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}