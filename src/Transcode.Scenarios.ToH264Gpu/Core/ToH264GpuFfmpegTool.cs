using System.Globalization;
using Microsoft.Extensions.Logging;
using Transcode.Core.MediaIntent;
using Transcode.Core.Scenarios;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.Videos;

namespace Transcode.Scenarios.ToH264Gpu.Core;

/*
Это ffmpeg-адаптер сценария toh264gpu.
Он рендерит web/stream/local-ориентированный план в ffmpeg-команду с remux или H.264 NVENC encode path.
*/
/// <summary>
/// Renders web/stream-oriented toh264gpu plans into ffmpeg execution recipes.
/// </summary>
public sealed class ToH264GpuFfmpegTool
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
    /// Determines whether the toh264gpu-specific ffmpeg tool can execute the supplied decision.
    /// </summary>
    internal bool CanHandle(ToH264GpuDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.Video is EncodeVideoIntent { UseFrameInterpolation: true })
        {
            return false;
        }

        if (!decision.TargetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase) &&
            !decision.TargetContainer.Equals("mkv", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (decision.RequiresVideoEncode &&
            decision.VideoExecutionDetails is null)
        {
            return false;
        }

        if (decision.RequiresAudioEncode &&
            decision.AudioExecutionDetails is null)
        {
            return false;
        }

        if (decision.Video is CopyVideoIntent)
        {
            return true;
        }

        return decision.Video is EncodeVideoIntent encodeVideo &&
               encodeVideo.PreferredBackend?.Equals("gpu", StringComparison.OrdinalIgnoreCase) == true &&
               encodeVideo.TargetVideoCodec.Equals("h264", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds an ffmpeg execution recipe for the supplied source video and decision.
    /// </summary>
    internal ScenarioExecution BuildExecution(SourceVideo video, ToH264GpuDecision decision)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(decision);

        if (!CanHandle(decision))
        {
            throw new NotSupportedException("The supplied transcode decision is not supported by ToH264Gpu ffmpeg tool.");
        }

        if (IsNoOp(video, decision))
        {
            return new ScenarioExecution(Array.Empty<string>());
        }

        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(decision.OutputPath);
        var workingOutputPath = FfmpegExecutionLayout.ResolveWorkingOutputPath(video.FilePath, video.FileNameWithoutExtension, decision.KeepSource, finalOutputPath);
        var ffmpegCommand = BuildFfmpegCommand(video, decision, workingOutputPath);
        var commands = new List<string> { ffmpegCommand };

        FfmpegExecutionLayout.AppendPostOperations(commands, video.FilePath, decision.KeepSource, workingOutputPath, finalOutputPath);

        return new ScenarioExecution(commands);
    }

    private static bool IsNoOp(SourceVideo video, ToH264GpuDecision decision)
    {
        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(decision.OutputPath);
        return decision.CopyVideo &&
               decision.CopyAudio &&
               !RequiresMuxRewrite(decision.Mux) &&
               video.Container.Equals(decision.TargetContainer, StringComparison.OrdinalIgnoreCase) &&
               finalOutputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildFfmpegCommand(SourceVideo video, ToH264GpuDecision decision, string outputPath)
    {
        var parts = new List<string>
        {
            _ffmpegPath,
            "-hide_banner"
        };

        var sanitizePart = BuildSanitizePart(video, decision);
        if (!string.IsNullOrWhiteSpace(sanitizePart))
        {
            parts.Add(sanitizePart);
        }

        if (decision.VideoExecutionDetails?.UseHardwareDecode == true)
        {
            parts.Add("-hwaccel cuda -hwaccel_output_format cuda");
        }

        parts.Add("-i");
        parts.Add(FfmpegExecutionLayout.Quote(video.FilePath));
        parts.Add(BuildVideoPart(video, decision));
        parts.Add(BuildAudioPart(decision));
        parts.Add("-sn");

        if (decision.TargetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase) &&
            decision.OptimizeForFastStart)
        {
            parts.Add("-movflags +faststart");
        }

        parts.Add("-max_muxing_queue_size 4096");
        parts.Add(FfmpegExecutionLayout.Quote(outputPath));

        return string.Join(" ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildSanitizePart(SourceVideo video, ToH264GpuDecision decision)
    {
        if (decision.FixTimestamps || UsesStrongSyncRemux(decision))
        {
            return "-fflags +genpts+igndts -avoid_negative_ts make_zero";
        }

        var needsContainerChange = !video.Container.Equals(decision.TargetContainer, StringComparison.OrdinalIgnoreCase);
        if (decision.RequiresVideoEncode || decision.RequiresAudioEncode || needsContainerChange)
        {
            return "-avoid_negative_ts make_zero";
        }

        return string.Empty;
    }

    private static string BuildVideoPart(SourceVideo video, ToH264GpuDecision decision)
    {
        if (decision.Video is CopyVideoIntent)
        {
            return UsesStrongSyncRemux(decision)
                ? "-map 0:v:0 -c:v copy -copytb 1"
                : "-map 0:v:0 -c:v copy";
        }

        var encodeVideo = GetRequiredEncodeVideoPlan(decision);
        var execution = GetRequiredVideoExecution(decision);
        var frameRatePart = encodeVideo.TargetFramesPerSecond.HasValue
            ? $"-fps_mode:v cfr -r {ResolveFrameRateToken(video, decision)} "
            : string.Empty;
        var rateControlPart = BuildVideoRateControlPart(execution.RateControl);
        var aqPart = BuildAqPart(execution.AdaptiveQuantization);
        var pixelFormatPart = string.IsNullOrWhiteSpace(execution.PixelFormat)
            ? string.Empty
            : $"-pix_fmt {execution.PixelFormat} ";
        var videoFilterPart = string.IsNullOrWhiteSpace(execution.Filter)
            ? string.Empty
            : $"-vf {FfmpegExecutionLayout.Quote(execution.Filter)} ";
        var compatibilityPart = ResolveVideoCompatibilityPart(video, decision);
        var gop = ResolveGop(video, decision);
        var preset = encodeVideo.EncoderPreset
                     ?? throw new InvalidOperationException("Encoder preset must be resolved before tool rendering.");
        var downscale = encodeVideo.Downscale;

        if (downscale is not null)
        {
            var algorithm = downscale.Algorithm
                            ?? throw new InvalidOperationException("Downscale algorithm must be resolved before tool rendering.");
            return $"-map 0:v:0 {frameRatePart}-vf \"scale_cuda=-2:{downscale.TargetHeight}:interp_algo={algorithm}:format=nv12\" " +
                   $"-c:v h264_nvenc -preset {preset} {rateControlPart}{aqPart}" +
                   $"{compatibilityPart}-g {gop}";
        }

        return $"-map 0:v:0 {frameRatePart}{videoFilterPart}" +
               $"-c:v h264_nvenc -preset {preset} {rateControlPart}{aqPart}" +
               $"{pixelFormatPart}{compatibilityPart}-g {gop}";
    }

    private static string BuildAudioPart(ToH264GpuDecision decision)
    {
        var mapPart = decision.Mux.MapPrimaryAudioOnly
            ? "-map 0:a:0?"
            : "-map 0:a?";

        return decision.Audio switch
        {
            CopyAudioIntent => $"{mapPart} -c:a copy",
            SynchronizeAudioIntent => BuildAudioEncodePart(mapPart, GetRequiredAudioExecution(decision)),
            RepairAudioIntent => BuildAudioEncodePart(mapPart, GetRequiredAudioExecution(decision)),
            EncodeAudioIntent => BuildAudioEncodePart(mapPart, GetRequiredAudioExecution(decision)),
            _ => throw new InvalidOperationException("Unsupported audio plan type.")
        };
    }

    private static string BuildAudioEncodePart(string mapPart, ToH264GpuDecision.AudioExecution options)
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

    private static bool UsesStrongSyncRemux(ToH264GpuDecision decision)
    {
        return decision.Video is CopyVideoIntent &&
               decision.Audio is SynchronizeAudioIntent;
    }

    private static bool RequiresMuxRewrite(ToH264GpuDecision.MuxExecution options)
    {
        return options.OptimizeForFastStart || options.MapPrimaryAudioOnly;
    }

    private static string BuildVideoRateControlPart(ToH264GpuDecision.VideoRateControlExecution options)
    {
        return options switch
        {
            ToH264GpuDecision.VariableBitrateVideoRateControlExecution rateControl =>
                $"-rc vbr -b:v {rateControl.BitrateKbps}k -maxrate {rateControl.MaxrateKbps}k -bufsize {rateControl.BufferSizeKbps}k ",
            ToH264GpuDecision.ConstantQualityVideoRateControlExecution rateControl when rateControl.MaxrateKbps.HasValue =>
                $"-rc vbr_hq -cq {rateControl.Cq} -b:v 0 -maxrate {rateControl.MaxrateKbps.Value}k -bufsize {rateControl.BufferSizeKbps!.Value}k ",
            ToH264GpuDecision.ConstantQualityVideoRateControlExecution rateControl =>
                $"-rc vbr_hq -cq {rateControl.Cq} -b:v 0 ",
            _ => throw new InvalidOperationException("Unsupported video rate-control type.")
        };
    }

    private static string BuildAqPart(ToH264GpuDecision.AdaptiveQuantizationExecution? options)
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

    private static string ResolveFrameRateToken(SourceVideo video, ToH264GpuDecision decision)
    {
        var encodeVideo = GetRequiredEncodeVideoPlan(decision);
        return (encodeVideo.TargetFramesPerSecond ?? video.FramesPerSecond).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static int ResolveGop(SourceVideo video, ToH264GpuDecision decision)
    {
        var encodeVideo = GetRequiredEncodeVideoPlan(decision);
        var fps = encodeVideo.TargetFramesPerSecond ?? video.FramesPerSecond;
        return (int)Math.Max(12, Math.Round(fps * 2.0));
    }

    private static string ResolveVideoCompatibilityPart(SourceVideo video, ToH264GpuDecision decision)
    {
        var encodeVideo = GetRequiredEncodeVideoPlan(decision);
        var (width, height) = ResolveOutputDimensions(video, decision);
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

    private static (int Width, int Height) ResolveOutputDimensions(SourceVideo video, ToH264GpuDecision decision)
    {
        var downscale = decision.Video is EncodeVideoIntent { Downscale: { } explicitDownscale }
            ? explicitDownscale
            : null;
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

    private static EncodeVideoIntent GetRequiredEncodeVideoPlan(ToH264GpuDecision decision)
    {
        return decision.Video as EncodeVideoIntent
            ?? throw new InvalidOperationException("Video encode plan is required for this operation.");
    }

    private static ToH264GpuDecision.VideoExecution GetRequiredVideoExecution(ToH264GpuDecision decision)
    {
        return decision.VideoExecutionDetails
            ?? throw new InvalidOperationException("Video execution spec is required for this operation.");
    }

    private static ToH264GpuDecision.AudioExecution GetRequiredAudioExecution(ToH264GpuDecision decision)
    {
        return decision.AudioExecutionDetails
            ?? throw new InvalidOperationException("Audio execution spec is required for this operation.");
    }

}
