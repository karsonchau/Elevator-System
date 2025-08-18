namespace ElevatorSystem.Application.Events;

/// <summary>
/// Command bus interface for sending commands and getting responses (CQRS pattern)
/// </summary>
public interface ICommandBus
{
    /// <summary>
    /// Send a command for processing without expecting a response
    /// </summary>
    /// <typeparam name="T">Type of command to send</typeparam>
    /// <param name="command">The command instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendAsync<T>(T command, CancellationToken cancellationToken = default) where T : class, ICommand;
    
    /// <summary>
    /// Send a command and wait for a response
    /// </summary>
    /// <typeparam name="TCommand">Type of command to send</typeparam>
    /// <typeparam name="TResponse">Type of expected response</typeparam>
    /// <param name="command">The command instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TResponse> SendAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : class, ICommand;
        
    /// <summary>
    /// Register a command handler for a specific command type
    /// </summary>
    /// <typeparam name="TCommand">Type of command to handle</typeparam>
    /// <param name="handler">The command handler instance</param>
    void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : class, ICommand;
    
    /// <summary>
    /// Register a command handler for a specific command type with response
    /// </summary>
    /// <typeparam name="TCommand">Type of command to handle</typeparam>
    /// <typeparam name="TResponse">Type of response from the handler</typeparam>
    /// <param name="handler">The command handler instance</param>
    void RegisterHandler<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> handler) where TCommand : class, ICommand;
}