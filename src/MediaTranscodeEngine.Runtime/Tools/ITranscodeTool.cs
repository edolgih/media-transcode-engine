using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tools;

/*
Это общий контракт tool-адаптера.
Он принимает общий план транскодирования и решает, может ли конкретный инструмент его выполнить.
*/
/// <summary>
/// Converts a tool-agnostic transcode plan into an executable recipe for a concrete transcode tool.
/// </summary>
public interface ITranscodeTool
{
    /// <summary>
    /// Gets the stable tool name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines whether the tool can execute the supplied plan.
    /// </summary>
    /// <param name="plan">Tool-agnostic transcode plan.</param>
    /// <returns><see langword="true"/> when the tool can execute the plan; otherwise <see langword="false"/>.</returns>
    bool CanHandle(TranscodePlan plan);

    /// <summary>
    /// Builds an executable recipe for the supplied source video and transcode plan.
    /// </summary>
    /// <param name="video">Normalized source video facts.</param>
    /// <param name="plan">Tool-agnostic transcode plan.</param>
    /// <returns>A concrete execution recipe for this tool.</returns>
    ToolExecution BuildExecution(SourceVideo video, TranscodePlan plan);
}
