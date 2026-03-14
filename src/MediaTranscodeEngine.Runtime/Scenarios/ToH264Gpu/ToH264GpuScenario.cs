using MediaTranscodeEngine.Runtime.VideoSettings;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;

/*
Это прикладной сценарий toh264gpu.
Он решает, когда достаточно remux-only, когда нужно полное NVENC-перекодирование,
и какие узкие ffmpeg-настройки нужны для сохранения legacy-поведения без раздувания общей модели.
*/
/// <summary>
/// Represents the legacy ToH264Gpu use case as a scenario that prefers mp4-compatible remuxing and falls back to H.264 NVENC encoding.
/// </summary>
public sealed class ToH264GpuScenario : TranscodeScenario
{
    private const double FrameRateTolerance = 0.0001;
    private const double DownscaleFrameRateCap = 30000d / 1001d;
    private const int DefaultAudioBitrateKbps = 192;
    private const int MinAudioBitrateKbps = 48;
    private const int MaxAudioBitrateKbps = 320;
    private static readonly VideoSettingsResolver VideoSettingsResolver = new(VideoSettingsProfiles.Default);

    /// <summary>
    /// Initializes a ToH264Gpu scenario with scenario-specific directives.
    /// </summary>
    public ToH264GpuScenario()
        : this(new ToH264GpuRequest())
    {
    }

    /// <summary>
    /// Initializes a ToH264Gpu scenario with scenario-specific directives.
    /// </summary>
    public ToH264GpuScenario(ToH264GpuRequest request)
        : base("toh264gpu")
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    /// <summary>
    /// Gets the scenario-specific directives carried by the ToH264Gpu workflow.
    /// </summary>
    public ToH264GpuRequest Request { get; }

    /// <inheritdoc />
    protected override TranscodePlan BuildPlanCore(SourceVideo video)
    {
        var targetContainer = Request.OutputMkv ? "mkv" : "mp4";
        var downscaleRequest = Request.Downscale;
        var useDownscale = downscaleRequest is not null && video.Height > downscaleRequest.TargetHeight;
        var synchronizeAudio = Request.SynchronizeAudio || RequiresAutomaticTimestampRepair(video);
        var videoCopyCompatible = CanCopyVideo(video, targetContainer, useDownscale);
        var copyVideo = !synchronizeAudio && videoCopyCompatible && CanCopyAudio(video) ||
                        synchronizeAudio && videoCopyCompatible;
        var copyAudio = !synchronizeAudio && CanCopyAudio(video);
        AudioPlan audioPlan = copyAudio
            ? new CopyAudioPlan()
            : synchronizeAudio
                ? new SynchronizeAudioPlan()
                : new EncodeAudioPlan();
        var videoSettingsRequest = copyVideo
            ? null
            : Request.VideoSettings;
        var targetFramesPerSecond = copyVideo
            ? (double?)null
            : ResolveTargetFramesPerSecond(video, useDownscale);
        var resolvedDownscale = useDownscale && downscaleRequest is not null
            ? downscaleRequest.Algorithm is null
                ? new DownscaleRequest(downscaleRequest.TargetHeight, "bicubic")
                : downscaleRequest
            : null;
        VideoPlan videoPlan = copyVideo
            ? new CopyVideoPlan()
            : new EncodeVideoPlan(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: VideoCompatibilityProfile.H264High,
                TargetFramesPerSecond: targetFramesPerSecond,
                UseFrameInterpolation: false,
                VideoSettings: videoSettingsRequest,
                Downscale: resolvedDownscale,
                EncoderPreset: Request.NvencPreset ?? "p6");

        return new TranscodePlan(
            targetContainer: targetContainer,
            video: videoPlan,
            audio: audioPlan,
            keepSource: Request.KeepSource,
            outputPath: ResolveOutputPath(video, targetContainer));
    }

    /// <inheritdoc />
    protected override TranscodeExecutionSpec? BuildExecutionSpecCore(SourceVideo video, TranscodePlan plan)
    {
        return BuildToH264GpuExecutionSpec(video, plan);
    }

