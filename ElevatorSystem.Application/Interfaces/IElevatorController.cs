using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Interfaces;

public interface IElevatorController
{
    Task AddRequestAsync(ElevatorRequest request);
    Task ProcessElevatorAsync(Elevator elevator, CancellationToken cancellationToken);
}