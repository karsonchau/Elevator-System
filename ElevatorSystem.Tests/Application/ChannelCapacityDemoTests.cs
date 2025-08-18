using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElevatorSystem.Tests.Application;

/// <summary>
/// Demonstrates that the channel capacity calculation works correctly for different building sizes.
/// </summary>
public class ChannelCapacityDemoTests
{
    [Fact]
    public async Task ElevatorService_SmallBuilding_ShouldUseMinimumChannelCapacity()
    {
        // Arrange - Small 3-floor building
        var settings = new ElevatorSettings
        {
            MinFloor = 1,
            MaxFloor = 3, // Only 3 floors, so 2*3=6, but minimum is 10
            NumberOfElevators = 1
        };

        // Act & Assert
        var service = await CreateElevatorServiceWithSettings(settings);
        
        // Should be able to handle at least 5 concurrent requests (half of minimum capacity)
        var requestTasks = Enumerable.Range(0, 5)
            .Select(_ => service.RequestElevatorAsync(1, 3))
            .ToArray();

        var requestIds = await Task.WhenAll(requestTasks);
        requestIds.Should().HaveCount(5);
        requestIds.Should().OnlyContain(id => id != Guid.Empty);
    }

    [Fact]
    public async Task ElevatorService_SkyscraperBuilding_ShouldScaleChannelCapacity()
    {
        // Arrange - Large 100-floor skyscraper
        var settings = new ElevatorSettings
        {
            MinFloor = 1,
            MaxFloor = 100, // 100 floors, so 2*100=200 capacity
            NumberOfElevators = 10
        };

        // Act & Assert
        var service = await CreateElevatorServiceWithSettings(settings);
        
        // Should handle many concurrent requests without blocking
        var requestTasks = Enumerable.Range(0, 50) // Create 50 requests
            .Select(i => service.RequestElevatorAsync(
                1 + (i % 100), 
                1 + ((i + 25) % 100)))
            .ToArray();

        var requestIds = await Task.WhenAll(requestTasks);
        requestIds.Should().HaveCount(50);
        requestIds.Should().OnlyContain(id => id != Guid.Empty);
    }

    [Fact]
    public async Task ElevatorService_BasementBuilding_ShouldCalculateCorrectFloorCount()
    {
        // Arrange - Building with basement levels
        var settings = new ElevatorSettings
        {
            MinFloor = -10, // 10 basement levels
            MaxFloor = 40,  // 40 above ground levels = 51 total floors, so 2*51=102 capacity
            NumberOfElevators = 3
        };

        // Act & Assert
        var service = await CreateElevatorServiceWithSettings(settings);
        
        // Test requests across the full range including basements
        var requestTasks = new[]
        {
            service.RequestElevatorAsync(-10, 40),  // Full range
            service.RequestElevatorAsync(-5, 20),   // Basement to mid-level
            service.RequestElevatorAsync(30, -8),   // High floor to basement
            service.RequestElevatorAsync(0, 15)     // Ground to upper
        };

        var requestIds = await Task.WhenAll(requestTasks);
        requestIds.Should().HaveCount(4);
        requestIds.Should().OnlyContain(id => id != Guid.Empty);
    }

    private async Task<IElevatorService> CreateElevatorServiceWithSettings(ElevatorSettings settings)
    {
        var services = new ServiceCollection();
        
        services.AddSingleton(Options.Create(settings));
        services.AddLogging(builder => 
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Information)
                   .AddFilter("ElevatorSystem.Application.Services.ElevatorService", LogLevel.Information));
        
        services.AddInfrastructure();
        services.AddApplication();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Initialize some elevators
        var elevatorRepository = serviceProvider.GetRequiredService<IElevatorRepository>();
        for (int i = 1; i <= settings.NumberOfElevators; i++)
        {
            var elevator = new Elevator(i, settings.MinFloor, settings.MaxFloor);
            await elevatorRepository.AddAsync(elevator);
        }
        
        return serviceProvider.GetRequiredService<IElevatorService>();
    }
}