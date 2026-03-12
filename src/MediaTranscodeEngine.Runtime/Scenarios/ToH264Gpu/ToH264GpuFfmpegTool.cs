using System.Globalization;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Tools.Ffmpeg;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;

/*
Это ffmpeg-адаптер сценария toh264gpu.
Он рендерит web/stream/local-ориентированный план в ffmpeg-команду с remux или H.264 NVENC encode path.
*/
/// <summary>
/// Renders web/stream-oriented toh264gpu plans into ffmpeg execution recipes.
/// </summary>
public sealed class ToH264GpuFfmpegTool : ITranscodeTool
{
    private readonly string _ffmpegPath;
    private readonly ILogger<ToH264GpuFfmpegTool> _logger;

    /// <summary>
    /// Initializes the toh264gpu-specific ffmpeg tool.
    /// </summary>
    public ToH264GpuFfmpegTool(string ffmpegPath, ILogger<ToH264GpuFfmpegTool> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentNullException.ThrowIfNull(logger);

        _ffmpegPath = ffmpegPath.Trim();
        _logger = logger;
    }

    /// <summary>
    /// Gets the stable tool name.
    /// </summary>
    public string Name => "ffmpeg";

    /// <summary>
    /// Determines whether the toh264gpu-specific ffmpeg tool can execute the supplied plan.
    /// </summary>
    public bool CanHandle(TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.FfmpegOptions is null || plan.UseFrameInterpolation)
        {
            return false;
        }

