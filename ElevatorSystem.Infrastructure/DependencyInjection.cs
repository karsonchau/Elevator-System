using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Application.Events;
using ElevatorSystem.Application.Events.Handlers;
using ElevatorSystem.Infrastructure.Repositories;
using ElevatorSystem.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;

namespace ElevatorSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IElevatorRepository, InMemoryElevatorRepository>();
        services.AddSingleton<IElevatorRequestRepository, InMemoryElevatorRequestRepository>();
        
        return services;
    }

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<IElevatorService, ElevatorService>();
        
        // Supporting services for separation of concerns
        services.AddSingleton<IElevatorMovementService, ElevatorMovementService>();
        services.AddSingleton<IElevatorRequestManager, ElevatorRequestManager>();
        
        // Use simple polling-based elevator controller instead of event-driven
        services.AddSingleton<IElevatorController, ElevatorController>();
        
        // Scenario reader for file-based simulation
        services.AddSingleton<IScenarioReader, ScenarioFileReader>();
        
        // Event infrastructure (Phase 1 - In-memory implementations)
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<ICommandBus, InMemoryCommandBus>();
        
        // Event handlers
        services.AddSingleton<ElevatorEventLogger>();
        
        return services;
    }
}