using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;

namespace ElevatorSystem.Application.Interfaces;

/// <summary>
/// Tracks elevator request status and monitors for timeouts
/// </summary>
public interface IRequestStatusTracker
{
    /// <summary>
    /// Starts tracking a request for timeout monitoring
    /// </summary>
    /// <param name="request">The request to track</param>
    Task StartTrackingAsync(ElevatorRequest request);

    /// <summary>
    /// Stops tracking a request (when completed or failed)
    /// </summary>
    /// <param name="requestId">ID of the request to stop tracking</param>
    Task StopTrackingAsync(Guid requestId);

    /// <summary>
    /// Updates the status of a tracked request
    /// </summary>
    /// <param name="requestId">ID of the request</param>
    /// <param name="newStatus">New status</param>
    /// <param name="reason">Reason for status change</param>
    Task UpdateRequestStatusAsync(Guid requestId, ElevatorRequestStatus newStatus, string? reason = null);

    /// <summary>
    /// Gets requests that have timed out and need intervention
    /// </summary>
    /// <returns>List of timed out requests</returns>
    Task<IList<ElevatorRequest>> GetTimedOutRequestsAsync();

    /// <summary>
    /// Gets tracking statistics for monitoring
    /// </summary>
    /// <returns>Request tracking statistics</returns>
    RequestTrackingStatistics GetStatistics();
}

/// <summary>
/// Statistics for request tracking monitoring
/// </summary>
public class RequestTrackingStatistics
{
    public int TotalTrackedRequests { get; set; }
    public int ActiveRequests { get; set; }
    public int TimedOutRequests { get; set; }
    public Dictionary<ElevatorRequestStatus, int> RequestsByStatus { get; set; } = new();
    public TimeSpan AverageProcessingTime { get; set; }
    public DateTime? OldestActiveRequestTime { get; set; }
}