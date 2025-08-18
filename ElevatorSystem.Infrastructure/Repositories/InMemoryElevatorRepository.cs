using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using System.Collections.Concurrent;

namespace ElevatorSystem.Infrastructure.Repositories;

public class InMemoryElevatorRepository : IElevatorRepository
{
    private readonly ConcurrentDictionary<int, Elevator> _elevators = new();

    public Task<IEnumerable<Elevator>> GetAllAsync()
    {
        return Task.FromResult(_elevators.Values.AsEnumerable());
    }

    public Task<Elevator?> GetByIdAsync(int id)
    {
        _elevators.TryGetValue(id, out var elevator);
        return Task.FromResult(elevator);
    }

    public Task UpdateAsync(Elevator elevator)
    {
        _elevators.AddOrUpdate(elevator.Id, elevator, (key, oldValue) => elevator);
        return Task.CompletedTask;
    }

    public Task AddAsync(Elevator elevator)
    {
        _elevators.TryAdd(elevator.Id, elevator);
        return Task.CompletedTask;
    }
}