using MediaTranscodeEngine.Runtime.Downscaling;

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
    /// <param name="synchronizeAudio">Whether the audio sync-safe path should be forced.</param>
    /// <param name="keepSource">Whether the source file should be preserved after execution.</param>
    /// <param name="downscale">Reusable downscale directives.</param>
    /// <param name="nvencPreset">Explicit NVENC preset override.</param>
    public ToMkvGpuRequest(
        bool overlayBackground = false,
        bool synchronizeAudio = false,
        bool keepSource = false,
        DownscaleRequest? downscale = null,
        string? nvencPreset = null)
    {
        OverlayBackground = overlayBackground;
        SynchronizeAudio = synchronizeAudio;
        KeepSource = keepSource;
        Downscale = downscale?.HasValue == true ? downscale : null;
        NvencPreset = NormalizeName(nvencPreset);
    }

    /// <summary>
    /// Gets a value indicating whether background overlay should be applied during encoding.
    /// </summary>
    public bool OverlayBackground { get; }

    /// <summary>
    /// Gets reusable downscale directives when the scenario requests them.
    /// </summary>
    public DownscaleRequest? Downscale { get; }

    /// <summary>
    /// Gets a value indicating whether the audio sync-safe path should be forced.
    /// </summary>
    public bool SynchronizeAudio { get; }

    /// <summary>
    /// Gets a value indicating whether the source file should be preserved after execution.
    /// </summary>
    public bool KeepSource { get; }

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
