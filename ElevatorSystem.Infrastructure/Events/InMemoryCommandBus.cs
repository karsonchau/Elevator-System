using ElevatorSystem.Application.Events;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ElevatorSystem.Infrastructure.Events;

/// <summary>
/// In-memory command bus implementation for Phase 1
/// Note: This implementation is not suitable for production horizontal scaling
/// </summary>
public class InMemoryCommandBus : ICommandBus
{
    private readonly ConcurrentDictionary<Type, object> _handlers = new();
    private readonly ILogger<InMemoryCommandBus> _logger;

    public InMemoryCommandBus(ILogger<InMemoryCommandBus> logger)
    {
        _logger = logger;
    }

    public async Task SendAsync<T>(T command, CancellationToken cancellationToken = default) where T : class, ICommand
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var commandType = typeof(T);
        
        _logger.LogDebug("Sending command {CommandType} with ID {CommandId}", 
            command.CommandType, command.CommandId);

        if (!_handlers.TryGetValue(commandType, out var handlerObj))
        {
            _logger.LogWarning("No handler registered for command type {CommandType}", commandType.Name);
            throw new InvalidOperationException($"No handler registered for command type {commandType.Name}");
        }

        if (handlerObj is Func<T, CancellationToken, Task> handler)
        {
            try
            {
                await handler(command, cancellationToken);
                _logger.LogDebug("Completed processing command {CommandType} with ID {CommandId}", 
                    command.CommandType, command.CommandId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command {CommandType} with ID {CommandId}", 
                    command.CommandType, command.CommandId);
                throw;
            }
        }
        else
        {
            throw new InvalidOperationException($"Invalid handler type for command {commandType.Name}");
        }
    }

    public async Task<TResponse> SendAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : class, ICommand
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var commandType = typeof(TCommand);
        
        _logger.LogDebug("Sending command {CommandType} with ID {CommandId} expecting response {ResponseType}", 
            command.CommandType, command.CommandId, typeof(TResponse).Name);

        if (!_handlers.TryGetValue(commandType, out var handlerObj))
        {
            _logger.LogWarning("No handler registered for command type {CommandType}", commandType.Name);
            throw new InvalidOperationException($"No handler registered for command type {commandType.Name}");
        }

        if (handlerObj is Func<TCommand, CancellationToken, Task<TResponse>> handler)
        {
            try
            {
                var response = await handler(command, cancellationToken);
                _logger.LogDebug("Completed processing command {CommandType} with ID {CommandId}", 
                    command.CommandType, command.CommandId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command {CommandType} with ID {CommandId}", 
                    command.CommandType, command.CommandId);
                throw;
            }
        }
        else
        {
            throw new InvalidOperationException($"Invalid handler type for command {commandType.Name}");
        }
    }

    public void RegisterHandler<T>(Func<T, CancellationToken, Task> handler) where T : class, ICommand
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var commandType = typeof(T);
        _handlers.TryAdd(commandType, handler);
        
        _logger.LogDebug("Registered handler for command type {CommandType}", commandType.Name);
    }

    public void RegisterHandler<TCommand, TResponse>(Func<TCommand, CancellationToken, Task<TResponse>> handler) 
        where TCommand : class, ICommand
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var commandType = typeof(TCommand);
        _handlers.TryAdd(commandType, handler);
        
        _logger.LogDebug("Registered handler for command type {CommandType} with response type {ResponseType}", 
            commandType.Name, typeof(TResponse).Name);
    }
}