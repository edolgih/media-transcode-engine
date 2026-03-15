using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.Failures;
using Transcode.Core.MediaIntent;
using Transcode.Core.Scenarios;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToMkvGpu.Core;

/*
Это прикладной сценарий tomkvgpu.
Он решает, достаточно ли remux в mkv, или нужно строить план GPU-кодирования в H.264/H.265.
*/
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

    private readonly VideoSettingsProfiles _videoSettingsProfiles;
    private readonly VideoSettingsResolver _videoSettingsResolver;
    private readonly Func<string, int, VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?> _sampleReductionProvider;
    private readonly ToMkvGpuFfmpegTool _ffmpegTool;
    private static readonly ToMkvGpuInfoFormatter InfoFormatter = new();

    /// <summary>
    /// Initializes a ToMkvGpu scenario with scenario-specific directives.
    /// </summary>
    public ToMkvGpuScenario()
        : this(new ToMkvGpuRequest(), VideoSettingsProfiles.Default, sampleReductionProvider: null, CreateDefaultTool())
    {
    }

    /// <summary>
    /// Initializes a ToMkvGpu scenario with scenario-specific directives.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToMkvGpu workflow.</param>
    public ToMkvGpuScenario(ToMkvGpuRequest request)
        : this(request, VideoSettingsProfiles.Default, sampleReductionProvider: null, CreateDefaultTool())
    {
    }

    /// <summary>
    /// Initializes a ToMkvGpu scenario with scenario-specific directives and a concrete ffmpeg renderer.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToMkvGpu workflow.</param>
    /// <param name="ffmpegTool">Concrete ffmpeg renderer used by this scenario.</param>
    public ToMkvGpuScenario(ToMkvGpuRequest request, ToMkvGpuFfmpegTool ffmpegTool)
        : this(request, VideoSettingsProfiles.Default, sampleReductionProvider: null, ffmpegTool)
    {
    }

    /// <summary>
    /// Initializes a ToMkvGpu scenario with an explicit sample measurer for scenario-local execution payloads.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToMkvGpu workflow.</param>
    /// <param name="sampleMeasurer">Measurer used for sample-backed autosample resolution.</param>
    public ToMkvGpuScenario(ToMkvGpuRequest request, FfmpegSampleMeasurer sampleMeasurer)
        : this(
            request,
            VideoSettingsProfiles.Default,
            (sampleMeasurer ?? throw new ArgumentNullException(nameof(sampleMeasurer))).MeasureAverageReduction,
            CreateDefaultTool())
    {
    }

    /// <summary>
    /// Initializes a ToMkvGpu scenario with an explicit sample measurer and a concrete ffmpeg renderer.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToMkvGpu workflow.</param>
    /// <param name="sampleMeasurer">Measurer used for sample-backed autosample resolution.</param>
    /// <param name="ffmpegTool">Concrete ffmpeg renderer used by this scenario.</param>
    public ToMkvGpuScenario(
        ToMkvGpuRequest request,
        FfmpegSampleMeasurer sampleMeasurer,
        ToMkvGpuFfmpegTool ffmpegTool)
        : this(
            request,
            VideoSettingsProfiles.Default,
            (sampleMeasurer ?? throw new ArgumentNullException(nameof(sampleMeasurer))).MeasureAverageReduction,
            ffmpegTool)
    {
    }

    internal ToMkvGpuScenario(ToMkvGpuRequest request, VideoSettingsProfiles videoSettingsProfiles)
        : this(request, videoSettingsProfiles, sampleReductionProvider: null, CreateDefaultTool())
    {
    }

    internal ToMkvGpuScenario(
        ToMkvGpuRequest request,
        VideoSettingsProfiles videoSettingsProfiles,
        Func<string, int, VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? sampleReductionProvider)
        : this(request, videoSettingsProfiles, sampleReductionProvider, CreateDefaultTool())
    {
    }

    internal ToMkvGpuScenario(
        ToMkvGpuRequest request,
        VideoSettingsProfiles videoSettingsProfiles,
        Func<string, int, VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? sampleReductionProvider,
        ToMkvGpuFfmpegTool ffmpegTool)
        : base("tomkvgpu")
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        _videoSettingsProfiles = videoSettingsProfiles ?? throw new ArgumentNullException(nameof(videoSettingsProfiles));
        _videoSettingsResolver = new VideoSettingsResolver(_videoSettingsProfiles);
        _sampleReductionProvider = sampleReductionProvider ?? NoSampleReduction;
        _ffmpegTool = ffmpegTool ?? throw new ArgumentNullException(nameof(ffmpegTool));
    }

    /// <summary>
    /// Gets the scenario-specific directives carried by the ToMkvGpu workflow.
    /// </summary>
    public ToMkvGpuRequest Request { get; }

    /// <summary>
    /// Builds the resolved tomkvgpu decision from the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    internal ToMkvGpuDecision BuildDecision(SourceVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        var applyDownscale = Request.Downscale is not null &&
                             video.Height > Request.Downscale.TargetHeight;
        ValidateDownscale(video, applyDownscale);

        var applyFrameRateCap = Request.MaxFramesPerSecond.HasValue &&
                                video.FramesPerSecond > Request.MaxFramesPerSecond.Value;
        var requiresTimestampFix = TimestampSensitiveExtensions.Contains(video.FileExtension);
        var copyVideo = VideoCopyCodecs.Contains(video.VideoCodec) &&
                        !requiresTimestampFix &&
                        !Request.OverlayBackground &&
                        !applyDownscale &&
                        !applyFrameRateCap;
        var copyAudio = !Request.SynchronizeAudio &&
                        copyVideo &&
                        AreAudioStreamsCopyCompatible(video.AudioCodecs);
        AudioIntent audioIntent = copyAudio
            ? new CopyAudioIntent()
            : Request.SynchronizeAudio
                ? new SynchronizeAudioIntent()
                : requiresTimestampFix
                    ? new RepairAudioIntent()
                    : new EncodeAudioIntent();
        VideoIntent videoIntent = copyVideo
            ? new CopyVideoIntent()
            : new EncodeVideoIntent(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: H264OutputProfile.H264High,
                TargetFramesPerSecond: applyFrameRateCap ? Request.MaxFramesPerSecond : null,
                UseFrameInterpolation: false,
                VideoSettings: Request.VideoSettings,
                Downscale: applyDownscale ? Request.Downscale : null,
                EncoderPreset: Request.NvencPreset);

        ProfileDrivenVideoSettingsResolution? videoResolution = null;
        ToMkvGpuResolvedSourceBitrate? sourceBitrate = null;
        if (videoIntent is EncodeVideoIntent encodeVideo)
        {
            var outputHeight = ResolveOutputHeight(video, videoIntent, Request.OverlayBackground, encodeVideo.Downscale);
            var actualSampleHeight = encodeVideo.Downscale?.TargetHeight ?? outputHeight;
            sourceBitrate = ResolveSourceBitrate(video);
            Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider = actualSampleHeight > 0
                ? (settings, windows) => _sampleReductionProvider(video.FilePath, actualSampleHeight, settings, windows)
                : null;
            videoResolution = encodeVideo.Downscale is not null
                ? _videoSettingsResolver.ResolveForDownscale(
                    request: encodeVideo.Downscale,
                    videoSettings: encodeVideo.VideoSettings,
                    sourceHeight: video.Height,
                    duration: video.Duration,
                    sourceBitrate: sourceBitrate.Bitrate,
                    hasAudio: video.HasAudio,
                    defaultAutoSampleMode: "hybrid",
                    accurateReductionProvider: accurateReductionProvider)
                : _videoSettingsResolver.ResolveForEncode(
                    request: encodeVideo.VideoSettings,
                    outputHeight: outputHeight,
                    duration: video.Duration,
                    sourceBitrate: sourceBitrate.Bitrate,
                    hasAudio: video.HasAudio,
                    defaultAutoSampleMode: "fast",
                    accurateReductionProvider: accurateReductionProvider);
        }

        return new ToMkvGpuDecision(
            targetContainer: "mkv",
            video: videoIntent,
            audio: audioIntent,
            keepSource: Request.KeepSource,
            outputPath: ResolveOutputPath(video, copyVideo, copyAudio),
            applyOverlayBackground: Request.OverlayBackground,
            videoResolution: videoResolution,
            sourceBitrate: sourceBitrate);
    }

    /// <inheritdoc />
    protected override string FormatInfoCore(SourceVideo video)
    {
        return InfoFormatter.Format(video, BuildDecision(video));
    }

    /// <inheritdoc />
    protected override ScenarioExecution BuildExecutionCore(SourceVideo video)
    {
        return _ffmpegTool.BuildExecution(video, BuildDecision(video));
    }

    private void ValidateDownscale(SourceVideo video, bool applyDownscale)
    {
        var targetHeight = Request.Downscale?.TargetHeight;
        if (!targetHeight.HasValue)
        {
            return;
        }

        if (!applyDownscale && video.Height > 0)
        {
            return;
        }

        if (!_videoSettingsProfiles.TryGetProfile(targetHeight.Value, out var profile))
        {
            return;
        }

        var issue = profile.ResolveSourceBucketIssue(video.Height);
        if (!string.IsNullOrWhiteSpace(issue))
        {
            throw RuntimeFailures.DownscaleSourceBucketIssue(issue);
        }
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

    private static int ResolveOutputHeight(SourceVideo video, VideoIntent videoIntent, bool applyOverlayBackground, DownscaleRequest? downscale)
    {
        var (_, height) = ToMkvGpuVideoGeometry.ResolveOutputDimensions(video, videoIntent, applyOverlayBackground);
        if (height > 0)
        {
            return height;
        }

        if (downscale?.TargetHeight > 0)
        {
            return downscale.TargetHeight;
        }

        return Math.Max(1, video.Height);
    }

    private static ToMkvGpuResolvedSourceBitrate ResolveSourceBitrate(SourceVideo video)
    {
        if (video.Bitrate.HasValue)
        {
            return new ToMkvGpuResolvedSourceBitrate(video.Bitrate.Value, "probe");
        }

        if (video.Duration <= TimeSpan.Zero || string.IsNullOrWhiteSpace(video.FilePath) || !File.Exists(video.FilePath))
        {
            return new ToMkvGpuResolvedSourceBitrate(null, "missing");
        }

        var fileSizeBytes = new FileInfo(video.FilePath).Length;
        if (fileSizeBytes <= 0)
        {
            return new ToMkvGpuResolvedSourceBitrate(null, "missing");
        }

        var estimatedBitsPerSecond = Math.Round((fileSizeBytes * 8m) / (decimal)video.Duration.TotalSeconds, MidpointRounding.AwayFromZero);
        if (estimatedBitsPerSecond <= 0m || estimatedBitsPerSecond > long.MaxValue)
        {
            return new ToMkvGpuResolvedSourceBitrate(null, "missing");
        }

        return new ToMkvGpuResolvedSourceBitrate((long)estimatedBitsPerSecond, "file_size_estimate");
    }

    private static decimal? NoSampleReduction(
        string inputPath,
        int outputHeight,
        VideoSettingsDefaults settings,
        IReadOnlyList<VideoSettingsSampleWindow> windows)
    {
        return null;
    }

    private static ToMkvGpuFfmpegTool CreateDefaultTool()
    {
        return new ToMkvGpuFfmpegTool("ffmpeg", NullLogger<ToMkvGpuFfmpegTool>.Instance);
    }
}
