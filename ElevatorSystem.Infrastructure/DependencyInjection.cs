using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Application.Events;
using ElevatorSystem.Application.Events.Handlers;
using ElevatorSystem.Application.Events.CommandHandlers;
using ElevatorSystem.Application.Events.CommandValidators;
using ElevatorSystem.Application.Events.ElevatorCommands;
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
        
        // Command-based elevator controller (Phase 2)
        services.AddSingleton<IElevatorController, CommandBasedElevatorController>();
        
        // Scenario reader for file-based simulation
        services.AddSingleton<IScenarioReader, ScenarioFileReader>();
        
        // Event infrastructure (Phase 1 - In-memory implementations)
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<ICommandBus, InMemoryCommandBus>();
        
        // Event handlers
        services.AddSingleton<ElevatorEventLogger>();
        
        // Command handlers (Phase 2)
        services.AddSingleton<AddElevatorRequestCommandHandler>();
        services.AddSingleton<ProcessElevatorCommandHandler>();
        
        // Command validators (Phase 2)
        services.AddSingleton<AddElevatorRequestCommandValidator>();
        services.AddSingleton<ProcessElevatorCommandValidator>();
        
        return services;
    }
}