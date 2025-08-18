using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using ElevatorSystem.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Presentation;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        await InitializeElevators(host.Services);
        
        var elevatorController = host.Services.GetRequiredService<IElevatorController>();
        var elevatorRepository = host.Services.GetRequiredService<IElevatorRepository>();
        var requestRepository = host.Services.GetRequiredService<IElevatorRequestRepository>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        
        var cts = new CancellationTokenSource();
        
        // Start elevator processing for each elevator
        var elevators = await elevatorRepository.GetAllAsync();
        var elevatorTasks = elevators.Select(elevator => 
            elevatorController.ProcessElevatorAsync(elevator, cts.Token)).ToArray();
        
        // Run simulation
        await RunSimulation(elevatorController, requestRepository, elevatorRepository, logger);
        
        cts.Cancel();
        
        try
        {
            await Task.WhenAll(elevatorTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling tasks
        }
        
        logger.LogInformation("Simulation completed");
        
        // Ensure program exits after simulation
        Environment.Exit(0);
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<ElevatorSettings>(context.Configuration.GetSection(ElevatorSettings.SectionName));
                services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
                services.AddInfrastructure();
                services.AddApplication();
            });

    private static async Task InitializeElevators(IServiceProvider services)
    {
        var elevatorRepository = services.GetRequiredService<IElevatorRepository>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        var elevatorSettings = new ElevatorSettings();
        configuration.GetSection(ElevatorSettings.SectionName).Bind(elevatorSettings);
        
        logger.LogInformation("Initializing elevator system with settings: MinFloor={MinFloor}, MaxFloor={MaxFloor}, NumberOfElevators={NumberOfElevators}, FloorMovementTime={FloorMovementTimeMs}ms, LoadingTime={LoadingTimeMs}ms",
            elevatorSettings.MinFloor, elevatorSettings.MaxFloor, elevatorSettings.NumberOfElevators, elevatorSettings.FloorMovementTimeMs, elevatorSettings.LoadingTimeMs);
        
        for (int i = 1; i <= elevatorSettings.NumberOfElevators; i++)
        {
            var elevator = new Elevator(i, 
                minFloor: elevatorSettings.MinFloor, 
                maxFloor: elevatorSettings.MaxFloor,
                floorMovementTimeMs: elevatorSettings.FloorMovementTimeMs,
                loadingTimeMs: elevatorSettings.LoadingTimeMs);
            await elevatorRepository.AddAsync(elevator);
            logger.LogInformation("Initialized elevator {ElevatorId} with floor range {MinFloor}-{MaxFloor}, movement speed {FloorMovementTimeMs}ms/floor, loading time {LoadingTimeMs}ms", 
                elevator.Id, elevatorSettings.MinFloor, elevatorSettings.MaxFloor, elevatorSettings.FloorMovementTimeMs, elevatorSettings.LoadingTimeMs);
        }
    }

    private static async Task RunSimulation(IElevatorController elevatorController, IElevatorRequestRepository requestRepository, IElevatorRepository elevatorRepository, ILogger<Program> logger)
    {
        logger.LogInformation("=== Single Elevator System Simulation ===");
        
        var scenarios = new[]
        {
            (currentFloor: 1, destinationFloor: 5, requestTimeMs: 0),
            (currentFloor: 3, destinationFloor: 8, requestTimeMs: 1000),
            (currentFloor: 7, destinationFloor: 2, requestTimeMs: 4000),
            (currentFloor: 6, destinationFloor: 1, requestTimeMs: 6000),
            (currentFloor: 4, destinationFloor: 9, requestTimeMs: 8000)
        };

        var requests = new List<ElevatorRequest>();
        var simulationStartTime = DateTime.UtcNow;
        
        foreach (var (currentFloor, destinationFloor, requestTimeMs) in scenarios)
        {
            // Calculate how long to wait based on the request time
            var elapsedMs = (DateTime.UtcNow - simulationStartTime).TotalMilliseconds;
            var waitTimeMs = Math.Max(0, requestTimeMs - elapsedMs);
            
            if (waitTimeMs > 0)
            {
                await Task.Delay((int)waitTimeMs);
            }
            
            logger.LogInformation("t={ElapsedTime}ms: Requesting elevator from floor {CurrentFloor} to floor {DestinationFloor}", 
                (int)(DateTime.UtcNow - simulationStartTime).TotalMilliseconds, currentFloor, destinationFloor);
            
            var request = new ElevatorRequest(currentFloor, destinationFloor);
            requests.Add(request);
            await elevatorController.AddRequestAsync(request);
        }
        
        logger.LogInformation("All requests submitted. Waiting for completion...");
        
        var allCompleted = false;
        
        while (!allCompleted)
        {
            await Task.Delay(200);
            
            // Movement path is pre-calculated based on expected behavior
            
            var completedCount = 0;
            foreach (var request in requests)
            {
                var updatedRequest = await requestRepository.GetByIdAsync(request.Id);
                if (updatedRequest?.Status == ElevatorRequestStatus.Completed)
                {
                    completedCount++;
                }
            }
            
            allCompleted = completedCount == requests.Count;
        }
        
        // Wait a moment for final elevator actions to complete
        await Task.Delay(2000);
        
        // Get final elevator status and verify idle state
        var elevators = await elevatorRepository.GetAllAsync();
        var lastDestinationFloor = scenarios.Last().destinationFloor;
        
        foreach (var elevator in elevators)
        {
            logger.LogInformation("Elevator {ElevatorId}: Floor {CurrentFloor}, Status {Status}, Direction {Direction}", 
                elevator.Id, elevator.CurrentFloor, elevator.Status, elevator.Direction);
            
            // Verify elevator is idling at the expected floor
            if (elevator.CurrentFloor == lastDestinationFloor && 
                elevator.Status == ElevatorStatus.Idle && 
                elevator.Direction == ElevatorDirection.Idle)
            {
                logger.LogInformation("Elevator {ElevatorId} is correctly idling at floor {Floor} (last destination)", 
                    elevator.Id, elevator.CurrentFloor);
                Console.WriteLine($"Elevator {elevator.Id} is correctly idling at floor {elevator.CurrentFloor} (last destination)");
            }
            else if (elevator.CurrentFloor == lastDestinationFloor)
            {
                logger.LogWarning("Elevator {ElevatorId} is at correct floor {Floor} but status is {Status}, direction is {Direction}", 
                    elevator.Id, elevator.CurrentFloor, elevator.Status, elevator.Direction);
                Console.WriteLine($"Elevator {elevator.Id} is at correct floor {elevator.CurrentFloor} but not fully idle");
            }
            else
            {
                logger.LogWarning("Elevator {ElevatorId} is at floor {CurrentFloor} but should be at floor {ExpectedFloor}", 
                    elevator.Id, elevator.CurrentFloor, lastDestinationFloor);
                Console.WriteLine($"Elevator {elevator.Id} is at floor {elevator.CurrentFloor} but should be at floor {lastDestinationFloor}");
            }
        }
        
        logger.LogInformation("=== Single Elevator Simulation Complete ===");
    }
}