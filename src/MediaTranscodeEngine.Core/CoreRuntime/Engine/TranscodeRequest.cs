namespace MediaTranscodeEngine.Core.Engine;

public sealed class TranscodeRequest
{
    private TranscodeRequest(
        string inputPath,
        string targetContainer,
        string encoderBackend,
        string videoPreset,
        string targetVideoCodec,
        bool preferH264,
        bool info,
        bool overlayBg,
        int? downscale,
        string downscaleAlgo,
        bool downscaleAlgoOverridden,
        string contentProfile,
        string qualityProfile,
        bool noAutoSample,
        string autoSampleMode,
        bool syncAudio,
        int? cq,
        double? maxrate,
        double? bufsize,
        bool forceVideoEncode,
        bool keepFps,
        bool useAq,
        int aqStrength,
        bool denoise,
        bool fixTimestamps,
        bool keepSource)
    {
        InputPath = inputPath;
        TargetContainer = targetContainer;
        EncoderBackend = encoderBackend;
        VideoPreset = videoPreset;
        TargetVideoCodec = targetVideoCodec;
        PreferH264 = preferH264;
        Info = info;
        OverlayBg = overlayBg;
        Downscale = downscale;
        DownscaleAlgo = downscaleAlgo;
        DownscaleAlgoOverridden = downscaleAlgoOverridden;
        ContentProfile = contentProfile;
        QualityProfile = qualityProfile;
        NoAutoSample = noAutoSample;
        AutoSampleMode = autoSampleMode;
        SyncAudio = syncAudio;
        Cq = cq;
        Maxrate = maxrate;
        Bufsize = bufsize;
        ForceVideoEncode = forceVideoEncode;
        KeepFps = keepFps;
        UseAq = useAq;
        AqStrength = aqStrength;
        Denoise = denoise;
        FixTimestamps = fixTimestamps;
        KeepSource = keepSource;
    }

    public string InputPath { get; }
    public string TargetContainer { get; }
    public string EncoderBackend { get; }
    public string VideoPreset { get; }
    public string TargetVideoCodec { get; }
    public bool PreferH264 { get; }
    public bool Info { get; }
    public bool OverlayBg { get; }
    public int? Downscale { get; }
    public string DownscaleAlgo { get; }
    public bool DownscaleAlgoOverridden { get; }
    public string ContentProfile { get; }
    public string QualityProfile { get; }
    public bool NoAutoSample { get; }
    public string AutoSampleMode { get; }
    public bool SyncAudio { get; }
    public int? Cq { get; }
    public double? Maxrate { get; }
    public double? Bufsize { get; }
    public bool ForceVideoEncode { get; }
    public bool KeepFps { get; }
    public bool UseAq { get; }
    public int AqStrength { get; }
    public bool Denoise { get; }
    public bool FixTimestamps { get; }
    public bool KeepSource { get; }

