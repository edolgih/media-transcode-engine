namespace MediaTranscodeEngine.Core.Engine.Behaviors;

public interface ITranscodeBehavior
{
    bool CanHandle(TargetVideoCodec targetCodec, TranscodeRequest request);

    string Process(TranscodeRequest request);

    string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe);

    string ProcessWithProbeJson(TranscodeRequest request, string? probeJson);
}
