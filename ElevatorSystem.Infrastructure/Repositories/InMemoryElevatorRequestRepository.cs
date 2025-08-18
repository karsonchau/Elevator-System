using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using System.Collections.Concurrent;

namespace ElevatorSystem.Infrastructure.Repositories;

public class InMemoryElevatorRequestRepository : IElevatorRequestRepository
{
    private readonly ConcurrentDictionary<Guid, ElevatorRequest> _requests = new();

    public Task<ElevatorRequest?> GetByIdAsync(Guid id)
    {
        _requests.TryGetValue(id, out var request);
        return Task.FromResult(request);
    }

    public Task AddAsync(ElevatorRequest request)
    {
        _requests.TryAdd(request.Id, request);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ElevatorRequest request)
    {
        _requests.AddOrUpdate(request.Id, request, (key, oldValue) => request);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ElevatorRequest>> GetPendingRequestsAsync()
    {
        var pendingRequests = _requests.Values
            .Where(r => r.Status == ElevatorRequestStatus.Pending)
            .AsEnumerable();
        
        return Task.FromResult(pendingRequests);
    }
}