namespace MediaTranscodeEngine.Core.Engine.Behaviors;

public sealed class H264GpuTranscodeBehavior : ITranscodeBehavior
{
    private readonly H264TranscodeEngine _engine;

    public H264GpuTranscodeBehavior(H264TranscodeEngine engine)
    {
        _engine = engine;
    }

    public bool CanHandle(TargetVideoCodec targetCodec, UnifiedTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return targetCodec is TargetVideoCodec.H264 &&
               request.ComputeMode.Equals(RequestContracts.Unified.GpuComputeMode, StringComparison.OrdinalIgnoreCase);
    }

    public string Process(UnifiedTranscodeRequest request)
    {
        return _engine.Process(Map(request));
    }

    public string ProcessWithProbeResult(UnifiedTranscodeRequest request, ProbeResult? probe)
    {
        return _engine.ProcessWithProbeResult(Map(request), probe);
    }

    public string ProcessWithProbeJson(UnifiedTranscodeRequest request, string? probeJson)
    {
        return _engine.ProcessWithProbeJson(Map(request), probeJson);
    }

    private static H264TranscodeRequest Map(UnifiedTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outputMkv = request.TargetContainer.Equals(RequestContracts.Unified.MkvContainer, StringComparison.OrdinalIgnoreCase);

        return H264TranscodeRequest.Create(
            InputPath: request.InputPath,
            Downscale: request.Downscale,
            KeepFps: request.KeepFps,
            DownscaleAlgo: request.DownscaleAlgo,
            Cq: request.Cq,
            NvencPreset: request.VideoPreset,
            UseAq: request.UseAq,
            AqStrength: request.AqStrength,
            Denoise: request.Denoise,
            FixTimestamps: request.FixTimestamps,
            OutputMkv: outputMkv,
            KeepSource: request.KeepSource);
    }
}
