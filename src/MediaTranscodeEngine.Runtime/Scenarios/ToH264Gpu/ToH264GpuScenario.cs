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
    private const int DefaultAudioBitrateKbps = 160;
    private const int MinAudioBitrateKbps = 48;
    private const int MaxAudioBitrateKbps = 320;
    private const int DefaultCq = 19;

    /// <summary>
    /// Initializes a ToH264Gpu scenario with scenario-specific directives.
    /// </summary>
    public ToH264GpuScenario(ToH264GpuRequest? request = null)
        : base("toh264gpu")
    {
        Request = request ?? new ToH264GpuRequest();
    }

    /// <summary>
    /// Gets the scenario-specific directives carried by the ToH264Gpu workflow.
    /// </summary>
    public ToH264GpuRequest Request { get; }

    /// <inheritdoc />
    protected override TranscodePlan BuildPlanCore(SourceVideo video)
    {
        var targetContainer = Request.OutputMkv ? "mkv" : "mp4";
        var useDownscale = Request.DownscaleTargetHeight.HasValue && video.Height > Request.DownscaleTargetHeight.Value;
        var fixTimestamps = Request.FixTimestamps || RequiresAutomaticTimestampRepair(video);
        var remuxOnly = CanRemuxOnly(video, fixTimestamps, useDownscale);
        var copyAudio = remuxOnly || CanCopyAudio(video, fixTimestamps);
        var targetFramesPerSecond = remuxOnly
            ? (double?)null
            : ResolveTargetFramesPerSecond(video, useDownscale);
        var ffmpegOptions = BuildFfmpegOptions(video, remuxOnly, fixTimestamps, useDownscale, targetContainer);

        return new TranscodePlan(
            targetContainer: targetContainer,
            targetVideoCodec: remuxOnly ? null : "h264",
            preferredBackend: remuxOnly ? null : "gpu",
            videoCompatibilityProfile: remuxOnly ? null : VideoCompatibilityProfile.H264High,
            targetHeight: useDownscale ? Request.DownscaleTargetHeight : null,
            targetFramesPerSecond: targetFramesPerSecond,
            useFrameInterpolation: false,
            downscale: useDownscale ? Request.BuildDownscaleRequest() : null,
            copyVideo: remuxOnly,
            copyAudio: copyAudio,
            fixTimestamps: fixTimestamps,
            keepSource: false,
            encoderPreset: remuxOnly ? null : (Request.NvencPreset ?? "p5"),
            outputPath: ResolveOutputPath(video, targetContainer),
            ffmpegOptions: ffmpegOptions);
    }

    private bool CanRemuxOnly(SourceVideo video, bool fixTimestamps, bool useDownscale)
    {
        if (Request.Denoise || fixTimestamps || useDownscale)
        {
            return false;
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

        if (!video.VideoCodec.Equals("h264", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!CanCopyAudio(video, fixTimestamps))
        {
            return false;
        }

        return !HasVariableFrameRateSignal(video);
    }

    private bool CanCopyAudio(SourceVideo video, bool fixTimestamps)
    {
        if (fixTimestamps || !video.HasAudio)
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

    private FfmpegOptions BuildFfmpegOptions(
        SourceVideo video,
        bool remuxOnly,
        bool fixTimestamps,
        bool useDownscale,
        string targetContainer)
    {
        var audioBitrateKbps = ResolveAudioBitrateKbps(video);
        var rateControl = ResolveVideoRateControl(video, audioBitrateKbps, useDownscale);
        var usesAmrAudio = IsAmrNb(video.PrimaryAudioCodec);

        return new FfmpegOptions(
            optimizeForFastStart: targetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase),
            mapPrimaryAudioOnly: true,
            useHardwareDecode: remuxOnly ? null : useDownscale,
            enableAdaptiveQuantization: remuxOnly ? null : Request.UseAdaptiveQuantization,
            aqStrength: !remuxOnly && Request.UseAdaptiveQuantization ? Request.AqStrength : null,
            rcLookahead: !remuxOnly && Request.UseAdaptiveQuantization ? 32 : null,
            videoBitrateKbps: remuxOnly ? null : rateControl.VideoBitrateKbps,
            videoMaxrateKbps: remuxOnly ? null : rateControl.VideoMaxrateKbps,
            videoBufferSizeKbps: remuxOnly ? null : rateControl.VideoBufferSizeKbps,
            videoCq: remuxOnly ? null : rateControl.VideoCq,
            videoFilter: remuxOnly || useDownscale || !Request.Denoise ? null : "hqdn3d=1.2:1.2:6:6",
            pixelFormat: remuxOnly || useDownscale ? null : "yuv420p",
            audioBitrateKbps: remuxOnly || CanCopyAudio(video, fixTimestamps) ? null : audioBitrateKbps,
            audioSampleRate: usesAmrAudio ? 48000 : null,
            audioChannels: usesAmrAudio ? 1 : null,
            audioFilter: usesAmrAudio ? "aresample=48000:async=1:first_pts=0" : null);
    }

    private VideoRateControl ResolveVideoRateControl(SourceVideo video, int audioBitrateKbps, bool useDownscale)
    {
        if (Request.Cq.HasValue)
        {
            if (!useDownscale)
            {
                return new VideoRateControl(videoCq: Request.Cq.Value);
            }

            var maxCap = Request.DownscaleTargetHeight == 720 ? 4500 : 3000;
            return new VideoRateControl(
                videoCq: Request.Cq.Value,
                videoMaxrateKbps: maxCap,
                videoBufferSizeKbps: maxCap * 2);
        }

        if (useDownscale)
        {
            var maxCap = Request.DownscaleTargetHeight == 720 ? 4500 : 3000;
            var sourceVideoBitrateKbps = TryResolveSourceVideoBitrateKbps(video, audioBitrateKbps);
            if (!sourceVideoBitrateKbps.HasValue || video.Height <= 0)
            {
                return new VideoRateControl(
                    videoCq: DefaultCq,
                    videoMaxrateKbps: maxCap,
                    videoBufferSizeKbps: maxCap * 2);
            }

            var areaRatio = Math.Pow(Request.DownscaleTargetHeight!.Value / (double)video.Height, 2);
            var logBoost = 1.0 + (0.35 * Math.Log10(Math.Max(1.0, sourceVideoBitrateKbps.Value / 1000.0)));
            var scaledRatio = Math.Min(1.0, areaRatio * logBoost);
            var targetBitrate = (int)Math.Round(sourceVideoBitrateKbps.Value * scaledRatio * 0.90, MidpointRounding.AwayFromZero);
            if (targetBitrate <= 0)
            {
                return new VideoRateControl(
                    videoCq: DefaultCq,
                    videoMaxrateKbps: maxCap,
                    videoBufferSizeKbps: maxCap * 2);
            }

            targetBitrate = Math.Min(targetBitrate, maxCap);
            return new VideoRateControl(
                videoBitrateKbps: targetBitrate,
                videoMaxrateKbps: maxCap,
                videoBufferSizeKbps: maxCap * 2);
        }

        var totalBitrateKbps = TryResolveTotalBitrateKbps(video);
        if (totalBitrateKbps.HasValue)
        {
            var targetBitrate = Math.Max(300, totalBitrateKbps.Value - audioBitrateKbps - 64);
            var maxrate = Math.Max(400, (int)Math.Round(targetBitrate * 1.30, MidpointRounding.AwayFromZero));
            var bufsize = Math.Max(800, maxrate * 2);
            return new VideoRateControl(
                videoBitrateKbps: targetBitrate,
                videoMaxrateKbps: maxrate,
                videoBufferSizeKbps: bufsize);
        }

        return new VideoRateControl(videoCq: DefaultCq);
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

    private static int? TryResolveSourceVideoBitrateKbps(SourceVideo video, int audioBitrateKbps)
    {
        var totalBitrateKbps = TryResolveTotalBitrateKbps(video);
        return totalBitrateKbps.HasValue
            ? Math.Max(0, totalBitrateKbps.Value - audioBitrateKbps - 64)
            : null;
    }

    private static int? TryResolveTotalBitrateKbps(SourceVideo video)
    {
        if (video.Duration <= TimeSpan.FromSeconds(0.1) ||
            string.IsNullOrWhiteSpace(video.FilePath) ||
            !File.Exists(video.FilePath))
        {
            return null;
        }

        var fileSizeBytes = new FileInfo(video.FilePath).Length;
        if (fileSizeBytes <= 0)
        {
            return null;
        }

        var totalBitrate = Math.Round((fileSizeBytes * 8.0 / 1000.0) / video.Duration.TotalSeconds, MidpointRounding.AwayFromZero);
        return totalBitrate > 0
            ? (int)totalBitrate
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

    private static string ResolveOutputPath(SourceVideo video, string targetContainer)
    {
        var directory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}.{targetContainer}");
    }

    private sealed class VideoRateControl
    {
        public VideoRateControl(
            int? videoBitrateKbps = null,
            int? videoMaxrateKbps = null,
            int? videoBufferSizeKbps = null,
            int? videoCq = null)
        {
            VideoBitrateKbps = videoBitrateKbps;
            VideoMaxrateKbps = videoMaxrateKbps;
            VideoBufferSizeKbps = videoBufferSizeKbps;
            VideoCq = videoCq;
        }

        public int? VideoBitrateKbps { get; }

        public int? VideoMaxrateKbps { get; }

        public int? VideoBufferSizeKbps { get; }

        public int? VideoCq { get; }
    }
}