    private bool CanCopyVideo(SourceVideo video, string targetContainer, bool useDownscale)
    {
        if (Request.Denoise || useDownscale)
        {
            return false;
        }

        if (!video.VideoCodec.Equals("h264", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (HasVariableFrameRateSignal(video))
        {
            return false;
        }

        if (targetContainer.Equals("mkv", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IsMp4Family(video.FormatName))
        {
            return false;
        }

        if (!video.FileExtension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) &&
            !video.FileExtension.Equals(".m4v", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private bool CanCopyAudio(SourceVideo video)
    {
        if (!video.HasAudio)
        {
            return !video.HasAudio;
        }

        var codec = video.PrimaryAudioCodec;
        return codec is not null &&
               (codec.Equals("aac", StringComparison.OrdinalIgnoreCase) ||
                codec.Equals("mp3", StringComparison.OrdinalIgnoreCase));
    }

    private double ResolveTargetFramesPerSecond(SourceVideo video, bool useDownscale)
    {
        if (useDownscale &&
            !Request.KeepFramesPerSecond &&
            video.FramesPerSecond > 30.0)
        {
            return DownscaleFrameRateCap;
        }

        return video.FramesPerSecond;
    }

    private ToH264GpuExecutionSpec BuildToH264GpuExecutionSpec(SourceVideo video, TranscodePlan plan)
    {
        var encodeVideo = plan.Video as EncodeVideoPlan;
        var copyVideo = encodeVideo is null;
        var copyAudio = plan.CopyAudio;
        var useDownscale = encodeVideo?.Downscale is not null;
        var targetContainer = plan.TargetContainer;
        var downscaleRequest = encodeVideo?.Downscale;
        var videoSettingsRequest = encodeVideo?.VideoSettings;
        var videoSettings = copyVideo
            ? null
            : ResolveVideoSettings(video, useDownscale, downscaleRequest, videoSettingsRequest);
        var mux = new ToH264GpuExecutionSpec.MuxExecution(
            optimizeForFastStart: targetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase),
            mapPrimaryAudioOnly: true);
        var videoExecution = copyVideo || videoSettings is null
            ? null
            : BuildVideoExecution(videoSettings, useDownscale);
        var audioExecution = copyAudio
            ? null
            : BuildAudioExecution(video, plan.Audio);

        return new ToH264GpuExecutionSpec(
            mux: mux,
            video: videoExecution,
            audio: audioExecution);
    }

    private ToH264GpuExecutionSpec.VideoExecution BuildVideoExecution(VideoSettingsDefaults videoSettings, bool useDownscale)
    {
        return new ToH264GpuExecutionSpec.VideoExecution(
            useHardwareDecode: useDownscale,
            rateControl: new ToH264GpuExecutionSpec.ConstantQualityVideoRateControlExecution(
                cq: videoSettings.Cq,
                maxrateKbps: ToKbps(videoSettings.Maxrate),
                bufferSizeKbps: ToKbps(videoSettings.Bufsize)),
            adaptiveQuantization: new ToH264GpuExecutionSpec.AdaptiveQuantizationExecution(rcLookahead: 32),
            filter: useDownscale || !Request.Denoise
                ? null
                : "hqdn3d=1.2:1.2:6:6",
            pixelFormat: useDownscale ? null : "yuv420p");
    }

    private static ToH264GpuExecutionSpec.AudioExecution BuildAudioExecution(SourceVideo video, AudioPlan audioPlan)
    {
        var usesAmrAudio = IsAmrNb(video.PrimaryAudioCodec);
        var requiresRepair = audioPlan is SynchronizeAudioPlan or RepairAudioPlan;

        return new ToH264GpuExecutionSpec.AudioExecution(
            bitrateKbps: ResolveAudioBitrateKbps(video),
            sampleRate: usesAmrAudio || requiresRepair ? 48000 : null,
            channels: usesAmrAudio ? 1 : requiresRepair ? 2 : null,
            filter: BuildAudioFilter(usesAmrAudio, requiresRepair));
    }

    private static string? BuildAudioFilter(bool usesAmrAudio, bool requiresRepair)
    {
        if (usesAmrAudio)
        {
            return "aresample=48000:async=1:first_pts=0";
        }

        return requiresRepair
            ? "aresample=async=1:first_pts=0"
            : null;
    }

    private static VideoSettingsDefaults ResolveVideoSettings(SourceVideo video, bool useDownscale, DownscaleRequest? downscaleRequest, VideoSettingsRequest? request)
    {
        if (useDownscale)
        {
            return VideoSettingsResolver.ResolveForDownscale(
                downscaleRequest ?? throw new InvalidOperationException("Downscale request is required when downscale mode is active."),
                videoSettings: request,
                sourceHeight: video.Height,
                duration: video.Duration,
                sourceBitrate: ResolveSourceBitrate(video),
                hasAudio: video.HasAudio,
                defaultAutoSampleMode: "hybrid").Settings;
        }

        return VideoSettingsResolver.ResolveForEncode(
            request: request,
            outputHeight: Math.Max(1, video.Height),
            duration: video.Duration,
            sourceBitrate: ResolveSourceBitrate(video),
            hasAudio: video.HasAudio,
            defaultAutoSampleMode: "fast").Settings;
    }

    private static int ResolveAudioBitrateKbps(SourceVideo video)
    {
        if (!video.PrimaryAudioBitrate.HasValue || video.PrimaryAudioBitrate.Value <= 0)
        {
            return DefaultAudioBitrateKbps;
        }

        var audioBitrateKbps = (int)Math.Round(video.PrimaryAudioBitrate.Value / 1000.0, MidpointRounding.AwayFromZero);
        return Math.Min(MaxAudioBitrateKbps, Math.Max(MinAudioBitrateKbps, audioBitrateKbps));
    }

    private static int ToKbps(decimal value)
    {
        return (int)Math.Round(value * 1000m, MidpointRounding.AwayFromZero);
    }

    private static long? ResolveSourceBitrate(SourceVideo video)
    {
        if (video.Bitrate.HasValue)
        {
            return video.Bitrate.Value;
        }

        if (video.Duration <= TimeSpan.FromSeconds(0.1) ||
            string.IsNullOrWhiteSpace(video.FilePath) ||
            !File.Exists(video.FilePath))
        {
            return null;
        }

        var fileSizeBits = new FileInfo(video.FilePath).Length * 8m;
        if (fileSizeBits <= 0m)
        {
            return null;
        }

        var totalBitrate = Math.Round(fileSizeBits / (decimal)video.Duration.TotalSeconds, MidpointRounding.AwayFromZero);
        return totalBitrate > 0m && totalBitrate <= long.MaxValue
            ? (long)totalBitrate
            : null;
    }

    private static bool RequiresAutomaticTimestampRepair(SourceVideo video)
    {
        if (video.FileExtension.Equals(".wmv", StringComparison.OrdinalIgnoreCase) ||
            video.FileExtension.Equals(".asf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(video.FormatName) &&
               video.FormatName.Contains("asf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMp4Family(string? formatName)
    {
        if (string.IsNullOrWhiteSpace(formatName))
        {
            return false;
        }

        return formatName.Contains("mov", StringComparison.OrdinalIgnoreCase) ||
               formatName.Contains("mp4", StringComparison.OrdinalIgnoreCase) ||
               formatName.Contains("m4a", StringComparison.OrdinalIgnoreCase) ||
               formatName.Contains("3gp", StringComparison.OrdinalIgnoreCase) ||
               formatName.Contains("3g2", StringComparison.OrdinalIgnoreCase) ||
               formatName.Contains("mj2", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasVariableFrameRateSignal(SourceVideo video)
    {
        if (!video.RawFramesPerSecond.HasValue || !video.AverageFramesPerSecond.HasValue)
        {
            return false;
        }

        return Math.Abs(video.RawFramesPerSecond.Value - video.AverageFramesPerSecond.Value) > FrameRateTolerance;
    }

    private static bool IsAmrNb(string? codec)
    {
        return codec is not null &&
               (codec.Equals("amr_nb", StringComparison.OrdinalIgnoreCase) ||
                codec.Equals("amrnb", StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveOutputPath(SourceVideo video, string targetContainer)
    {
        var directory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        var outputPath = Path.Combine(directory, $"{video.FileNameWithoutExtension}.{targetContainer}");
        if (!Request.KeepSource ||
            !outputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return outputPath;
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}_out.{targetContainer}");
    }

}
