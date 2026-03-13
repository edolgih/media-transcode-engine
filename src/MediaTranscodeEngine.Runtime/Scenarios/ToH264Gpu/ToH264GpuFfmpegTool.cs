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
    public bool CanHandle(TranscodePlan plan, TranscodeExecutionSpec? executionSpec)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (executionSpec is not ToH264GpuExecutionSpec spec)
        {
            return false;
        }

        if (plan.UseFrameInterpolation)
        {
            return false;
        }

        if (!plan.TargetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase) &&
            !plan.TargetContainer.Equals("mkv", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (plan.RequiresVideoEncode &&
            spec.Video is null)
        {
            return false;
        }

        if (plan.RequiresAudioEncode &&
            spec.Audio is null)
        {
            return false;
        }

        if (plan.Video is CopyVideoPlan)
        {
            return true;
        }

        var encodeVideo = plan.EncodeVideo;
        return encodeVideo?.PreferredBackend?.Equals("gpu", StringComparison.OrdinalIgnoreCase) == true &&
               encodeVideo.TargetVideoCodec.Equals("h264", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds an ffmpeg execution recipe for the supplied source video and plan.
    /// </summary>
    public ToolExecution BuildExecution(SourceVideo video, TranscodePlan plan, TranscodeExecutionSpec? executionSpec)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        var spec = executionSpec as ToH264GpuExecutionSpec
            ?? throw new NotSupportedException("The supplied transcode plan is not supported by ToH264Gpu ffmpeg tool.");

        if (!CanHandle(plan, spec))
        {
            throw new NotSupportedException("The supplied transcode plan is not supported by ToH264Gpu ffmpeg tool.");
        }

        if (IsNoOp(video, plan, spec))
        {
            return new ToolExecution(Name, Array.Empty<string>());
        }

        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(video, plan);
        var workingOutputPath = FfmpegExecutionLayout.ResolveWorkingOutputPath(video, plan, finalOutputPath);
        var ffmpegCommand = BuildFfmpegCommand(video, plan, spec, workingOutputPath);
        var commands = new List<string> { ffmpegCommand };

        FfmpegExecutionLayout.AppendPostOperations(commands, video, plan, workingOutputPath, finalOutputPath);

        return new ToolExecution(Name, commands);
    }

    private static bool IsNoOp(SourceVideo video, TranscodePlan plan, ToH264GpuExecutionSpec spec)
    {
        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(video, plan);
        return plan.CopyVideo &&
               plan.CopyAudio &&
               !RequiresMuxRewrite(spec.Mux) &&
               video.Container.Equals(plan.TargetContainer, StringComparison.OrdinalIgnoreCase) &&
               finalOutputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildFfmpegCommand(SourceVideo video, TranscodePlan plan, ToH264GpuExecutionSpec spec, string outputPath)
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

        if (spec.Video?.UseHardwareDecode == true)
        {
            parts.Add("-hwaccel cuda -hwaccel_output_format cuda");
        }

        parts.Add("-i");
        parts.Add(FfmpegExecutionLayout.Quote(video.FilePath));
        parts.Add(BuildVideoPart(video, plan, spec));
        parts.Add(BuildAudioPart(plan, spec));
        parts.Add("-sn");

        if (plan.TargetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase) &&
            spec.OptimizeForFastStart)
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

    private static string BuildVideoPart(SourceVideo video, TranscodePlan plan, ToH264GpuExecutionSpec options)
    {
        if (plan.Video is CopyVideoPlan)
        {
            return UsesStrongSyncRemux(plan)
                ? "-map 0:v:0 -c:v copy -copytb 1"
                : "-map 0:v:0 -c:v copy";
        }

        var encodeVideo = GetRequiredEncodeVideoPlan(plan);
        var execution = GetRequiredVideoExecution(options);
        var frameRatePart = encodeVideo.TargetFramesPerSecond.HasValue
            ? $"-fps_mode:v cfr -r {ResolveFrameRateToken(video, plan)} "
            : string.Empty;
        var rateControlPart = BuildVideoRateControlPart(execution.RateControl);
        var aqPart = BuildAqPart(execution.AdaptiveQuantization);
        var pixelFormatPart = string.IsNullOrWhiteSpace(execution.PixelFormat)
            ? string.Empty
            : $"-pix_fmt {execution.PixelFormat} ";
        var videoFilterPart = string.IsNullOrWhiteSpace(execution.Filter)
            ? string.Empty
            : $"-vf {FfmpegExecutionLayout.Quote(execution.Filter)} ";
        var compatibilityPart = ResolveVideoCompatibilityPart(video, plan);
        var gop = ResolveGop(video, plan);
        var preset = encodeVideo.EncoderPreset ?? "p6";
        var downscale = encodeVideo.Downscale;

        if (downscale is not null)
        {
            var algorithm = downscale.Algorithm ?? "bicubic";
            return $"-map 0:v:0 {frameRatePart}-vf \"scale_cuda=-2:{downscale.TargetHeight}:interp_algo={algorithm}:format=nv12\" " +
                   $"-c:v h264_nvenc -preset {preset} {rateControlPart}{aqPart}" +
                   $"{compatibilityPart}-g {gop}";
        }

        return $"-map 0:v:0 {frameRatePart}{videoFilterPart}" +
               $"-c:v h264_nvenc -preset {preset} {rateControlPart}{aqPart}" +
               $"{pixelFormatPart}{compatibilityPart}-g {gop}";
    }

    private static string BuildAudioPart(TranscodePlan plan, ToH264GpuExecutionSpec options)
    {
        var mapPart = options.Mux.MapPrimaryAudioOnly
            ? "-map 0:a:0?"
            : "-map 0:a?";

        return plan.Audio switch
        {
            CopyAudioPlan => $"{mapPart} -c:a copy",
            SynchronizeAudioPlan => BuildAudioEncodePart(mapPart, GetRequiredAudioExecution(options)),
            RepairAudioPlan => BuildAudioEncodePart(mapPart, GetRequiredAudioExecution(options)),
            EncodeAudioPlan => BuildAudioEncodePart(mapPart, GetRequiredAudioExecution(options)),
            _ => throw new InvalidOperationException("Unsupported audio plan type.")
        };
    }

    private static string BuildAudioEncodePart(string mapPart, ToH264GpuExecutionSpec.AudioExecution options)
    {
        var parts = new List<string>
        {
            mapPart,
            "-c:a aac"
        };

        if (options.SampleRate.HasValue)
        {
            parts.Add($"-ar {options.SampleRate.Value}");
        }

        if (options.Channels.HasValue)
        {
            parts.Add($"-ac {options.Channels.Value}");
        }

        parts.Add($"-b:a {options.BitrateKbps}k");

        if (!string.IsNullOrWhiteSpace(options.Filter))
        {
            parts.Add($"-af {FfmpegExecutionLayout.Quote(options.Filter)}");
        }

        return string.Join(" ", parts);
    }

    private static bool UsesStrongSyncRemux(TranscodePlan plan)
    {
        return plan.Video is CopyVideoPlan &&
               plan.Audio is SynchronizeAudioPlan;
    }

    private static bool RequiresMuxRewrite(ToH264GpuExecutionSpec.MuxExecution options)
    {
        return options.OptimizeForFastStart || options.MapPrimaryAudioOnly;
    }

    private static string BuildVideoRateControlPart(ToH264GpuExecutionSpec.VideoRateControlExecution options)
    {
        return options switch
        {
            ToH264GpuExecutionSpec.VariableBitrateVideoRateControlExecution rateControl =>
                $"-rc vbr -b:v {rateControl.BitrateKbps}k -maxrate {rateControl.MaxrateKbps}k -bufsize {rateControl.BufferSizeKbps}k ",
            ToH264GpuExecutionSpec.ConstantQualityVideoRateControlExecution rateControl when rateControl.MaxrateKbps.HasValue =>
                $"-rc vbr_hq -cq {rateControl.Cq} -b:v 0 -maxrate {rateControl.MaxrateKbps.Value}k -bufsize {rateControl.BufferSizeKbps!.Value}k ",
            ToH264GpuExecutionSpec.ConstantQualityVideoRateControlExecution rateControl =>
                $"-rc vbr_hq -cq {rateControl.Cq} -b:v 0 ",
            _ => throw new InvalidOperationException("Unsupported video rate-control type.")
        };
    }

    private static string BuildAqPart(ToH264GpuExecutionSpec.AdaptiveQuantizationExecution? options)
    {
        if (options is null)
        {
            return string.Empty;
        }

        var parts = new List<string>
        {
            "-spatial_aq 1",
            "-temporal_aq 1",
            $"-rc-lookahead {options.RcLookahead}"
        };

        if (options.Strength.HasValue)
        {
            parts.Insert(2, $"-aq-strength {options.Strength.Value}");
        }

        return $"{string.Join(" ", parts)} ";
    }

    private static string ResolveFrameRateToken(SourceVideo video, TranscodePlan plan)
    {
        var encodeVideo = GetRequiredEncodeVideoPlan(plan);
        return (encodeVideo.TargetFramesPerSecond ?? video.FramesPerSecond).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static int ResolveGop(SourceVideo video, TranscodePlan plan)
    {
        var encodeVideo = GetRequiredEncodeVideoPlan(plan);
        var fps = encodeVideo.TargetFramesPerSecond ?? video.FramesPerSecond;
        return (int)Math.Max(12, Math.Round(fps * 2.0));
    }

    private static string ResolveVideoCompatibilityPart(SourceVideo video, TranscodePlan plan)
    {
        var encodeVideo = GetRequiredEncodeVideoPlan(plan);
        var (width, height) = ResolveOutputDimensions(video, plan);
        var fps = encodeVideo.TargetFramesPerSecond ?? video.FramesPerSecond;
        var compatibilityPart = VideoCodecCompatibility.ResolveArguments(
            "h264",
            encodeVideo.CompatibilityProfile,
            width,
            height,
            fps);
        return string.IsNullOrWhiteSpace(compatibilityPart)
            ? string.Empty
            : $"{compatibilityPart} ";
    }

    private static (int Width, int Height) ResolveOutputDimensions(SourceVideo video, TranscodePlan plan)
    {
        var downscale = plan.EncodeVideo?.Downscale;
        if (downscale is null)
        {
            return (video.Width, video.Height);
        }

        if (video.Width <= 0 || video.Height <= 0)
        {
            return (video.Width, video.Height);
        }

        var outputWidth = (int)Math.Round(video.Width * (double)downscale.TargetHeight / video.Height);
        return (MakeEven(outputWidth), MakeEven(downscale.TargetHeight));
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

    private static EncodeVideoPlan GetRequiredEncodeVideoPlan(TranscodePlan plan)
    {
        return plan.EncodeVideo
            ?? throw new InvalidOperationException("Video encode plan is required for this operation.");
    }

    private static ToH264GpuExecutionSpec.VideoExecution GetRequiredVideoExecution(ToH264GpuExecutionSpec spec)
    {
        return spec.Video
            ?? throw new InvalidOperationException("Video execution spec is required for this operation.");
    }

    private static ToH264GpuExecutionSpec.AudioExecution GetRequiredAudioExecution(ToH264GpuExecutionSpec spec)
    {
        return spec.Audio
            ?? throw new InvalidOperationException("Audio execution spec is required for this operation.");
    }
}
