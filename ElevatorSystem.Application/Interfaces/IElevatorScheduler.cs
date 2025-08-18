using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Interfaces;

public interface IElevatorScheduler
{
    Task<Elevator> AssignElevatorAsync(ElevatorRequest request);
    Task<IEnumerable<Elevator>> GetAvailableElevatorsAsync();
}