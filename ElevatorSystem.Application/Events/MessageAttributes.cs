namespace ElevatorSystem.Application.Events;

/// <summary>
/// Attribute to mark event handlers and provide metadata for external queue systems
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class EventHandlerAttribute : Attribute
{
    /// <summary>
    /// Topic name for event routing (used by RabbitMQ, Kafka, Azure Service Bus, etc.)
    /// </summary>
    public string? Topic { get; set; }
    
    /// <summary>
    /// Queue name for event processing (used by database tables, Redis streams, etc.)
    /// </summary>
    public string? Queue { get; set; }
    
    /// <summary>
    /// Whether events require ordered processing
    /// </summary>
    public bool RequiresOrdering { get; set; } = false;
    
    public EventHandlerAttribute(string? topic = null, string? queue = null, bool requiresOrdering = false)
    {
        Topic = topic;
        Queue = queue;
        RequiresOrdering = requiresOrdering;
    }
}

/// <summary>
/// Attribute to mark command handlers and provide metadata for external queue systems
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class CommandHandlerAttribute : Attribute
{
    /// <summary>
    /// Queue name for command routing (used by RabbitMQ, database tables, Redis, etc.)
    /// </summary>
    public string? Queue { get; set; }
    
    /// <summary>
    /// Processing priority (0 = lowest, higher numbers = higher priority)
    /// </summary>
    public int Priority { get; set; } = 0;
    
    /// <summary>
    /// Maximum number of retry attempts for failed commands
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Whether command processing requires ordering
    /// </summary>
    public bool RequiresOrdering { get; set; } = false;
    
    public CommandHandlerAttribute(string? queue = null, int priority = 0, int maxRetries = 3, bool requiresOrdering = false)
    {
        Queue = queue;
        Priority = priority;
        MaxRetries = maxRetries;
        RequiresOrdering = requiresOrdering;
    }
}

/// <summary>
/// Attribute to mark message types (events/commands) with routing metadata
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class MessageAttribute : Attribute
{
    /// <summary>
    /// Message type identifier for routing
    /// </summary>
    public string? MessageType { get; set; }
    
    /// <summary>
    /// Schema version for message evolution
    /// </summary>
    public string Version { get; set; } = "1.0";
    
    /// <summary>
    /// Whether message should be persisted
    /// </summary>
    public bool Persistent { get; set; } = true;
    
    public MessageAttribute(string? messageType = null, string version = "1.0", bool persistent = true)
    {
        MessageType = messageType;
        Version = version;
        Persistent = persistent;
    }
}