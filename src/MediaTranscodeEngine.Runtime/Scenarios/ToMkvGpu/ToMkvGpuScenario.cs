using MediaTranscodeEngine.Runtime.Downscaling;
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

    private readonly DownscaleProfiles _downscaleProfiles;

    /// <summary>
    /// Initializes a ToMkvGpu scenario with scenario-specific directives.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToMkvGpu workflow.</param>
    public ToMkvGpuScenario(ToMkvGpuRequest? request = null)
        : this(request, DownscaleProfiles.Default)
    {
    }

    internal ToMkvGpuScenario(ToMkvGpuRequest? request, DownscaleProfiles downscaleProfiles)
        : base("tomkvgpu")
    {
        Request = request ?? new ToMkvGpuRequest();
        _downscaleProfiles = downscaleProfiles ?? throw new ArgumentNullException(nameof(downscaleProfiles));
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
        if (Request.Downscale?.TargetHeight == 720)
        {
            throw new NotSupportedException("Downscale 720 is not implemented for ToMkvGpu.");
        }

        var applyDownscale = Request.Downscale?.TargetHeight.HasValue == true &&
                             video.Height > Request.Downscale.TargetHeight.Value;
        ValidateDownscale(video, applyDownscale);

        var effectiveDownscale = ResolveEffectiveDownscale(applyDownscale);
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
            videoCompatibilityProfile: copyVideo ? null : VideoCompatibilityProfile.H264High,
            targetHeight: applyDownscale ? Request.Downscale!.TargetHeight : null,
            targetFramesPerSecond: null,
            useFrameInterpolation: false,
            downscale: effectiveDownscale,
            copyVideo: copyVideo,
            copyAudio: copyAudio,
            fixTimestamps: requiresTimestampFix || !copyVideo || Request.SynchronizeAudio,
            keepSource: Request.KeepSource,
            encoderPreset: Request.NvencPreset,
            outputPath: ResolveOutputPath(video, copyVideo, copyAudio),
            applyOverlayBackground: Request.OverlayBackground,
            synchronizeAudio: Request.SynchronizeAudio);
    }

    private void ValidateDownscale(SourceVideo video, bool applyDownscale)
    {
        if (Request.Downscale?.TargetHeight != 576)
        {
            return;
        }

        if (!applyDownscale && video.Height > 0)
        {
            return;
        }

        var profile = _downscaleProfiles.GetRequiredProfile(576);
        var issue = profile.ResolveSourceBucketIssue(video.Height);
        if (!string.IsNullOrWhiteSpace(issue))
        {
            throw new InvalidOperationException(issue);
        }
    }

    private DownscaleRequest? ResolveEffectiveDownscale(bool applyDownscale)
    {
        if (Request.Downscale is null)
        {
            return null;
        }

        if (applyDownscale)
        {
            return Request.Downscale;
        }

        if (!Request.Downscale.TargetHeight.HasValue)
        {
            return Request.Downscale;
        }

        return null;
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