    public static TranscodeRequest Create(
        string InputPath,
        string TargetContainer = RequestContracts.General.DefaultContainer,
        string EncoderBackend = RequestContracts.General.DefaultEncoderBackend,
        string VideoPreset = RequestContracts.General.DefaultVideoPreset,
        string? TargetVideoCodec = null,
        bool PreferH264 = false,
        bool Info = false,
        bool OverlayBg = false,
        int? Downscale = null,
        string? DownscaleAlgo = null,
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
        int AqStrength = RequestContracts.General.DefaultAqStrength,
        bool Denoise = false,
        bool FixTimestamps = false,
        bool KeepSource = false)
    {
        var normalizedInputPath = RequireValue(InputPath, nameof(InputPath), "InputPath is required.");
        var normalizedTargetContainer = RequireAllowedValue(
            RequireValue(TargetContainer, nameof(TargetContainer), "TargetContainer is required."),
            nameof(TargetContainer),
            "TargetContainer must be one of: mkv, mp4.",
            RequestContracts.General.Containers);
        var normalizedEncoderBackend = RequireAllowedValue(
            RequireValue(EncoderBackend, nameof(EncoderBackend), "EncoderBackend is required."),
            nameof(EncoderBackend),
            "EncoderBackend must be one of: gpu, cpu.",
            RequestContracts.General.EncoderBackends);
        var normalizedVideoPreset = RequireAllowedValue(
            RequireValue(VideoPreset, nameof(VideoPreset), "VideoPreset is required."),
            nameof(VideoPreset),
            "VideoPreset must be one of: p1, p2, p3, p4, p5, p6, p7.",
            RequestContracts.General.VideoPresets);
        var normalizedTargetVideoCodec = ResolveTargetVideoCodec(
            targetVideoCodec: TargetVideoCodec,
            targetContainer: normalizedTargetContainer,
            preferH264: PreferH264);
        var downscaleAlgoOverridden = !string.IsNullOrWhiteSpace(DownscaleAlgo);
        var normalizedDownscaleAlgo = downscaleAlgoOverridden
            ? RequireAllowedValue(
                RequireValue(DownscaleAlgo, nameof(DownscaleAlgo), "DownscaleAlgo is required."),
                nameof(DownscaleAlgo),
                "DownscaleAlgo must be one of: bicubic, lanczos, bilinear.",
                RequestContracts.General.DownscaleAlgorithms)
            : RequestContracts.General.DefaultDownscaleAlgorithm;
        var normalizedContentProfile = RequireAllowedValue(
            RequireValue(ContentProfile, nameof(ContentProfile), "ContentProfile is required."),
            nameof(ContentProfile),
            "ContentProfile must be one of: anime, mult, film.",
            RequestContracts.Transcode.ContentProfiles);
        var normalizedQualityProfile = RequireAllowedValue(
            RequireValue(QualityProfile, nameof(QualityProfile), "QualityProfile is required."),
            nameof(QualityProfile),
            "QualityProfile must be one of: high, default, low.",
            RequestContracts.Transcode.QualityProfiles);
        var normalizedAutoSampleMode = RequireAllowedValue(
            RequireValue(AutoSampleMode, nameof(AutoSampleMode), "AutoSampleMode is required."),
            nameof(AutoSampleMode),
            "AutoSampleMode must be one of: accurate, fast, hybrid.",
            RequestContracts.Transcode.AutoSampleModes);

        if (Downscale.HasValue && Downscale.Value <= 0)
        {
            throw new ArgumentException("Downscale must be greater than zero.", nameof(Downscale));
        }

        if (Cq.HasValue && Cq.Value is < 0 or > 51)
        {
            throw new ArgumentException("Cq must be in range 0..51.", nameof(Cq));
        }

        if (Maxrate.HasValue && Maxrate.Value <= 0)
        {
            throw new ArgumentException("Maxrate must be greater than zero.", nameof(Maxrate));
        }

        if (Bufsize.HasValue && Bufsize.Value <= 0)
        {
            throw new ArgumentException("Bufsize must be greater than zero.", nameof(Bufsize));
        }

        if (AqStrength is < 1 or > 15)
        {
            throw new ArgumentException("AqStrength must be in range 1..15.", nameof(AqStrength));
        }

        var preferH264Effective = PreferH264 ||
                                  normalizedTargetVideoCodec.Equals(
                                      RequestContracts.General.H264VideoCodec,
                                      StringComparison.OrdinalIgnoreCase);

        return new TranscodeRequest(
            inputPath: normalizedInputPath,
            targetContainer: normalizedTargetContainer,
            encoderBackend: normalizedEncoderBackend,
            videoPreset: normalizedVideoPreset,
            targetVideoCodec: normalizedTargetVideoCodec,
            preferH264: preferH264Effective,
            info: Info,
            overlayBg: OverlayBg,
            downscale: Downscale,
            downscaleAlgo: normalizedDownscaleAlgo,
            downscaleAlgoOverridden: downscaleAlgoOverridden,
            contentProfile: normalizedContentProfile,
            qualityProfile: normalizedQualityProfile,
            noAutoSample: NoAutoSample,
            autoSampleMode: normalizedAutoSampleMode,
            syncAudio: SyncAudio,
            cq: Cq,
            maxrate: Maxrate,
            bufsize: Bufsize,
            forceVideoEncode: ForceVideoEncode,
            keepFps: KeepFps,
            useAq: UseAq,
            aqStrength: AqStrength,
            denoise: Denoise,
            fixTimestamps: FixTimestamps,
            keepSource: KeepSource);
    }

    private static string ResolveTargetVideoCodec(
        string? targetVideoCodec,
        string targetContainer,
        bool preferH264)
    {
        if (!string.IsNullOrWhiteSpace(targetVideoCodec))
        {
            var normalizedTargetVideoCodec = RequireAllowedValue(
                RequireValue(targetVideoCodec, nameof(TargetVideoCodec), "TargetVideoCodec is required."),
                nameof(TargetVideoCodec),
                "TargetVideoCodec must be one of: copy, h264, h265.",
                RequestContracts.General.TargetVideoCodecs);
            if (!preferH264 ||
                !normalizedTargetVideoCodec.Equals(RequestContracts.General.CopyVideoCodec, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedTargetVideoCodec;
            }
        }

        if (preferH264 || !targetContainer.Equals(RequestContracts.General.MkvContainer, StringComparison.OrdinalIgnoreCase))
        {
            return RequestContracts.General.H264VideoCodec;
        }

        return RequestContracts.General.DefaultTargetVideoCodec;
    }

    private static string RequireValue(string? value, string paramName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, paramName);
        }

        return value.Trim();
    }

    private static string RequireAllowedValue(
        string value,
        string paramName,
        string message,
        IReadOnlyCollection<string> allowedValues)
    {
        if (!allowedValues.Any(option => option.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(message, paramName);
        }

        return value;
    }
}
