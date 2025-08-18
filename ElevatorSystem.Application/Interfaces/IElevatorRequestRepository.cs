using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Interfaces;

public interface IElevatorRequestRepository
{
    Task<ElevatorRequest?> GetByIdAsync(Guid id);
    Task AddAsync(ElevatorRequest request);
    Task UpdateAsync(ElevatorRequest request);
    Task<IEnumerable<ElevatorRequest>> GetPendingRequestsAsync();
}