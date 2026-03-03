namespace MediaTranscodeEngine.Core.Engine;

public sealed record RawTranscodeRequest(
    string InputPath,
    bool Info = false,
    bool OverlayBg = false,
    int? Downscale = null,
    string? DownscaleAlgoOverride = null,
    string ContentProfile = RequestContracts.Transcode.DefaultContentProfile,
    string QualityProfile = RequestContracts.Transcode.DefaultQualityProfile,
    bool NoAutoSample = false,
    string AutoSampleMode = RequestContracts.Transcode.DefaultAutoSampleMode,
    bool SyncAudio = false,
    int? Cq = null,
    double? Maxrate = null,
    double? Bufsize = null,
    string NvencPreset = RequestContracts.Transcode.DefaultNvencPreset,
    bool ForceVideoEncode = false)
{
    public TranscodeRequest ToDomain()
    {
        return TranscodeRequest.Create(
            InputPath: InputPath,
            Info: Info,
            OverlayBg: OverlayBg,
            Downscale: Downscale,
            DownscaleAlgoOverride: DownscaleAlgoOverride,
            ContentProfile: ContentProfile,
            QualityProfile: QualityProfile,
            NoAutoSample: NoAutoSample,
            AutoSampleMode: AutoSampleMode,
            SyncAudio: SyncAudio,
            Cq: Cq,
            Maxrate: Maxrate,
            Bufsize: Bufsize,
            NvencPreset: NvencPreset,
            ForceVideoEncode: ForceVideoEncode);
    }
}
