using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Services;
using ElevatorSystem.Infrastructure.Repositories;
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
        
        return services;
    }
}