using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Application.Interfaces;

public interface IElevatorController
{
    /// <summary>
    /// Adds an elevator request to the controller for processing.
    /// </summary>
    /// <param name="request">The elevator request to add.</param>
    /// <returns>True if the request was successfully added, false if validation failed or no elevators are available.</returns>
    Task<bool> AddRequestAsync(ElevatorRequest request);
    
    Task ProcessElevatorAsync(Elevator elevator, CancellationToken cancellationToken);
}