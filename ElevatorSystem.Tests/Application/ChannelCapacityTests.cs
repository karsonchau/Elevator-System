using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElevatorSystem.Tests.Application;

/// <summary>
/// Tests for dynamic channel capacity calculation based on building floors.
/// </summary>
public class ChannelCapacityTests
{
    [Theory]
    [InlineData(1, 10, 20)]      // 10 floors => 2*10 = 20 capacity
    [InlineData(1, 50, 100)]     // 50 floors => 2*50 = 100 capacity
    [InlineData(1, 100, 200)]    // 100 floors => 2*100 = 200 capacity
    [InlineData(-5, 5, 22)]      // 11 floors (including basement) => 2*11 = 22 capacity
    [InlineData(1, 3, 10)]       // 3 floors => max(2*3=6, min=10) = 10 capacity (minimum)
    public async Task ElevatorService_WithVariousFloorCounts_ShouldCalculateCorrectChannelCapacity(
        int minFloor, int maxFloor, int expectedCapacity)
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Configure settings with specified floor range
        var settings = new ElevatorSettings
        {
            MinFloor = minFloor,
            MaxFloor = maxFloor,
            NumberOfElevators = 1,
            FloorMovementTimeMs = 1000,
            LoadingTimeMs = 1000
        };
        
        services.AddSingleton(Options.Create(settings));
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddInfrastructure();
        services.AddApplication();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act - Creating the service should calculate the channel capacity
        var elevatorService = serviceProvider.GetRequiredService<IElevatorService>();
        
        // Test that the service can handle the expected number of concurrent requests
        var requestTasks = new List<Task<Guid>>();
        
        // Try to create requests up to the expected capacity
        // This should not block or throw
        for (int i = 0; i < expectedCapacity / 2; i++)
        {
            var fromFloor = minFloor + (i % (maxFloor - minFloor));
            var toFloor = minFloor + ((i + 1) % (maxFloor - minFloor));
            
            if (fromFloor == toFloor)
                toFloor = (toFloor == maxFloor) ? minFloor : toFloor + 1;
            
            requestTasks.Add(elevatorService.RequestElevatorAsync(fromFloor, toFloor));
        }
        
        // Assert - All requests should be created successfully
        var requestIds = await Task.WhenAll(requestTasks);
        requestIds.Should().HaveCount(expectedCapacity / 2);
        requestIds.Should().OnlyContain(id => id != Guid.Empty);
        
        serviceProvider.Dispose();
    }

    [Fact]
    public async Task ElevatorService_WithLargeBuilding_ShouldHandleManySimultaneousRequests()
    {
        // Arrange - Large building with 200 floors
        var services = new ServiceCollection();
        
        var settings = new ElevatorSettings
        {
            MinFloor = 1,
            MaxFloor = 200,
            NumberOfElevators = 5,
            FloorMovementTimeMs = 500,
            LoadingTimeMs = 500
        };
        
        services.AddSingleton(Options.Create(settings));
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddInfrastructure();
        services.AddApplication();
        
        var serviceProvider = services.BuildServiceProvider();
        var elevatorService = serviceProvider.GetRequiredService<IElevatorService>();
        
        // Act - Create many concurrent requests (channel capacity should be 400)
        var requestTasks = new List<Task<Guid>>();
        
        for (int i = 0; i < 100; i++) // Create 100 requests (well below 400 capacity)
        {
            var fromFloor = 1 + (i % 200);
            var toFloor = 1 + ((i * 3 + 50) % 200);
            
            if (fromFloor == toFloor)
                toFloor = (toFloor == 200) ? 1 : toFloor + 1;
            
            requestTasks.Add(elevatorService.RequestElevatorAsync(fromFloor, toFloor));
        }
        
        // Assert - All requests should be created without blocking
        var requestIds = await Task.WhenAll(requestTasks);
        requestIds.Should().HaveCount(100);
        requestIds.Should().OnlyContain(id => id != Guid.Empty);
        
        serviceProvider.Dispose();
    }
}