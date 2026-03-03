namespace MediaTranscodeEngine.Core.Engine;

public sealed class TranscodeRequest
{
    private TranscodeRequest(
        string inputPath,
        bool info,
        bool overlayBg,
        int? downscale,
        string? downscaleAlgoOverride,
        string contentProfile,
        string qualityProfile,
        bool noAutoSample,
        string autoSampleMode,
        bool syncAudio,
        int? cq,
        double? maxrate,
        double? bufsize,
        string nvencPreset,
        bool forceVideoEncode)
    {
        InputPath = inputPath;
        Info = info;
        OverlayBg = overlayBg;
        Downscale = downscale;
        DownscaleAlgoOverride = downscaleAlgoOverride;
        ContentProfile = contentProfile;
        QualityProfile = qualityProfile;
        NoAutoSample = noAutoSample;
        AutoSampleMode = autoSampleMode;
        SyncAudio = syncAudio;
        Cq = cq;
        Maxrate = maxrate;
        Bufsize = bufsize;
        NvencPreset = nvencPreset;
        ForceVideoEncode = forceVideoEncode;
    }

    public string InputPath { get; }
    public bool Info { get; }
    public bool OverlayBg { get; }
    public int? Downscale { get; }
    public string? DownscaleAlgoOverride { get; }
    public string ContentProfile { get; }
    public string QualityProfile { get; }
    public bool NoAutoSample { get; }
    public string AutoSampleMode { get; }
    public bool SyncAudio { get; }
    public int? Cq { get; }
    public double? Maxrate { get; }
    public double? Bufsize { get; }
    public string NvencPreset { get; }
    public bool ForceVideoEncode { get; }

    public static TranscodeRequest Create(
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
        var normalizedInputPath = RequireValue(InputPath, nameof(InputPath), "InputPath is required.");
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
        var normalizedNvencPreset = RequireAllowedValue(
            RequireValue(NvencPreset, nameof(NvencPreset), "NvencPreset is required."),
            nameof(NvencPreset),
            "NvencPreset must be one of: p1, p2, p3, p4, p5, p6, p7.",
            RequestContracts.Transcode.NvencPresets);
        var normalizedDownscaleAlgoOverride = NormalizeOptional(DownscaleAlgoOverride);
        if (normalizedDownscaleAlgoOverride is not null)
        {
            normalizedDownscaleAlgoOverride = RequireAllowedValue(
                normalizedDownscaleAlgoOverride,
                nameof(DownscaleAlgoOverride),
                "DownscaleAlgoOverride must be one of: bicubic, lanczos, bilinear.",
                RequestContracts.Transcode.DownscaleAlgorithms);
        }

        if (Downscale.HasValue && !RequestContracts.Transcode.DownscaleTargets.Contains(Downscale.Value))
        {
            throw new ArgumentException("Downscale must be 576 or 720.", nameof(Downscale));
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

        return new TranscodeRequest(
            inputPath: normalizedInputPath,
            info: Info,
            overlayBg: OverlayBg,
            downscale: Downscale,
            downscaleAlgoOverride: normalizedDownscaleAlgoOverride,
            contentProfile: normalizedContentProfile,
            qualityProfile: normalizedQualityProfile,
            noAutoSample: NoAutoSample,
            autoSampleMode: normalizedAutoSampleMode,
            syncAudio: SyncAudio,
            cq: Cq,
            maxrate: Maxrate,
            bufsize: Bufsize,
            nvencPreset: normalizedNvencPreset,
            forceVideoEncode: ForceVideoEncode);
    }

    private static string RequireValue(string? value, string paramName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, paramName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
