namespace MediaTranscodeEngine.Core.Engine;

public sealed class H264TranscodeRequest
{
    private H264TranscodeRequest(
        string inputPath,
        int? downscale,
        bool keepFps,
        string downscaleAlgo,
        int? cq,
        string nvencPreset,
        bool useAq,
        int aqStrength,
        bool denoise,
        bool fixTimestamps,
        bool outputMkv)
    {
        InputPath = inputPath;
        Downscale = downscale;
        KeepFps = keepFps;
        DownscaleAlgo = downscaleAlgo;
        Cq = cq;
        NvencPreset = nvencPreset;
        UseAq = useAq;
        AqStrength = aqStrength;
        Denoise = denoise;
        FixTimestamps = fixTimestamps;
        OutputMkv = outputMkv;
    }

    public string InputPath { get; }
    public int? Downscale { get; }
    public bool KeepFps { get; }
    public string DownscaleAlgo { get; }
    public int? Cq { get; }
    public string NvencPreset { get; }
    public bool UseAq { get; }
    public int AqStrength { get; }
    public bool Denoise { get; }
    public bool FixTimestamps { get; }
    public bool OutputMkv { get; }

    public static H264TranscodeRequest Create(
        string InputPath,
        int? Downscale = null,
        bool KeepFps = false,
        string DownscaleAlgo = RequestContracts.H264.DefaultDownscaleAlgorithm,
        int? Cq = null,
        string NvencPreset = RequestContracts.H264.DefaultNvencPreset,
        bool UseAq = false,
        int AqStrength = RequestContracts.H264.DefaultAqStrength,
        bool Denoise = false,
        bool FixTimestamps = false,
        bool OutputMkv = false)
    {
        var normalizedInputPath = RequireValue(InputPath, nameof(InputPath), "InputPath is required.");
        var normalizedDownscaleAlgo = RequireAllowedValue(
            RequireValue(DownscaleAlgo, nameof(DownscaleAlgo), "DownscaleAlgo is required."),
            nameof(DownscaleAlgo),
            "DownscaleAlgo must be one of: bicubic, lanczos, bilinear.",
            RequestContracts.H264.DownscaleAlgorithms);
        var normalizedNvencPreset = RequireAllowedValue(
            RequireValue(NvencPreset, nameof(NvencPreset), "NvencPreset is required."),
            nameof(NvencPreset),
            "NvencPreset must be one of: p1, p2, p3, p4, p5, p6, p7.",
            RequestContracts.H264.NvencPresets);

        if (Downscale.HasValue && !RequestContracts.H264.DownscaleTargets.Contains(Downscale.Value))
        {
            throw new ArgumentException("Downscale must be 576 or 720.", nameof(Downscale));
        }

        if (Cq.HasValue && Cq.Value is < 0 or > 51)
        {
            throw new ArgumentException("Cq must be in range 0..51.", nameof(Cq));
        }

        if (AqStrength is < 1 or > 15)
        {
            throw new ArgumentException("AqStrength must be in range 1..15.", nameof(AqStrength));
        }

        return new H264TranscodeRequest(
            inputPath: normalizedInputPath,
            downscale: Downscale,
            keepFps: KeepFps,
            downscaleAlgo: normalizedDownscaleAlgo,
            cq: Cq,
            nvencPreset: normalizedNvencPreset,
            useAq: UseAq,
            aqStrength: AqStrength,
            denoise: Denoise,
            fixTimestamps: FixTimestamps,
            outputMkv: OutputMkv);
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
