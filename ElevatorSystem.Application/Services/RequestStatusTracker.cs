using ElevatorSystem.Application.Configuration;
using ElevatorSystem.Application.Events;
using ElevatorSystem.Application.Events.ElevatorCommands;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ElevatorSystem.Application.Services;

/// <summary>
/// Production-ready request status tracker with timeout monitoring
/// </summary>
public class RequestStatusTracker : IRequestStatusTracker, IDisposable
{
    private readonly RequestProcessingSettings _settings;
    private readonly ICommandBus _commandBus;
    private readonly IElevatorRequestRepository _requestRepository;
    private readonly ILogger<RequestStatusTracker> _logger;
    private readonly Timer _timeoutCheckTimer;
    
    // Tracking data structures
    private readonly ConcurrentDictionary<Guid, RequestTrackingInfo> _trackedRequests = new();
    private readonly object _statsLock = new();
    private int _totalTrackedRequests = 0;
    private int _timedOutRequests = 0;
    private readonly List<TimeSpan> _completedRequestTimes = new();
    private volatile bool _disposed = false;

    public RequestStatusTracker(
        IOptions<RequestProcessingSettings> settings,
        ICommandBus commandBus,
        IElevatorRequestRepository requestRepository,
        ILogger<RequestStatusTracker> logger)
    {
        _settings = settings.Value;
        _commandBus = commandBus;
        _requestRepository = requestRepository;
        _logger = logger;
        
        // Start timeout monitoring timer
        _timeoutCheckTimer = new Timer(CheckForTimeouts, null, 
            TimeSpan.FromMilliseconds(_settings.TimeoutCheckIntervalMs),
            TimeSpan.FromMilliseconds(_settings.TimeoutCheckIntervalMs));
    }

    public async Task StartTrackingAsync(ElevatorRequest request)
    {
        if (request == null)
            return;

        var trackingInfo = new RequestTrackingInfo
        {
            RequestId = request.Id,
            StartTime = DateTime.UtcNow,
            LastStatusUpdate = DateTime.UtcNow,
            CurrentStatus = request.Status,
            TimeoutThreshold = TimeSpan.FromMilliseconds(_settings.RequestTimeoutMs)
        };

        _trackedRequests.TryAdd(request.Id, trackingInfo);
        
        lock (_statsLock)
        {
            _totalTrackedRequests++;
        }

        _logger.LogDebug("Started tracking request {RequestId} with timeout threshold {TimeoutMs}ms", 
            request.Id, _settings.RequestTimeoutMs);

        await Task.CompletedTask;
    }

    public async Task StopTrackingAsync(Guid requestId)
    {
        if (_trackedRequests.TryRemove(requestId, out var trackingInfo))
        {
            var processingTime = DateTime.UtcNow - trackingInfo.StartTime;
            
            lock (_statsLock)
            {
                _completedRequestTimes.Add(processingTime);
                
                // Keep only recent completion times for average calculation
                if (_completedRequestTimes.Count > 100)
                {
                    _completedRequestTimes.RemoveAt(0);
                }
            }

            _logger.LogDebug("Stopped tracking request {RequestId} after {ProcessingTimeMs}ms", 
                requestId, processingTime.TotalMilliseconds);
        }

        await Task.CompletedTask;
    }

    public async Task UpdateRequestStatusAsync(Guid requestId, ElevatorRequestStatus newStatus, string? reason = null)
    {
        if (_trackedRequests.TryGetValue(requestId, out var trackingInfo))
        {
            var previousStatus = trackingInfo.CurrentStatus;
            trackingInfo.CurrentStatus = newStatus;
            trackingInfo.LastStatusUpdate = DateTime.UtcNow;

            _logger.LogDebug("Updated tracking status for request {RequestId} from {PreviousStatus} to {NewStatus}. Reason: {Reason}", 
                requestId, previousStatus, newStatus, reason ?? "None specified");

            // Stop tracking if request is in a final state
            if (newStatus == ElevatorRequestStatus.Completed || 
                newStatus == ElevatorRequestStatus.Failed || 
                newStatus == ElevatorRequestStatus.Cancelled)
            {
                await StopTrackingAsync(requestId);
            }
        }
    }

    public async Task<IList<ElevatorRequest>> GetTimedOutRequestsAsync()
    {
        var timedOutRequests = new List<ElevatorRequest>();
        var now = DateTime.UtcNow;

        foreach (var trackingInfo in _trackedRequests.Values)
        {
            var elapsed = now - trackingInfo.StartTime;
            
            if (elapsed > trackingInfo.TimeoutThreshold)
            {
                try
                {
                    var request = await _requestRepository.GetByIdAsync(trackingInfo.RequestId);
                    if (request != null)
                    {
                        timedOutRequests.Add(request);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving timed out request {RequestId}", trackingInfo.RequestId);
                }
            }
        }

        return timedOutRequests;
    }

    public RequestTrackingStatistics GetStatistics()
    {
        var now = DateTime.UtcNow;
        var statusCounts = new Dictionary<ElevatorRequestStatus, int>();
        var oldestActiveTime = (DateTime?)null;

        foreach (var trackingInfo in _trackedRequests.Values)
        {
            // Count by status
            if (statusCounts.ContainsKey(trackingInfo.CurrentStatus))
                statusCounts[trackingInfo.CurrentStatus]++;
            else
                statusCounts[trackingInfo.CurrentStatus] = 1;

            // Find oldest active request
            if (oldestActiveTime == null || trackingInfo.StartTime < oldestActiveTime)
                oldestActiveTime = trackingInfo.StartTime;
        }

        TimeSpan averageProcessingTime;
        lock (_statsLock)
        {
            averageProcessingTime = _completedRequestTimes.Any() 
                ? TimeSpan.FromMilliseconds(_completedRequestTimes.Average(t => t.TotalMilliseconds))
                : TimeSpan.Zero;
        }

        return new RequestTrackingStatistics
        {
            TotalTrackedRequests = _totalTrackedRequests,
            ActiveRequests = _trackedRequests.Count,
            TimedOutRequests = _timedOutRequests,
            RequestsByStatus = statusCounts,
            AverageProcessingTime = averageProcessingTime,
            OldestActiveRequestTime = oldestActiveTime
        };
    }

    private async void CheckForTimeouts(object? state)
    {
        if (_disposed)
            return;

        try
        {
            var timedOutRequests = await GetTimedOutRequestsAsync();
            
            foreach (var request in timedOutRequests)
            {
                _logger.LogWarning("Request {RequestId} has timed out after {TimeoutMs}ms", 
                    request.Id, _settings.RequestTimeoutMs);

                lock (_statsLock)
                {
                    _timedOutRequests++;
                }

                // Update request status to failed due to timeout
                try
                {
                    var updateCommand = new UpdateRequestStatusCommand(
                        request.Id, 
                        ElevatorRequestStatus.Failed, 
                        request.Status, 
                        $"Request timed out after {_settings.RequestTimeoutMs}ms");
                    
                    await _commandBus.SendAsync(updateCommand);
                    await StopTrackingAsync(request.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling timeout for request {RequestId}", request.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during timeout check");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _timeoutCheckTimer?.Dispose();
            _logger.LogInformation("RequestStatusTracker disposed");
        }
    }

    private class RequestTrackingInfo
    {
        public Guid RequestId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastStatusUpdate { get; set; }
        public ElevatorRequestStatus CurrentStatus { get; set; }
        public TimeSpan TimeoutThreshold { get; set; }
    }
}