        if (!plan.TargetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase) &&
            !plan.TargetContainer.Equals("mkv", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (plan.CopyVideo)
        {
            return true;
        }

        return plan.PreferredBackend?.Equals("gpu", StringComparison.OrdinalIgnoreCase) == true &&
               plan.TargetVideoCodec?.Equals("h264", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Builds an ffmpeg execution recipe for the supplied source video and plan.
    /// </summary>
    public ToolExecution BuildExecution(SourceVideo video, TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        if (!CanHandle(plan))
        {
            throw new NotSupportedException("The supplied transcode plan is not supported by ToH264Gpu ffmpeg tool.");
        }

        if (IsNoOp(video, plan))
        {
            return new ToolExecution(Name, Array.Empty<string>());
        }

        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(video, plan);
        var workingOutputPath = FfmpegExecutionLayout.ResolveWorkingOutputPath(video, plan, finalOutputPath);
        var ffmpegCommand = BuildFfmpegCommand(video, plan, workingOutputPath);
        var commands = new List<string> { ffmpegCommand };

        FfmpegExecutionLayout.AppendPostOperations(commands, video, plan, workingOutputPath, finalOutputPath);

        return new ToolExecution(Name, commands);
    }

    private static bool IsNoOp(SourceVideo video, TranscodePlan plan)
    {
        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(video, plan);
        return plan.CopyVideo &&
               plan.CopyAudio &&
               !RequiresMuxRewrite(plan) &&
               video.Container.Equals(plan.TargetContainer, StringComparison.OrdinalIgnoreCase) &&
               finalOutputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildFfmpegCommand(SourceVideo video, TranscodePlan plan, string outputPath)
    {
        var parts = new List<string>
        {
            _ffmpegPath,
            "-hide_banner"
        };

        var sanitizePart = BuildSanitizePart(video, plan);
        if (!string.IsNullOrWhiteSpace(sanitizePart))
        {
            parts.Add(sanitizePart);
        }

        if (ShouldUseHardwareDecode(plan))
        {
            parts.Add("-hwaccel cuda -hwaccel_output_format cuda");
        }

        parts.Add("-i");
        parts.Add(FfmpegExecutionLayout.Quote(video.FilePath));
        parts.Add(BuildVideoPart(video, plan));
        parts.Add(BuildAudioPart(plan));
        parts.Add("-sn");

        if (plan.TargetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase) &&
            plan.FfmpegOptions!.OptimizeForFastStart)
        {
            parts.Add("-movflags +faststart");
        }

        parts.Add("-max_muxing_queue_size 4096");
        parts.Add(FfmpegExecutionLayout.Quote(outputPath));

        return string.Join(" ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildSanitizePart(SourceVideo video, TranscodePlan plan)
    {
        if (plan.FixTimestamps || UsesStrongSyncRemux(plan))
        {
            return "-fflags +genpts+igndts -avoid_negative_ts make_zero";
        }

        var needsContainerChange = !video.Container.Equals(plan.TargetContainer, StringComparison.OrdinalIgnoreCase);
        if (plan.RequiresVideoEncode || plan.RequiresAudioEncode || needsContainerChange)
        {
            return "-avoid_negative_ts make_zero";
        }

        return string.Empty;
    }

    private static string BuildVideoPart(SourceVideo video, TranscodePlan plan)
    {
        if (plan.CopyVideo)
        {
            return UsesStrongSyncRemux(plan)
                ? "-map 0:v:0 -c:v copy -copytb 1"
                : "-map 0:v:0 -c:v copy";
        }

        var options = plan.FfmpegOptions!;
        var frameRatePart = plan.TargetFramesPerSecond.HasValue
            ? $"-fps_mode:v cfr -r {ResolveFrameRateToken(video, plan)} "
            : string.Empty;
        var rateControlPart = BuildVideoRateControlPart(options);
        var aqPart = BuildAqPart(options);
        var pixelFormatPart = string.IsNullOrWhiteSpace(options.PixelFormat)
            ? string.Empty
            : $"-pix_fmt {options.PixelFormat} ";
        var videoFilterPart = string.IsNullOrWhiteSpace(options.VideoFilter)
            ? string.Empty
            : $"-vf {FfmpegExecutionLayout.Quote(options.VideoFilter)} ";
        var compatibilityPart = ResolveVideoCompatibilityPart(video, plan);
        var gop = ResolveGop(video, plan);
        var preset = plan.EncoderPreset ?? "p6";

        if (plan.TargetHeight.HasValue)
        {
            var algorithm = plan.VideoSettings?.Algorithm ?? "bicubic";
            return $"-map 0:v:0 {frameRatePart}-vf \"scale_cuda=-2:{plan.TargetHeight.Value}:interp_algo={algorithm}:format=nv12\" " +
                   $"-c:v h264_nvenc -preset {preset} {rateControlPart}{aqPart}" +
                   $"{compatibilityPart}-g {gop}";
        }

        return $"-map 0:v:0 {frameRatePart}{videoFilterPart}" +
               $"-c:v h264_nvenc -preset {preset} {rateControlPart}{aqPart}" +
               $"{pixelFormatPart}{compatibilityPart}-g {gop}";
    }

    private static string BuildAudioPart(TranscodePlan plan)
    {
        var options = plan.FfmpegOptions!;
        var mapPart = options.MapPrimaryAudioOnly
            ? "-map 0:a:0?"
            : "-map 0:a?";

        return plan.CopyAudio
            ? $"{mapPart} -c:a copy"
            : RequiresAudioRepair(plan)
                ? BuildRepairAudioEncodePart(mapPart, options)
                : BuildAudioEncodePart(mapPart, options);
    }

    private static string BuildRepairAudioEncodePart(string mapPart, FfmpegOptions options)
    {
        var repairedOptions = new FfmpegOptions(
            audioBitrateKbps: options.AudioBitrateKbps ?? 192,
            audioSampleRate: options.AudioSampleRate ?? 48000,
            audioChannels: options.AudioChannels ?? 2,
            audioFilter: BuildRepairAudioFilter(options.AudioFilter));
        return BuildAudioEncodePart(mapPart, repairedOptions);
    }

    private static string BuildAudioEncodePart(string mapPart, FfmpegOptions options)
    {
        var parts = new List<string>
        {
            mapPart,
            "-c:a aac"
        };

        if (options.AudioSampleRate.HasValue)
        {
            parts.Add($"-ar {options.AudioSampleRate.Value}");
        }

        if (options.AudioChannels.HasValue)
        {
            parts.Add($"-ac {options.AudioChannels.Value}");
        }

        parts.Add($"-b:a {(options.AudioBitrateKbps ?? 192)}k");

        if (!string.IsNullOrWhiteSpace(options.AudioFilter))
        {
            parts.Add($"-af {FfmpegExecutionLayout.Quote(options.AudioFilter)}");
        }

        return string.Join(" ", parts);
    }

    private static bool UsesStrongSyncRemux(TranscodePlan plan)
    {
        return plan.CopyVideo && plan.SynchronizeAudio;
    }

    private static bool RequiresAudioRepair(TranscodePlan plan)
    {
        return plan.SynchronizeAudio || plan.FixTimestamps;
    }

    private static bool ShouldUseHardwareDecode(TranscodePlan plan)
    {
        if (plan.FfmpegOptions!.UseHardwareDecode.HasValue)
        {
            return plan.FfmpegOptions.UseHardwareDecode.Value;
        }

        return plan.RequiresVideoEncode &&
               plan.TargetHeight.HasValue &&
               string.Equals(plan.PreferredBackend, "gpu", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresMuxRewrite(TranscodePlan plan)
    {
        var options = plan.FfmpegOptions!;
        return options.OptimizeForFastStart || options.MapPrimaryAudioOnly;
    }

    private static string BuildVideoRateControlPart(FfmpegOptions options)
    {
        if (options.VideoBitrateKbps.HasValue)
        {
            var maxrateKbps = options.VideoMaxrateKbps ?? options.VideoBitrateKbps.Value;
            var bufferSizeKbps = options.VideoBufferSizeKbps ?? (maxrateKbps * 2);
            return $"-rc vbr -b:v {options.VideoBitrateKbps.Value}k -maxrate {maxrateKbps}k -bufsize {bufferSizeKbps}k ";
        }

        if (options.VideoCq.HasValue)
        {
            if (options.VideoMaxrateKbps.HasValue || options.VideoBufferSizeKbps.HasValue)
            {
                var maxrateKbps = options.VideoMaxrateKbps ?? 3000;
                var bufferSizeKbps = options.VideoBufferSizeKbps ?? (maxrateKbps * 2);
                return $"-rc vbr_hq -cq {options.VideoCq.Value} -b:v 0 -maxrate {maxrateKbps}k -bufsize {bufferSizeKbps}k ";
            }

            return $"-rc vbr_hq -cq {options.VideoCq.Value} -b:v 0 ";
        }

        return "-rc vbr_hq -cq 19 -b:v 0 ";
    }

    private static string BuildAqPart(FfmpegOptions options)
    {
        if (options.EnableAdaptiveQuantization != true)
        {
            return string.Empty;
        }

        var parts = new List<string>
        {
            "-spatial_aq 1",
            "-temporal_aq 1",
            $"-rc-lookahead {(options.RcLookahead ?? 32)}"
        };

        if (options.AqStrength.HasValue)
        {
            parts.Insert(2, $"-aq-strength {options.AqStrength.Value}");
        }

        return $"{string.Join(" ", parts)} ";
    }

    private static string BuildRepairAudioFilter(string? existingFilter)
    {
        if (string.IsNullOrWhiteSpace(existingFilter))
        {
            return "aresample=async=1:first_pts=0";
        }

        var normalized = existingFilter.Trim();
        return normalized.Contains("async=1:first_pts=0", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"{normalized},aresample=async=1:first_pts=0";
    }

    private static string ResolveFrameRateToken(SourceVideo video, TranscodePlan plan)
    {
        return (plan.TargetFramesPerSecond ?? video.FramesPerSecond).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static int ResolveGop(SourceVideo video, TranscodePlan plan)
    {
        var fps = plan.TargetFramesPerSecond ?? video.FramesPerSecond;
        return (int)Math.Max(12, Math.Round(fps * 2.0));
    }

    private static string ResolveVideoCompatibilityPart(SourceVideo video, TranscodePlan plan)
    {
        var (width, height) = ResolveOutputDimensions(video, plan);
        var fps = plan.TargetFramesPerSecond ?? video.FramesPerSecond;
        var compatibilityPart = VideoCodecCompatibility.ResolveArguments(
            "h264",
            plan.VideoCompatibilityProfile,
            width,
            height,
            fps);
        return string.IsNullOrWhiteSpace(compatibilityPart)
            ? string.Empty
            : $"{compatibilityPart} ";
    }

    private static (int Width, int Height) ResolveOutputDimensions(SourceVideo video, TranscodePlan plan)
    {
        if (!plan.TargetHeight.HasValue)
        {
            return (video.Width, video.Height);
        }

        if (video.Width <= 0 || video.Height <= 0)
        {
            return (video.Width, video.Height);
        }

        var outputWidth = (int)Math.Round(video.Width * (double)plan.TargetHeight.Value / video.Height);
        return (MakeEven(outputWidth), MakeEven(plan.TargetHeight.Value));
    }

    private static int MakeEven(int value)
    {
        if (value <= 0)
        {
            return value;
        }

        return (value % 2) == 0
            ? value
            : value + 1;
    }
}
