using Transcode.Core.MediaIntent;
using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToMkvGpu.Core;

/*
Это локальная rich model сценария tomkvgpu.
Она несет уже принятые scenario-local решения и resolved video-settings payload,
чтобы ffmpeg-rendering не ходил обратно в shared промежуточные слои.
*/
/// <summary>
/// Carries the resolved tomkvgpu decision together with video-settings execution details.
/// </summary>
internal sealed class ToMkvGpuDecision
{
    public ToMkvGpuDecision(
        string targetContainer,
        VideoIntent video,
        AudioIntent audio,
        bool keepSource,
        string outputPath,
        bool applyOverlayBackground,
        ProfileDrivenVideoSettingsResolution? videoResolution = null,
        ToMkvGpuResolvedSourceBitrate? sourceBitrate = null)
    {
        TargetContainer = NormalizeRequiredToken(targetContainer, nameof(targetContainer));
        Video = NormalizeVideoPlan(video);
        Audio = NormalizeAudioPlan(audio);
        KeepSource = keepSource;
        OutputPath = NormalizeOutputPath(outputPath, nameof(outputPath));
        ApplyOverlayBackground = applyOverlayBackground;
        VideoResolution = videoResolution;
        SourceBitrate = sourceBitrate;
    }

    public string TargetContainer { get; }

    public VideoIntent Video { get; }

    public AudioIntent Audio { get; }

    public bool KeepSource { get; }

    public string OutputPath { get; }

    public bool ApplyOverlayBackground { get; }

    public ProfileDrivenVideoSettingsResolution? VideoResolution { get; }

    public ToMkvGpuResolvedSourceBitrate? SourceBitrate { get; }

    public bool CopyVideo => Video is CopyVideoIntent;

    public bool CopyAudio => Audio is CopyAudioIntent;

    public bool SynchronizeAudio => Audio is SynchronizeAudioIntent;

    public bool FixTimestamps => Audio is RepairAudioIntent;

    public bool RequiresVideoEncode => !CopyVideo;

    public bool RequiresAudioEncode => !CopyAudio;

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeOutputPath(string? outputPath, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath, paramName);
        return Path.GetFullPath(outputPath.Trim());
    }

    private static VideoIntent NormalizeVideoPlan(VideoIntent video)
    {
        ArgumentNullException.ThrowIfNull(video);
        return video switch
        {
            CopyVideoIntent => video,
            EncodeVideoIntent => video,
            _ => throw new ArgumentException($"Unsupported video plan type '{video.GetType().Name}'.", nameof(video))
        };
    }

    private static AudioIntent NormalizeAudioPlan(AudioIntent audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return audio switch
        {
            CopyAudioIntent => audio,
            SynchronizeAudioIntent => audio,
            RepairAudioIntent => audio,
            EncodeAudioIntent => audio,
            _ => throw new ArgumentException($"Unsupported audio plan type '{audio.GetType().Name}'.", nameof(audio))
        };
    }
}

internal sealed record ToMkvGpuResolvedSourceBitrate(long? Bitrate, string Origin);
