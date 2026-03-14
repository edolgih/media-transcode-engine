using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/*
Это локальный execution spec сценария tomkvgpu.
Он несет уже resolved video-settings execution payload и связанную autosample-диагностику,
чтобы tool не принимал domain-решения повторно.
*/
/// <summary>
/// Carries tomkvgpu-specific resolved execution details outside the shared transcode plan.
/// </summary>
internal sealed class ToMkvGpuExecutionSpec : TranscodeExecutionSpec
{
    public ToMkvGpuExecutionSpec(
        ProfileDrivenVideoSettingsResolution videoResolution,
        ToMkvGpuResolvedSourceBitrate sourceBitrate)
    {
        VideoResolution = videoResolution ?? throw new ArgumentNullException(nameof(videoResolution));
        SourceBitrate = sourceBitrate ?? throw new ArgumentNullException(nameof(sourceBitrate));
    }

    public ProfileDrivenVideoSettingsResolution VideoResolution { get; }

    public ToMkvGpuResolvedSourceBitrate SourceBitrate { get; }
}

internal sealed record ToMkvGpuResolvedSourceBitrate(long? Bitrate, string Origin);
