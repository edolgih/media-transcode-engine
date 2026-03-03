namespace MediaTranscodeEngine.Core.Engine.Behaviors;

public sealed class CopyTranscodeBehavior : ITranscodeBehavior
{
    private readonly TranscodeEngine _engine;

    public CopyTranscodeBehavior(TranscodeEngine engine)
    {
        _engine = engine;
    }

    public bool CanHandle(TargetVideoCodec targetCodec, UnifiedTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return targetCodec is TargetVideoCodec.Copy;
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

    private static TranscodeRequest Map(UnifiedTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TranscodeRequest.Create(
            InputPath: request.InputPath,
            Info: request.Info,
            OverlayBg: request.OverlayBg,
            Downscale: request.Downscale,
            DownscaleAlgoOverride: request.DownscaleAlgo,
            ContentProfile: request.ContentProfile,
            QualityProfile: request.QualityProfile,
            NoAutoSample: request.NoAutoSample,
            AutoSampleMode: request.AutoSampleMode,
            SyncAudio: request.SyncAudio,
            Cq: request.Cq,
            Maxrate: request.Maxrate,
            Bufsize: request.Bufsize,
            NvencPreset: request.VideoPreset,
            ForceVideoEncode: request.ForceVideoEncode,
            KeepSource: request.KeepSource);
    }
}
