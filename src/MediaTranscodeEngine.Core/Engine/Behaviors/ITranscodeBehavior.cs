namespace MediaTranscodeEngine.Core.Engine.Behaviors;

public interface ITranscodeBehavior
{
    bool CanHandle(TargetVideoCodec targetCodec, UnifiedTranscodeRequest request);

    string Process(UnifiedTranscodeRequest request);

    string ProcessWithProbeResult(UnifiedTranscodeRequest request, ProbeResult? probe);

    string ProcessWithProbeJson(UnifiedTranscodeRequest request, string? probeJson);
}
