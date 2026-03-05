using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Execution;

public interface ITranscodeExecutionPipeline
{
    string ProcessByKey(string strategyKey, TranscodeRequest request);

    string ProcessByKeyWithProbeResult(string strategyKey, TranscodeRequest request, ProbeResult? probe);

    string ProcessByKeyWithProbeJson(string strategyKey, TranscodeRequest request, string? probeJson);
}
