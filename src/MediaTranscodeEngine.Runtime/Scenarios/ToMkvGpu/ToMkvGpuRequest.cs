namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/// <summary>
/// Captures scenario-specific directives for the legacy ToMkvGpu workflow.
/// </summary>
public sealed class ToMkvGpuRequest
{
    /// <summary>
    /// Initializes scenario-specific directives for the ToMkvGpu workflow.
    /// </summary>
    /// <param name="overlayBackground">Whether background overlay should be applied during encoding.</param>
    /// <param name="downscaleTarget">Requested downscale target height.</param>
    /// <param name="synchronizeAudio">Whether the audio sync-safe path should be forced.</param>
    /// <param name="keepSource">Whether the source file should be preserved after execution.</param>
    /// <param name="contentProfile">Requested 576 content profile.</param>
    /// <param name="qualityProfile">Requested 576 quality profile.</param>
    /// <param name="noAutoSample">Whether 576 autosample should be disabled.</param>
    /// <param name="autoSampleMode">Requested 576 autosample mode.</param>
    /// <param name="downscaleAlgorithm">Explicit downscale algorithm override.</param>
    /// <param name="cq">Explicit NVENC CQ override.</param>
    /// <param name="maxrate">Explicit VBV maxrate override in Mbit/s.</param>
    /// <param name="bufsize">Explicit VBV bufsize override in Mbit/s.</param>
    /// <param name="nvencPreset">Explicit NVENC preset override.</param>
    public ToMkvGpuRequest(
        bool overlayBackground = false,
        int? downscaleTarget = null,
        bool synchronizeAudio = false,
        bool keepSource = false,
        string? contentProfile = null,
        string? qualityProfile = null,
        bool noAutoSample = false,
        string? autoSampleMode = null,
        string? downscaleAlgorithm = null,
        int? cq = null,
        decimal? maxrate = null,
        decimal? bufsize = null,
        string? nvencPreset = null)
    {
        if (downscaleTarget.HasValue && downscaleTarget.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(downscaleTarget), downscaleTarget.Value, "Downscale target must be greater than zero.");
        }

        if (cq.HasValue && cq.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cq), cq.Value, "CQ must be greater than zero.");
        }

        if (maxrate.HasValue && maxrate.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(maxrate), maxrate.Value, "Maxrate must be greater than zero.");
        }

        if (bufsize.HasValue && bufsize.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(bufsize), bufsize.Value, "Bufsize must be greater than zero.");
        }

        OverlayBackground = overlayBackground;
        DownscaleTarget = downscaleTarget;
        SynchronizeAudio = synchronizeAudio;
        KeepSource = keepSource;
        ContentProfile = NormalizeName(contentProfile);
        QualityProfile = NormalizeName(qualityProfile);
        NoAutoSample = noAutoSample;
        AutoSampleMode = NormalizeName(autoSampleMode);
        DownscaleAlgorithm = NormalizeName(downscaleAlgorithm);
        Cq = cq;
        Maxrate = maxrate;
        Bufsize = bufsize;
        NvencPreset = NormalizeName(nvencPreset);
    }

    /// <summary>
    /// Gets a value indicating whether background overlay should be applied during encoding.
    /// </summary>
    public bool OverlayBackground { get; }

    /// <summary>
    /// Gets the requested downscale target height.
    /// </summary>
    public int? DownscaleTarget { get; }

    /// <summary>
    /// Gets a value indicating whether the audio sync-safe path should be forced.
    /// </summary>
    public bool SynchronizeAudio { get; }

    /// <summary>
    /// Gets a value indicating whether the source file should be preserved after execution.
    /// </summary>
    public bool KeepSource { get; }

    /// <summary>
    /// Gets the requested 576 content profile.
    /// </summary>
    public string? ContentProfile { get; }

    /// <summary>
    /// Gets the requested 576 quality profile.
    /// </summary>
    public string? QualityProfile { get; }

    /// <summary>
    /// Gets a value indicating whether 576 autosample should be disabled.
    /// </summary>
    public bool NoAutoSample { get; }

    /// <summary>
    /// Gets the requested 576 autosample mode.
    /// </summary>
    public string? AutoSampleMode { get; }

    /// <summary>
    /// Gets the explicit downscale algorithm override.
    /// </summary>
    public string? DownscaleAlgorithm { get; }

    /// <summary>
    /// Gets the explicit NVENC CQ override.
    /// </summary>
    public int? Cq { get; }

    /// <summary>
    /// Gets the explicit VBV maxrate override in Mbit/s.
    /// </summary>
    public decimal? Maxrate { get; }

    /// <summary>
    /// Gets the explicit VBV bufsize override in Mbit/s.
    /// </summary>
    public decimal? Bufsize { get; }

    /// <summary>
    /// Gets the explicit NVENC preset override.
    /// </summary>
    public string? NvencPreset { get; }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
