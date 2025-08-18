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
    private readonly ConcurrentDictionary<Type, object> _validators = new();
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
            commandType.Name, command.CommandId);

        // Validate command if validator is registered
        await ValidateCommandAsync(command, cancellationToken);

        if (!_handlers.TryGetValue(commandType, out var handlerObj))
        {
            _logger.LogWarning("No handler registered for command type {CommandType}", commandType.Name);
            throw new InvalidOperationException($"No handler registered for command type {commandType.Name}");
        }

        if (handlerObj is ICommandHandler<T> handler)
        {
            try
            {
                await handler.HandleAsync(command, cancellationToken);
                _logger.LogDebug("Completed processing command {CommandType} with ID {CommandId}", 
                    commandType.Name, command.CommandId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command {CommandType} with ID {CommandId}", 
                    commandType.Name, command.CommandId);
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
            commandType.Name, command.CommandId, typeof(TResponse).Name);

        // Validate command if validator is registered
        await ValidateCommandAsync(command, cancellationToken);

        if (!_handlers.TryGetValue(commandType, out var handlerObj))
        {
            _logger.LogWarning("No handler registered for command type {CommandType}", commandType.Name);
            throw new InvalidOperationException($"No handler registered for command type {commandType.Name}");
        }

        if (handlerObj is ICommandHandler<TCommand, TResponse> handler)
        {
            try
            {
                var response = await handler.HandleAsync(command, cancellationToken);
                _logger.LogDebug("Completed processing command {CommandType} with ID {CommandId}", 
                    commandType.Name, command.CommandId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command {CommandType} with ID {CommandId}", 
                    commandType.Name, command.CommandId);
                throw;
            }
        }
        else
        {
            throw new InvalidOperationException($"Invalid handler type for command {commandType.Name}");
        }
    }

    public void RegisterHandler<T>(ICommandHandler<T> handler) where T : class, ICommand
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var commandType = typeof(T);
        _handlers.TryAdd(commandType, handler);
        
        _logger.LogDebug("Registered handler for command type {CommandType}", commandType.Name);
    }

    public void RegisterHandler<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> handler) 
        where TCommand : class, ICommand
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var commandType = typeof(TCommand);
        _handlers.TryAdd(commandType, handler);
        
        _logger.LogDebug("Registered handler for command type {CommandType} with response type {ResponseType}", 
            commandType.Name, typeof(TResponse).Name);
    }

    public void RegisterValidator<T>(ICommandValidator<T> validator) where T : class, ICommand
    {
        if (validator == null)
            throw new ArgumentNullException(nameof(validator));

        var commandType = typeof(T);
        _validators.TryAdd(commandType, validator);
        
        _logger.LogDebug("Registered validator for command type {CommandType}", commandType.Name);
    }

    private async Task ValidateCommandAsync<T>(T command, CancellationToken cancellationToken) where T : class, ICommand
    {
        var commandType = typeof(T);
        
        if (_validators.TryGetValue(commandType, out var validatorObj) && 
            validatorObj is ICommandValidator<T> validator)
        {
            var validationResult = await validator.ValidateAsync(command, cancellationToken);
            
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors);
                _logger.LogWarning("Command validation failed for {CommandType} with ID {CommandId}: {Errors}", 
                    commandType.Name, command.CommandId, errors);
                    
                throw new ArgumentException($"Command validation failed: {errors}");
            }
        }
    }
}