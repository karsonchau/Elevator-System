namespace ElevatorSystem.Application.Events;

/// <summary>
/// Interface for command validators that validate commands before processing.
/// </summary>
/// <typeparam name="TCommand">The type of command to validate</typeparam>
public interface ICommandValidator<in TCommand> where TCommand : ICommand
{
    /// <summary>
    /// Validates the specified command.
    /// </summary>
    /// <param name="command">The command to validate</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Validation result indicating success or failure with details</returns>
    Task<CommandValidationResult> ValidateAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of command validation.
/// </summary>
public class CommandValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }

    private CommandValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public static CommandValidationResult Success() => new(true, Array.Empty<string>());
    
    public static CommandValidationResult Failure(params string[] errors) => new(false, errors);
    
    public static CommandValidationResult Failure(IEnumerable<string> errors) => new(false, errors.ToList());
}