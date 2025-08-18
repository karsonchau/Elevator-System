namespace ElevatorSystem.Application.Events;

/// <summary>
/// Base implementation for all commands with common properties
/// </summary>
public abstract class BaseCommand : ICommand
{
    protected BaseCommand(string? correlationId = null)
    {
        CommandId = Guid.NewGuid();
        Timestamp = DateTime.UtcNow;
        CommandType = GetType().Name;
        CorrelationId = correlationId;
    }
    
    public Guid CommandId { get; }
    public DateTime Timestamp { get; }
    public string CommandType { get; }
    public string? CorrelationId { get; }
}