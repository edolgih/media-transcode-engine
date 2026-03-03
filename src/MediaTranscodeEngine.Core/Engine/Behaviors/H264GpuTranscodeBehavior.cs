namespace MediaTranscodeEngine.Core.Engine.Behaviors;

public sealed class H264GpuTranscodeBehavior : ITranscodeBehavior
{
    private readonly H264TranscodeEngine _engine;

    public H264GpuTranscodeBehavior(H264TranscodeEngine engine)
    {
        _engine = engine;
    }

    public bool CanHandle(TargetVideoCodec targetCodec, TranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return targetCodec is TargetVideoCodec.H264 &&
               request.ComputeMode.Equals(RequestContracts.General.GpuComputeMode, StringComparison.OrdinalIgnoreCase);
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
