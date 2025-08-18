using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Events.CommandHandlers;

/// <summary>
/// Base class for command handlers providing common functionality.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle</typeparam>
/// <typeparam name="TResult">The type of result returned by the command</typeparam>
public abstract class BaseCommandHandler<TCommand, TResult> : ICommandHandler<TCommand, TResult> 
    where TCommand : ICommand
{
    protected readonly ILogger Logger;

    protected BaseCommandHandler(ILogger logger)
    {
        Logger = logger;
    }

    public async Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Handling command {CommandType} with ID {CommandId}", 
            typeof(TCommand).Name, command.CommandId);

        try
        {
            var result = await ExecuteAsync(command, cancellationToken);
            
            Logger.LogDebug("Successfully handled command {CommandType} with ID {CommandId}", 
                typeof(TCommand).Name, command.CommandId);
                
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling command {CommandType} with ID {CommandId}: {Error}", 
                typeof(TCommand).Name, command.CommandId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes the command logic. Override this method in derived classes.
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The result of executing the command</returns>
    protected abstract Task<TResult> ExecuteAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for command handlers that don't return results.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle</typeparam>
public abstract class BaseCommandHandler<TCommand> : ICommandHandler<TCommand> 
    where TCommand : ICommand
{
    protected readonly ILogger Logger;

    protected BaseCommandHandler(ILogger logger)
    {
        Logger = logger;
    }

    public async Task HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Handling command {CommandType} with ID {CommandId}", 
            typeof(TCommand).Name, command.CommandId);

        try
        {
            await ExecuteAsync(command, cancellationToken);
            
            Logger.LogDebug("Successfully handled command {CommandType} with ID {CommandId}", 
                typeof(TCommand).Name, command.CommandId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling command {CommandType} with ID {CommandId}: {Error}", 
                typeof(TCommand).Name, command.CommandId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes the command logic. Override this method in derived classes.
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    protected abstract Task ExecuteAsync(TCommand command, CancellationToken cancellationToken);
}