using Transcode.Core.Videos;

namespace Transcode.Core.Scenarios;

/*
Это базовая абстракция прикладного сценария transcoding.
Она инкапсулирует domain-правила сценария и возвращает либо info-output, либо готовую последовательность команд.
*/
/// <summary>
/// Encapsulates scenario-local domain rules that inspect a source video and produce either info output or executable commands.
/// </summary>
public abstract class TranscodeScenario
{
    /// <summary>
    /// Initializes a named scenario.
    /// </summary>
    /// <param name="name">Stable scenario name.</param>
    protected TranscodeScenario(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>
    /// Gets the stable scenario name used by callers to select this behavior.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Builds the scenario-specific info output for the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>Scenario-specific info output.</returns>
    public string FormatInfo(SourceVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        return FormatInfoCore(video);
    }

    /// <summary>
    /// Builds the scenario-specific execution recipe for the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>Scenario-specific execution recipe.</returns>
    public ScenarioExecution BuildExecution(SourceVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        var execution = BuildExecutionCore(video);
        return execution ?? throw new InvalidOperationException($"Scenario '{Name}' returned null execution.");
    }

    /// <summary>
    /// Builds the scenario-specific info output for the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>Scenario-specific info output.</returns>
    protected virtual string FormatInfoCore(SourceVideo video)
    {
        return string.Empty;
    }

    /// <summary>
    /// Builds the scenario-specific execution recipe for the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>Scenario-specific execution recipe.</returns>
    protected abstract ScenarioExecution BuildExecutionCore(SourceVideo video);
}
