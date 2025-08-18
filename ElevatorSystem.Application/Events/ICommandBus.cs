namespace ElevatorSystem.Application.Events;

/// <summary>
/// Command bus interface for sending commands and getting responses (CQRS pattern)
/// </summary>
public interface ICommandBus
{
    /// <summary>
    /// Send a command for processing
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
}