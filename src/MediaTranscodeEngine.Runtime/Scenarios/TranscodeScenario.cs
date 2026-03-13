using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Scenarios;

/*
Это базовая абстракция прикладного сценария transcoding.
Она инкапсулирует domain-правила построения tool-agnostic плана и при необходимости дополнительного execution payload.
*/
/// <summary>
/// Encapsulates domain rules that inspect a source video and produce a tool-agnostic transcode plan.
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
    /// Builds a tool-agnostic transcode plan for the provided source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>A tool-agnostic transcode plan.</returns>
    public TranscodePlan BuildPlan(SourceVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        var plan = BuildPlanCore(video);
        return plan ?? throw new InvalidOperationException($"Scenario '{Name}' returned null transcode plan.");
    }

    /// <summary>
    /// Builds an optional scenario-specific execution payload for the supplied source video and shared plan.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <param name="plan">Already built shared transcode plan.</param>
    /// <returns>Scenario-specific execution payload when needed; otherwise <see langword="null"/>.</returns>
    public TranscodeExecutionSpec? BuildExecutionSpec(SourceVideo video, TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        return BuildExecutionSpecCore(video, plan);
    }

    /// <summary>
    /// Builds a tool-agnostic transcode plan for the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>A tool-agnostic transcode plan.</returns>
    protected abstract TranscodePlan BuildPlanCore(SourceVideo video);

    /// <summary>
    /// Builds an optional scenario-specific execution payload for the supplied source video and shared plan.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <param name="plan">Already built shared transcode plan.</param>
    /// <returns>Scenario-specific execution payload when needed; otherwise <see langword="null"/>.</returns>
    protected virtual TranscodeExecutionSpec? BuildExecutionSpecCore(SourceVideo video, TranscodePlan plan)
    {
        return null;
    }
}
