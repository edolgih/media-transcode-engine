using MediaTranscodeEngine.Runtime.VideoSettings;

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
        bool keepSource = false,
        DownscaleRequest? downscale = null,
        bool keepFramesPerSecond = false,
        VideoSettingsRequest? videoSettings = null,
        string? nvencPreset = null,
        bool denoise = false,
        bool synchronizeAudio = false,
        bool outputMkv = false)
    {
        if (downscale is not null &&
            downscale.TargetHeight is not (720 or 576 or 480 or 424))
        {
            throw new ArgumentOutOfRangeException(nameof(downscale), downscale.TargetHeight, "Supported values: 720, 576, 480, 424.");
        }

        KeepSource = keepSource;
        Downscale = downscale;
        KeepFramesPerSecond = keepFramesPerSecond;
        VideoSettings = videoSettings?.HasValue == true ? videoSettings : null;
        NvencPreset = NormalizeName(nvencPreset);
        Denoise = denoise;
        SynchronizeAudio = synchronizeAudio;
        OutputMkv = outputMkv;
    }

    /// <summary>
    /// Gets a value indicating whether the source file should be preserved after execution.
    /// </summary>
    public bool KeepSource { get; }

    /// <summary>
    /// Gets explicit downscale intent when the scenario requests resized output.
    /// </summary>
    public DownscaleRequest? Downscale { get; }

    /// <summary>
    /// Gets a value indicating whether downscale mode should preserve the source FPS instead of capping it.
    /// </summary>
    public bool KeepFramesPerSecond { get; }

    /// <summary>
    /// Gets reusable video-settings directives when the scenario requests them.
    /// </summary>
    public VideoSettingsRequest? VideoSettings { get; }

    /// <summary>
    /// Gets the explicit NVENC preset override.
    /// </summary>
    public string? NvencPreset { get; }

    /// <summary>
    /// Gets a value indicating whether denoise should be enabled when normal encoding is used.
    /// </summary>
    public bool Denoise { get; }

    /// <summary>
    /// Gets a value indicating whether the sync-safe repair path was explicitly requested.
    /// </summary>
    public bool SynchronizeAudio { get; }

    /// <summary>
    /// Gets a value indicating whether the target container should be MKV instead of MP4.
    /// </summary>
    public bool OutputMkv { get; }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
