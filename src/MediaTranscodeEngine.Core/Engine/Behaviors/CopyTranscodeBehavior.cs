namespace MediaTranscodeEngine.Core.Engine.Behaviors;

public sealed class CopyTranscodeBehavior : ITranscodeBehavior
{
    private readonly TranscodeEngine _engine;

    public CopyTranscodeBehavior(TranscodeEngine engine)
    {
        _engine = engine;
    }

    public bool CanHandle(TargetVideoCodec targetCodec, TranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return targetCodec is TargetVideoCodec.Copy;
    }

    public string Process(TranscodeRequest request)
    {
        return _engine.Process(request);
    }

    public string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe)
    {
        return _engine.ProcessWithProbeResult(request, probe);
    }

    public string ProcessWithProbeJson(TranscodeRequest request, string? probeJson)
    {
        return _engine.ProcessWithProbeJson(request, probeJson);
    }
}
