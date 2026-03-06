using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/// <summary>
/// Represents the legacy ToMkvGpu use case as a scenario that decides when MKV remuxing is enough and when H.264 GPU encoding is required.
/// </summary>
public sealed class ToMkvGpuScenario : TranscodeScenario
{
    private static readonly HashSet<string> VideoCopyCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "h264",
        "mpeg4"
    };

    private static readonly HashSet<string> TimestampSensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wmv",
        ".asf"
    };

    /// <summary>
    /// Initializes a ToMkvGpu scenario with scenario-specific directives.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToMkvGpu workflow.</param>
    public ToMkvGpuScenario(ToMkvGpuRequest? request = null)
        : base("tomkvgpu")
    {
        Request = request ?? new ToMkvGpuRequest();
    }

    /// <summary>
    /// Gets the scenario-specific directives carried by the ToMkvGpu workflow.
    /// </summary>
    public ToMkvGpuRequest Request { get; }

    /// <summary>
    /// Builds a ToMkvGpu plan from the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>A tool-agnostic plan describing the required MKV conversion work.</returns>
    protected override TranscodePlan BuildPlanCore(SourceVideo video)
    {
        if (Request.DownscaleTarget == 720)
        {
            throw new NotSupportedException("Downscale 720 is not implemented for ToMkvGpu.");
        }

        var applyDownscale = Request.DownscaleTarget.HasValue && video.Height > Request.DownscaleTarget.Value;
        var requiresTimestampFix = TimestampSensitiveExtensions.Contains(video.FileExtension);
        var copyVideo = VideoCopyCodecs.Contains(video.VideoCodec) &&
                        !requiresTimestampFix &&
                        !Request.OverlayBackground &&
                        !applyDownscale;
        var copyAudio = !Request.SynchronizeAudio &&
                        copyVideo &&
                        AreAudioStreamsCopyCompatible(video.AudioCodecs);

        return new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: copyVideo ? null : "h264",
            preferredBackend: copyVideo ? null : "gpu",
            targetHeight: applyDownscale ? Request.DownscaleTarget : null,
            targetFramesPerSecond: null,
            useFrameInterpolation: false,
            copyVideo: copyVideo,
            copyAudio: copyAudio,
            fixTimestamps: requiresTimestampFix || !copyVideo,
            keepSource: Request.KeepSource,
            outputPath: ResolveOutputPath(video, copyVideo, copyAudio),
            applyOverlayBackground: Request.OverlayBackground,
            synchronizeAudio: Request.SynchronizeAudio);
    }

    private static bool AreAudioStreamsCopyCompatible(IReadOnlyList<string> audioCodecs)
    {
        if (audioCodecs.Count == 0)
        {
            return true;
        }

        return audioCodecs.All(codec => codec.Equals("aac", StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveOutputPath(SourceVideo video, bool copyVideo, bool copyAudio)
    {
        var directory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        var outputPath = Path.Combine(directory, $"{video.FileNameWithoutExtension}.mkv");
        if (!Request.KeepSource ||
            !video.Container.Equals("mkv", StringComparison.OrdinalIgnoreCase) ||
            (copyVideo && copyAudio))
        {
            return outputPath;
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}_out.mkv");
    }
}
