namespace ElevatorSystem.Domain.Enums;

/// <summary>
/// Enhanced elevator request status for robust request processing pipeline
/// </summary>
public enum ElevatorRequestStatus
{
    /// <summary>
    /// Initial state when request is first created
    /// </summary>
    Pending,
    
    /// <summary>
    /// Request has passed validation and is ready for processing
    /// </summary>
    Validated,
    
    /// <summary>
    /// Request has been assigned to a specific elevator
    /// </summary>
    Assigned,
    
    /// <summary>
    /// Request is currently being executed (passenger pickup/dropoff)
    /// </summary>
    InProgress,
    
    /// <summary>
    /// Request was successfully completed
    /// </summary>
    Completed,
    
    /// <summary>
    /// Request was cancelled by user or system
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// Request is currently being retried due to previous failure
    /// </summary>
    Retrying,
    
    /// <summary>
    /// Request failed permanently after all retry attempts
    /// </summary>
    Failed
}