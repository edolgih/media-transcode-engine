using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Scenarios;

public sealed record ScenarioPreset(
    string Name,
    string? TargetContainer = null,
    string? EncoderBackend = null,
    string? VideoPreset = null,
    string? TargetVideoCodec = null,
    bool? PreferH264 = null,
    bool? OverlayBg = null,
    int? Downscale = null,
    string? DownscaleAlgo = null,
    string? ContentProfile = null,
    string? QualityProfile = null,
    bool? NoAutoSample = null,
    string? AutoSampleMode = null,
    bool? SyncAudio = null,
    int? Cq = null,
    double? Maxrate = null,
    double? Bufsize = null,
    bool? ForceVideoEncode = null,
    bool? KeepFps = null,
    bool? UseAq = null,
    int? AqStrength = null,
    bool? Denoise = null,
    bool? FixTimestamps = null,
    bool? KeepSource = null)
{
    public static ScenarioPreset CreateToMkvGpu()
    {
        return new ScenarioPreset(
            Name: "tomkvgpu",
            TargetContainer: RequestContracts.General.MkvContainer,
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            VideoPreset: RequestContracts.General.DefaultVideoPreset,
            TargetVideoCodec: RequestContracts.General.CopyVideoCodec,
            PreferH264: false,
            OverlayBg: false,
            ContentProfile: RequestContracts.Transcode.DefaultContentProfile,
            QualityProfile: RequestContracts.Transcode.DefaultQualityProfile,
            NoAutoSample: false,
            AutoSampleMode: RequestContracts.Transcode.DefaultAutoSampleMode,
            SyncAudio: false,
            ForceVideoEncode: false,
            KeepFps: false,
            UseAq: false,
            AqStrength: RequestContracts.General.DefaultAqStrength,
            Denoise: false,
            FixTimestamps: false,
            KeepSource: false);
    }
}
