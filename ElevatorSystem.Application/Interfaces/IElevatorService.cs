using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Interfaces;

public interface IElevatorService
{
    Task<Guid> RequestElevatorAsync(int currentFloor, int destinationFloor);
    Task ProcessRequestsAsync(CancellationToken cancellationToken);
    Task<ElevatorRequest?> GetRequestStatusAsync(Guid requestId);
    Task<IEnumerable<Elevator>> GetElevatorStatusAsync();
}