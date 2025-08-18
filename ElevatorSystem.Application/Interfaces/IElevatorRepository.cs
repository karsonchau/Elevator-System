using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Interfaces;

public interface IElevatorRepository
{
    Task<IEnumerable<Elevator>> GetAllAsync();
    Task<Elevator?> GetByIdAsync(int id);
    Task UpdateAsync(Elevator elevator);
    Task AddAsync(Elevator elevator);
}