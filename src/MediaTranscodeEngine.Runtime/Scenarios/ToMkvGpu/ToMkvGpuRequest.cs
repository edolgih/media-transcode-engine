using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/*
Это request-модель сценария tomkvgpu.
Она хранит только пользовательские указания, специфичные для этого сценария.
*/
/// <summary>
/// Captures scenario-specific directives for the legacy ToMkvGpu workflow.
/// </summary>
public sealed class ToMkvGpuRequest
{
    /// <summary>
    /// Supported frame-rate cap values exposed by the ToMkvGpu workflow.
    /// </summary>
    public const string SupportedMaxFramesPerSecondDisplay = "50, 40, 30, 24";

    /// <summary>
    /// Initializes scenario-specific directives for the ToMkvGpu workflow.
    /// </summary>
    /// <param name="overlayBackground">Whether background overlay should be applied during encoding.</param>
    /// <param name="synchronizeAudio">Whether the audio sync-safe path should be forced.</param>
    /// <param name="keepSource">Whether the source file should be preserved after execution.</param>
    /// <param name="videoSettings">Reusable video-settings directives.</param>
    /// <param name="nvencPreset">Explicit NVENC preset override.</param>
    /// <param name="maxFramesPerSecond">Optional frame-rate cap applied only when the source frame rate is higher.</param>
    public ToMkvGpuRequest(
        bool overlayBackground = false,
        bool synchronizeAudio = false,
        bool keepSource = false,
        VideoSettingsRequest? videoSettings = null,
        string? nvencPreset = null,
        int? maxFramesPerSecond = null)
    {
        if (maxFramesPerSecond.HasValue && !IsSupportedMaxFramesPerSecond(maxFramesPerSecond.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFramesPerSecond),
                maxFramesPerSecond.Value,
                $"Supported values: {SupportedMaxFramesPerSecondDisplay}.");
        }

        OverlayBackground = overlayBackground;
        SynchronizeAudio = synchronizeAudio;
        KeepSource = keepSource;
        VideoSettings = videoSettings?.HasValue == true ? videoSettings : null;
        NvencPreset = NormalizeName(nvencPreset);
        MaxFramesPerSecond = maxFramesPerSecond;
    }

    /// <summary>
    /// Gets a value indicating whether background overlay should be applied during encoding.
    /// </summary>
    public bool OverlayBackground { get; }

    /// <summary>
    /// Gets reusable video-settings directives when the scenario requests them.
    /// </summary>
    public VideoSettingsRequest? VideoSettings { get; }

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

    /// <summary>
    /// Gets the optional frame-rate cap applied only when the source exceeds it.
    /// </summary>
    public int? MaxFramesPerSecond { get; }

    /// <summary>
    /// Determines whether the supplied frame-rate cap is supported by the ToMkvGpu workflow.
    /// </summary>
    public static bool IsSupportedMaxFramesPerSecond(int value)
    {
        return value is 50 or 40 or 30 or 24;
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
