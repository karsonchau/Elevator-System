using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Application.Events;
using ElevatorSystem.Application.Events.Handlers;
using ElevatorSystem.Application.Events.CommandHandlers;
using ElevatorSystem.Application.Events.ElevatorCommands;
using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Infrastructure.Repositories;
using ElevatorSystem.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using ElevatorSystem.Domain.Entities;

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
        
        // Shared collections for elevator state (Phase 2 command infrastructure)
        services.AddSingleton<ConcurrentDictionary<int, ConcurrentBag<ElevatorRequest>>>();
        services.AddSingleton<ConcurrentDictionary<int, ConcurrentDictionary<int, bool>>>();
        services.AddSingleton<ConcurrentDictionary<int, object>>();
        
        // Production-ready elevator controller with failure handling
        services.AddSingleton<IElevatorController, ElevatorController>();
        
        // Scenario reader for file-based simulation
        services.AddSingleton<IScenarioReader, ScenarioFileReader>();
        
        // Event infrastructure (Phase 1 - In-memory implementations)
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<ICommandBus, InMemoryCommandBus>();
        
        // Event handlers
        services.AddSingleton<ElevatorEventLogger>();
        
        // Phase 3: Robust processing pipeline services
        services.AddSingleton<IRetryPolicyManager, RetryPolicyManager>();
        services.AddSingleton<IRequestStatusTracker, RequestStatusTracker>();
        services.AddSingleton<IHealthMonitor, HealthMonitor>();
        
        // Essential command handlers for robust pipeline
        services.AddSingleton<SubmitElevatorRequestCommandHandler>();        // Phase 3 entry point
        services.AddSingleton<AddElevatorRequestCommandHandler>();           // Phase 2 internal processing
        services.AddSingleton<ProcessElevatorCommandHandler>();              // Phase 2 elevator operations
        services.AddSingleton<UpdateRequestStatusCommandHandler>();          // Phase 3 status updates
        
        return services;
    }
}