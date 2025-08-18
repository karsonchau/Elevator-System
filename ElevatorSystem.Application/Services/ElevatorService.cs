using System.Threading.Channels;
using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElevatorSystem.Application.Services;

public class ElevatorService : IElevatorService
{
    private readonly Channel<ElevatorRequest> _requestChannel;
    private readonly ChannelWriter<ElevatorRequest> _requestWriter;
    private readonly ChannelReader<ElevatorRequest> _requestReader;
    private readonly IElevatorRequestRepository _requestRepository;
    private readonly IElevatorRepository _elevatorRepository;
    private readonly ILogger<ElevatorService> _logger;
    private readonly IElevatorController _elevatorController;

    public ElevatorService(
        IElevatorRequestRepository requestRepository,
        IElevatorRepository elevatorRepository,
        ILogger<ElevatorService> logger,
        IElevatorController elevatorController,
        IOptions<ElevatorSettings> elevatorSettings)
    {
        _requestRepository = requestRepository;
        _elevatorRepository = elevatorRepository;
        _logger = logger;
        _elevatorController = elevatorController;
        
        var settings = elevatorSettings.Value;
        var numberOfFloors = settings.MaxFloor - settings.MinFloor + 1;
        var channelCapacity = CalculateChannelCapacity(numberOfFloors);
        
        _logger.LogInformation("Initializing elevator service with channel capacity {ChannelCapacity} for {NumberOfFloors} floors", 
            channelCapacity, numberOfFloors);

        var channelOptions = new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _requestChannel = Channel.CreateBounded<ElevatorRequest>(channelOptions);
        _requestWriter = _requestChannel.Writer;
        _requestReader = _requestChannel.Reader;
    }

    /// <summary>
    /// Calculates the appropriate channel capacity based on the number of floors.
    /// Uses 2n formula to ensure sufficient capacity for concurrent requests.
    /// </summary>
    /// <param name="numberOfFloors">The total number of floors in the building.</param>
    /// <returns>The calculated channel capacity.</returns>
    private static int CalculateChannelCapacity(int numberOfFloors)
    {
        return numberOfFloors * 2;
    }

    public async Task<Guid> RequestElevatorAsync(int currentFloor, int destinationFloor)
    {
        var request = new ElevatorRequest(currentFloor, destinationFloor);
        
        await _requestRepository.AddAsync(request);
        await _requestWriter.WriteAsync(request);
        
        _logger.LogInformation("Elevator request created: {RequestId} from floor {CurrentFloor} to {DestinationFloor}", 
            request.Id, currentFloor, destinationFloor);
        
        return request.Id;
    }

    public async Task ProcessRequestsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting elevator request processing");

        var requestProcessingTask = ProcessIncomingRequests(cancellationToken);
        var elevatorTasks = await StartElevatorProcessing(cancellationToken);

        await Task.WhenAny(requestProcessingTask, Task.WhenAll(elevatorTasks));
    }

    private async Task<Task[]> StartElevatorProcessing(CancellationToken cancellationToken)
    {
        var elevators = await _elevatorRepository.GetAllAsync();
        
        var elevatorTasks = elevators.Select(elevator => 
            Task.Run(() => _elevatorController.ProcessElevatorAsync(elevator, cancellationToken)))
            .ToArray();

        _logger.LogInformation("Started processing for {ElevatorCount} elevators", elevators.Count());
        return elevatorTasks;
    }

    private async Task ProcessIncomingRequests(CancellationToken cancellationToken)
    {
        await foreach (var request in _requestReader.ReadAllAsync(cancellationToken))
        {
            try
            {
                _logger.LogInformation("Processing incoming request: {RequestId}", request.Id);
                
                await _elevatorController.AddRequestAsync(request);
                
                _logger.LogInformation("Added request {RequestId} to elevator controller", request.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing incoming request: {RequestId}", request.Id);
            }
        }
    }

    public async Task<ElevatorRequest?> GetRequestStatusAsync(Guid requestId)
    {
        return await _requestRepository.GetByIdAsync(requestId);
    }

    public async Task<IEnumerable<Elevator>> GetElevatorStatusAsync()
    {
        return await _elevatorRepository.GetAllAsync();
    }
}