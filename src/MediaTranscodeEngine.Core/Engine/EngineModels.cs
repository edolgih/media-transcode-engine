namespace MediaTranscodeEngine.Core.Engine;

public sealed record ProbeFormat(
    double? DurationSeconds = null,
    double? BitrateBps = null,
    string? FormatName = null);

public sealed record ProbeStream(
    string CodecType,
    string CodecName,
    int? Width = null,
    int? Height = null,
    double? BitrateBps = null,
    string? RFrameRate = null,
    string? AvgFrameRate = null);

public sealed record ProbeResult(
    ProbeFormat? Format,
    IReadOnlyList<ProbeStream> Streams);

public sealed record TranscodeRequest(
    string InputPath,
    bool Info = false,
    bool OverlayBg = false,
    int? Downscale = null,
    string? DownscaleAlgoOverride = null,
    string ContentProfile = "film",
    string QualityProfile = "default",
    bool NoAutoSample = false,
    string AutoSampleMode = "accurate",
    bool SyncAudio = false,
    int? CqOverride = null,
    double? MaxrateOverride = null,
    double? BufsizeOverride = null,
    string NvencPreset = "p6");
