namespace MediaTranscodeEngine.Core.Engine;

public sealed record RawUnifiedTranscodeRequest(
    string InputPath,
    string TargetContainer = RequestContracts.Unified.DefaultContainer,
    string ComputeMode = RequestContracts.Unified.DefaultComputeMode,
    string VideoPreset = RequestContracts.Unified.DefaultVideoPreset,
    bool PreferH264 = false,
    bool Info = false,
    bool OverlayBg = false,
    int? Downscale = null,
    string DownscaleAlgo = RequestContracts.Unified.DefaultDownscaleAlgorithm,
    string ContentProfile = RequestContracts.Transcode.DefaultContentProfile,
    string QualityProfile = RequestContracts.Transcode.DefaultQualityProfile,
    bool NoAutoSample = false,
    string AutoSampleMode = RequestContracts.Transcode.DefaultAutoSampleMode,
    bool SyncAudio = false,
    int? Cq = null,
    double? Maxrate = null,
    double? Bufsize = null,
    bool ForceVideoEncode = false,
    bool KeepFps = false,
    bool UseAq = false,
    int AqStrength = RequestContracts.Unified.DefaultAqStrength,
    bool Denoise = false,
    bool FixTimestamps = false,
    bool KeepSource = false)
{
    public UnifiedTranscodeRequest ToDomain()
    {
        return UnifiedTranscodeRequest.Create(
            InputPath: InputPath,
            TargetContainer: TargetContainer,
            ComputeMode: ComputeMode,
            VideoPreset: VideoPreset,
            PreferH264: PreferH264,
            Info: Info,
            OverlayBg: OverlayBg,
            Downscale: Downscale,
            DownscaleAlgo: DownscaleAlgo,
            ContentProfile: ContentProfile,
            QualityProfile: QualityProfile,
            NoAutoSample: NoAutoSample,
            AutoSampleMode: AutoSampleMode,
            SyncAudio: SyncAudio,
            Cq: Cq,
            Maxrate: Maxrate,
            Bufsize: Bufsize,
            ForceVideoEncode: ForceVideoEncode,
            KeepFps: KeepFps,
            UseAq: UseAq,
            AqStrength: AqStrength,
            Denoise: Denoise,
            FixTimestamps: FixTimestamps,
            KeepSource: KeepSource);
    }
}
