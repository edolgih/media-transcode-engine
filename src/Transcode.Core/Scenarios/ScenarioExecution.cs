namespace Transcode.Core.Scenarios;

/*
Это минимальный общий результат выполнения сценария.
Он хранит только итоговую последовательность команд без отдельного tool-слоя в общем runtime flow.
*/
/// <summary>
/// Represents the executable command sequence prepared directly by a scenario.
/// </summary>
public sealed record ScenarioExecution
{
    /// <summary>
    /// Initializes a scenario execution recipe.
    /// </summary>
    /// <param name="commands">Command sequence to execute.</param>
    public ScenarioExecution(IReadOnlyList<string> commands)
    {
        Commands = NormalizeCommands(commands);
    }

    /// <summary>
    /// Gets the ordered command sequence prepared by the scenario.
    /// </summary>
    public IReadOnlyList<string> Commands { get; }

    /// <summary>
    /// Gets a value indicating whether the execution recipe contains no commands.
    /// </summary>
    public bool IsEmpty => Commands.Count == 0;

    /// <summary>
    /// Creates an execution recipe with a single command.
    /// </summary>
    /// <param name="command">Single command to execute.</param>
    /// <returns>An execution recipe containing one command.</returns>
    public static ScenarioExecution Single(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return new ScenarioExecution([command.Trim()]);
    }

    private static IReadOnlyList<string> NormalizeCommands(IReadOnlyList<string> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Count == 0)
        {
            return Array.Empty<string>();
        }

        return commands
            .Select(command =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(command);
                return command.Trim();
            })
            .ToArray();
    }
}
