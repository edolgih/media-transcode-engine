using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToH264Gpu.Core;

/*
Это runtime-request для сценария toh264gpu.
Он хранит только scenario-specific domain-опции и не знает про raw CLI-аргументы.
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
        var normalizedNvencPreset = NormalizeName(nvencPreset);
        if (normalizedNvencPreset is not null && !NvencPresetOptions.IsSupportedPreset(normalizedNvencPreset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nvencPreset),
                nvencPreset,
                $"Supported values: {GetSupportedPresetsDisplay()}.");
        }

        if (videoSettings?.Cq is > 51)
        {
            throw new ArgumentOutOfRangeException("cq", videoSettings.Cq.Value, "CQ must be between 1 and 51.");
        }

        KeepSource = keepSource;
        Downscale = downscale?.WithDefaultAlgorithm(FfmpegScaleAlgorithms.Bicubic);
        KeepFramesPerSecond = keepFramesPerSecond;
        VideoSettings = videoSettings;
        NvencPreset = normalizedNvencPreset ?? NvencPresetOptions.DefaultPreset;
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
    /// Gets the normalized NVENC preset used by the scenario.
    /// </summary>
    public string NvencPreset { get; }

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

    private static string GetSupportedPresetsDisplay()
    {
        return string.Join(", ", NvencPresetOptions.SupportedPresets);
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
