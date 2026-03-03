namespace MediaTranscodeEngine.Core.Engine;

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
    int? Cq = null,
    double? Maxrate = null,
    double? Bufsize = null,
    string NvencPreset = "p6",
    bool ForceVideoEncode = false)
{
    public TranscodeRequest EnsureValid()
    {
        var inputPath = RequireValue(InputPath, nameof(InputPath), "InputPath is required.");
        var contentProfile = RequireAllowedValue(
            RequireValue(ContentProfile, nameof(ContentProfile), "ContentProfile is required."),
            nameof(ContentProfile),
            "ContentProfile must be one of: anime, mult, film.",
            "anime",
            "mult",
            "film");
        var qualityProfile = RequireAllowedValue(
            RequireValue(QualityProfile, nameof(QualityProfile), "QualityProfile is required."),
            nameof(QualityProfile),
            "QualityProfile must be one of: high, default, low.",
            "high",
            "default",
            "low");
        var autoSampleMode = RequireAllowedValue(
            RequireValue(AutoSampleMode, nameof(AutoSampleMode), "AutoSampleMode is required."),
            nameof(AutoSampleMode),
            "AutoSampleMode must be one of: accurate, fast, hybrid.",
            "accurate",
            "fast",
            "hybrid");
        var nvencPreset = RequireAllowedValue(
            RequireValue(NvencPreset, nameof(NvencPreset), "NvencPreset is required."),
            nameof(NvencPreset),
            "NvencPreset must be one of: p1, p2, p3, p4, p5, p6, p7.",
            "p1",
            "p2",
            "p3",
            "p4",
            "p5",
            "p6",
            "p7");
        var downscaleAlgoOverride = NormalizeOptional(DownscaleAlgoOverride);
        if (downscaleAlgoOverride is not null)
        {
            downscaleAlgoOverride = RequireAllowedValue(
                downscaleAlgoOverride,
                nameof(DownscaleAlgoOverride),
                "DownscaleAlgoOverride must be one of: bicubic, lanczos, bilinear.",
                "bicubic",
                "lanczos",
                "bilinear");
        }

        if (Downscale.HasValue && Downscale.Value is not (576 or 720))
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

        return this with
        {
            InputPath = inputPath,
            ContentProfile = contentProfile,
            QualityProfile = qualityProfile,
            AutoSampleMode = autoSampleMode,
            NvencPreset = nvencPreset,
            DownscaleAlgoOverride = downscaleAlgoOverride
        };
    }

    private static string RequireValue(string value, string paramName, string message)
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
        params string[] allowedValues)
    {
        if (!allowedValues.Any(option => option.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(message, paramName);
        }

        return value;
    }
}
