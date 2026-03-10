using MediaTranscodeEngine.Runtime.Downscaling;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;

/*
Это runtime-request для сценария toh264gpu.
Он хранит только scenario-specific опции, а вычисление итогового поведения остается внутри сценария.
*/
/// <summary>
/// Captures scenario-specific directives for the legacy ToH264Gpu workflow.
/// </summary>
public sealed class ToH264GpuRequest
{
    /// <summary>
    /// Initializes scenario-specific directives for the ToH264Gpu workflow.
    /// </summary>
    public ToH264GpuRequest(
        int? downscaleTargetHeight = null,
        bool keepFramesPerSecond = false,
        string? downscaleAlgorithm = null,
        int? cq = null,
        string? nvencPreset = null,
        bool useAdaptiveQuantization = false,
        int aqStrength = 4,
        bool denoise = false,
        bool fixTimestamps = false,
        bool outputMkv = false)
    {
        if (downscaleTargetHeight.HasValue &&
            downscaleTargetHeight.Value is not (720 or 576))
        {
            throw new ArgumentOutOfRangeException(nameof(downscaleTargetHeight), downscaleTargetHeight.Value, "Supported values: 720, 576.");
        }

        if (cq.HasValue && (cq.Value <= 0 || cq.Value > 51))
        {
            throw new ArgumentOutOfRangeException(nameof(cq), cq.Value, "CQ must be between 1 and 51.");
        }

        if (aqStrength < 0 || aqStrength > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(aqStrength), aqStrength, "AQ strength must be between 0 and 15.");
        }

        DownscaleTargetHeight = downscaleTargetHeight;
        KeepFramesPerSecond = keepFramesPerSecond;
        DownscaleAlgorithm = NormalizeName(downscaleAlgorithm) ?? "bicubic";
        Cq = cq;
        NvencPreset = NormalizeName(nvencPreset);
        UseAdaptiveQuantization = useAdaptiveQuantization;
        AqStrength = aqStrength;
        Denoise = denoise;
        FixTimestamps = fixTimestamps;
        OutputMkv = outputMkv;
    }

    /// <summary>
    /// Gets the optional downscale target height.
    /// </summary>
    public int? DownscaleTargetHeight { get; }

    /// <summary>
    /// Gets a value indicating whether downscale mode should preserve the source FPS instead of capping it.
    /// </summary>
    public bool KeepFramesPerSecond { get; }

    /// <summary>
    /// Gets the downscale interpolation algorithm.
    /// </summary>
    public string DownscaleAlgorithm { get; }

    /// <summary>
    /// Gets the explicit CQ override.
    /// </summary>
    public int? Cq { get; }

    /// <summary>
    /// Gets the explicit NVENC preset override.
    /// </summary>
    public string? NvencPreset { get; }

    /// <summary>
    /// Gets a value indicating whether AQ/lookahead should be enabled.
    /// </summary>
    public bool UseAdaptiveQuantization { get; }

    /// <summary>
    /// Gets the AQ strength value.
    /// </summary>
    public int AqStrength { get; }

    /// <summary>
    /// Gets a value indicating whether denoise should be enabled when normal encoding is used.
    /// </summary>
    public bool Denoise { get; }

    /// <summary>
    /// Gets a value indicating whether timestamp repair was explicitly requested.
    /// </summary>
    public bool FixTimestamps { get; }

    /// <summary>
    /// Gets a value indicating whether the target container should be MKV instead of MP4.
    /// </summary>
    public bool OutputMkv { get; }

    internal DownscaleRequest? BuildDownscaleRequest()
    {
        if (!DownscaleTargetHeight.HasValue)
        {
            return null;
        }

        return new DownscaleRequest(
            targetHeight: DownscaleTargetHeight.Value,
            algorithm: DownscaleAlgorithm);
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
