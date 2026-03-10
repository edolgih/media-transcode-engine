namespace MediaTranscodeEngine.Runtime.Tools;

/*
Это результат рендеринга команды конкретным инструментом.
Он хранит имя инструмента и итоговую последовательность команд для выполнения.
*/
/// <summary>
/// Represents the executable command sequence prepared by a concrete transcode tool.
/// </summary>
public sealed record ToolExecution
{
    /// <summary>
    /// Initializes an execution recipe for a concrete transcode tool.
    /// </summary>
    /// <param name="toolName">Stable concrete tool name.</param>
    /// <param name="commands">Command sequence to execute.</param>
    public ToolExecution(
        string toolName,
        IReadOnlyList<string>? commands)
    {
        ToolName = NormalizeToolName(toolName);
        Commands = NormalizeCommands(commands);
    }

    /// <summary>
    /// Gets the stable concrete tool name.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// Gets the ordered command sequence prepared by the tool.
    /// </summary>
    public IReadOnlyList<string> Commands { get; }

    /// <summary>
    /// Gets a value indicating whether the execution recipe contains no commands.
    /// </summary>
    public bool IsEmpty => Commands.Count == 0;

    /// <summary>
    /// Creates an execution recipe with a single command.
    /// </summary>
    /// <param name="toolName">Stable concrete tool name.</param>
    /// <param name="command">Single command to execute.</param>
    /// <returns>An execution recipe containing one command.</returns>
    public static ToolExecution Single(string toolName, string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return new ToolExecution(toolName, [command.Trim()]);
    }

    private static string NormalizeToolName(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return toolName.Trim();
    }

    private static IReadOnlyList<string> NormalizeCommands(IReadOnlyList<string>? commands)
    {
        if (commands is null || commands.Count == 0)
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
