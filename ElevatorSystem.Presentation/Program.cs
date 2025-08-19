using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Models;
using ElevatorSystem.Application.Events;
using ElevatorSystem.Application.Events.Handlers;
using ElevatorSystem.Application.Events.ElevatorEvents;
using ElevatorSystem.Application.Events.CommandHandlers;
using ElevatorSystem.Application.Events.ElevatorCommands;
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
        
        // Initialize event infrastructure  
        await InitializeEventHandlers(host.Services);
        
        // Initialize command infrastructure
        await InitializeCommandHandlers(host.Services);
        
        var elevatorController = host.Services.GetRequiredService<IElevatorController>();
        var elevatorRepository = host.Services.GetRequiredService<IElevatorRepository>();
        var requestRepository = host.Services.GetRequiredService<IElevatorRequestRepository>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        
        var cts = new CancellationTokenSource();
        
        // Start elevator processing for each elevator
        var elevators = await elevatorRepository.GetAllAsync();
        var elevatorTasks = elevators.Select(elevator => 
            elevatorController.ProcessElevatorAsync(elevator, cts.Token)).ToArray();
        
        var scenarioReader = host.Services.GetRequiredService<IScenarioReader>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        
        // Run simulation
        await RunSimulation(elevatorController, requestRepository, elevatorRepository, scenarioReader, configuration, logger);
        
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
        
        // Dispose elevator controller to clean up resources
        if (elevatorController is IDisposable disposableController)
        {
            disposableController.Dispose();
        }
        
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
                services.Configure<SimulationSettings>(context.Configuration.GetSection(SimulationSettings.SectionName));
                services.Configure<RequestProcessingSettings>(context.Configuration.GetSection(RequestProcessingSettings.SectionName));
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

    private static async Task RunSimulation(IElevatorController elevatorController, IElevatorRequestRepository requestRepository, IElevatorRepository elevatorRepository, IScenarioReader scenarioReader, IConfiguration configuration, ILogger<Program> logger)
    {
        logger.LogInformation("=== Single Elevator System Simulation ===");
        
        var simulationSettings = new SimulationSettings();
        configuration.GetSection(SimulationSettings.SectionName).Bind(simulationSettings);
        
        IEnumerable<ElevatorScenario> scenarios;
        try
        {
            scenarios = await scenarioReader.ReadScenariosAsync(simulationSettings.ScenarioFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load scenarios from file: {FilePath}", simulationSettings.ScenarioFilePath);
            throw;
        }

        var requests = new List<ElevatorRequest>();
        var simulationStartTime = DateTime.UtcNow;
        
        foreach (var scenario in scenarios)
        {
            // Calculate how long to wait based on the request time
            var elapsedMs = (DateTime.UtcNow - simulationStartTime).TotalMilliseconds;
            var waitTimeMs = Math.Max(0, scenario.RequestTimeMs - elapsedMs);
            
            if (waitTimeMs > 0)
            {
                await Task.Delay((int)waitTimeMs);
            }
            
            logger.LogInformation("t={ElapsedTime}ms: Requesting elevator from floor {CurrentFloor} to floor {DestinationFloor}", 
                (int)(DateTime.UtcNow - simulationStartTime).TotalMilliseconds, scenario.CurrentFloor, scenario.DestinationFloor);
            
            var request = new ElevatorRequest(scenario.CurrentFloor, scenario.DestinationFloor);
            var success = await elevatorController.AddRequestAsync(request);
            
            // Only track requests that were successfully added to the system
            if (success)
            {
                requests.Add(request);
            }
            else
            {
                logger.LogWarning("Request from floor {CurrentFloor} to {DestinationFloor} was rejected by the system", 
                    scenario.CurrentFloor, scenario.DestinationFloor);
            }
        }
        
        logger.LogInformation("All requests submitted. Waiting for completion...");
        
        // Add timeout to prevent infinite waiting
        var timeout = TimeSpan.FromSeconds(60);
        var timeoutCts = new CancellationTokenSource(timeout);
        var allCompleted = false;
        var completedCount = 0;
        
        while (!allCompleted && !timeoutCts.Token.IsCancellationRequested)
        {
            await Task.Delay(200, timeoutCts.Token);
            
            // Movement path is pre-calculated based on expected behavior
            
            completedCount = 0;
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
        
        if (timeoutCts.Token.IsCancellationRequested && !allCompleted)
        {
            logger.LogWarning("Timeout reached while waiting for request completion. {CompletedCount}/{TotalCount} requests completed.", 
                completedCount, requests.Count);
        }
        
        // Wait a moment for final elevator actions to complete
        await Task.Delay(2000);
        
        // Get final elevator status and verify idle state
        await VerifyElevatorFloor(elevatorRepository, logger, scenarios);

        logger.LogInformation("=== Single Elevator Simulation Complete ===");
    }

    private static async Task VerifyElevatorFloor(IElevatorRepository elevatorRepository, ILogger<Program> logger,
        IEnumerable<ElevatorScenario> scenarios)
    {
        if (scenarios == null || scenarios.Count() == 0)
        {
            return;
        }

        var elevators = await elevatorRepository.GetAllAsync();
        var lastDestinationFloor = scenarios.Last().DestinationFloor;
        
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
    }

    private static async Task InitializeEventHandlers(IServiceProvider services)
    {
        var eventBus = services.GetRequiredService<IEventBus>();
        var eventLogger = services.GetRequiredService<ElevatorEventLogger>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Subscribe to all elevator events for logging and monitoring
        eventBus.Subscribe<ElevatorMovedEvent>(eventLogger.HandleElevatorMovedEvent);
        eventBus.Subscribe<PassengerPickedUpEvent>(eventLogger.HandlePassengerPickedUpEvent);
        eventBus.Subscribe<PassengerDroppedOffEvent>(eventLogger.HandlePassengerDroppedOffEvent);
        eventBus.Subscribe<ElevatorRequestReceivedEvent>(eventLogger.HandleElevatorRequestReceivedEvent);
        eventBus.Subscribe<ElevatorRequestAssignedEvent>(eventLogger.HandleElevatorRequestAssignedEvent);
        eventBus.Subscribe<ElevatorStatusChangedEvent>(eventLogger.HandleElevatorStatusChangedEvent);

        logger.LogInformation("Event handlers initialized successfully");
        
        await Task.CompletedTask;
    }

    private static async Task InitializeCommandHandlers(IServiceProvider services)
    {
        var commandBus = services.GetRequiredService<ICommandBus>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Register command handlers for request processing pipeline
        commandBus.RegisterHandler<SubmitElevatorRequestCommand, bool>(
            services.GetRequiredService<SubmitElevatorRequestCommandHandler>());
        commandBus.RegisterHandler<AddElevatorRequestCommand, bool>(
            services.GetRequiredService<AddElevatorRequestCommandHandler>());
        commandBus.RegisterHandler<ProcessElevatorCommand>(
            services.GetRequiredService<ProcessElevatorCommandHandler>());
        commandBus.RegisterHandler<UpdateRequestStatusCommand>(
            services.GetRequiredService<UpdateRequestStatusCommandHandler>());

        logger.LogInformation("Command handlers initialized successfully");
        
        await Task.CompletedTask;
    }
}