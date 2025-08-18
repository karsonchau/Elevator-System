namespace ElevatorSystem.Application.Events;

/// <summary>
/// Base interface for all commands in the system (CQRS pattern)
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Unique identifier for this command instance
    /// </summary>
    Guid CommandId { get; }
    
    /// <summary>
    /// UTC timestamp when the command was issued
    /// </summary>
    DateTime Timestamp { get; }
    
    /// <summary>
    /// Type name of the command for routing
    /// </summary>
    string CommandType { get; }
    
    /// <summary>
    /// Optional correlation ID to track related commands and events
    /// </summary>
    string? CorrelationId { get; }
}