using System.Globalization;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Scenarios;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Tools.Ffmpeg;
using MediaTranscodeEngine.Runtime.VideoSettings;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/*
Это ffmpeg-адаптер сценария tomkvgpu.
Он рендерит mkv-ориентированный план в конкретные команды ffmpeg и post-steps для файлов.
*/
/// <summary>
/// Renders mkv-oriented transcode plans into ffmpeg execution recipes.
/// </summary>
public sealed class ToMkvGpuFfmpegTool : ITranscodeTool
{
    private readonly string _ffmpegPath;
    private readonly ILogger<ToMkvGpuFfmpegTool> _logger;

    /// <summary>
    /// Initializes the mkv-oriented ffmpeg tool.
    /// </summary>
    public ToMkvGpuFfmpegTool(string ffmpegPath, ILogger<ToMkvGpuFfmpegTool> logger)
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
    /// Determines whether the mkv-oriented ffmpeg tool can execute the supplied plan.
    /// </summary>
    public bool CanHandle(TranscodePlan plan, TranscodeExecutionSpec? executionSpec)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (executionSpec is not null && executionSpec is not ToMkvGpuExecutionSpec)
        {
            return false;
        }

        if (plan.Video is EncodeVideoPlan { UseFrameInterpolation: true })
        {
            return false;
        }

        if (!plan.TargetContainer.Equals("mkv", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (plan.Video is CopyVideoPlan)
        {
            return executionSpec is null;
        }

        return plan.Video is EncodeVideoPlan encodeVideo &&
               executionSpec is ToMkvGpuExecutionSpec &&
               encodeVideo.PreferredBackend?.Equals("gpu", StringComparison.OrdinalIgnoreCase) == true &&
               (encodeVideo.TargetVideoCodec.Equals("h264", StringComparison.OrdinalIgnoreCase) ||
                encodeVideo.TargetVideoCodec.Equals("h265", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds an ffmpeg execution recipe for the supplied source video and plan.
    /// </summary>
    public ToolExecution BuildExecution(SourceVideo video, TranscodePlan plan, TranscodeExecutionSpec? executionSpec)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        if (!CanHandle(plan, executionSpec))
        {
            throw new NotSupportedException("The supplied transcode plan is not supported by ToMkvGpu ffmpeg tool.");
        }

        var mkvExecutionSpec = executionSpec as ToMkvGpuExecutionSpec;
        if (IsNoOp(video, plan))
        {
            return new ToolExecution(Name, Array.Empty<string>());
        }

        if (mkvExecutionSpec is not null)
        {
            LogAutoSampleResolution(
                video.FilePath,
                mkvExecutionSpec.VideoResolution.BaseSettings,
                mkvExecutionSpec.VideoResolution.AutoSample,
                mkvExecutionSpec.SourceBitrate);
        }

        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(video, plan);
        var workingOutputPath = FfmpegExecutionLayout.ResolveWorkingOutputPath(video, plan, finalOutputPath);
        var ffmpegCommand = BuildFfmpegCommand(video, plan, mkvExecutionSpec, workingOutputPath);
        var commands = new List<string> { ffmpegCommand };

        FfmpegExecutionLayout.AppendPostOperations(commands, video, plan, workingOutputPath, finalOutputPath);

        return new ToolExecution(Name, commands);
    }

    private static bool IsNoOp(SourceVideo video, TranscodePlan plan)
    {
        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(video, plan);
        return plan.CopyVideo &&
               plan.CopyAudio &&
               video.Container.Equals(plan.TargetContainer, StringComparison.OrdinalIgnoreCase) &&
               finalOutputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildFfmpegCommand(SourceVideo video, TranscodePlan plan, ToMkvGpuExecutionSpec? executionSpec, string outputPath)
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

        if (plan.Video is EncodeVideoPlan encodeVideo &&
            string.Equals(encodeVideo.PreferredBackend, "gpu", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("-hwaccel cuda -hwaccel_output_format cuda");
        }

        parts.Add("-i");
        parts.Add(FfmpegExecutionLayout.Quote(video.FilePath));
        parts.Add(BuildVideoPart(video, plan, executionSpec));
        parts.Add(BuildAudioPart(plan));
        parts.Add("-sn");
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

    private string BuildVideoPart(SourceVideo video, TranscodePlan plan, ToMkvGpuExecutionSpec? executionSpec)
    {
        if (plan.Video is CopyVideoPlan)
        {
            return UsesStrongSyncRemux(plan)
                ? "-map 0:v:0 -c:v copy -copytb 1"
                : "-map 0:v:0 -c:v copy";
        }

        var encodeVideo = GetRequiredEncodeVideoPlan(plan);
        var mkvExecutionSpec = GetRequiredExecutionSpec(executionSpec);
        var encoder = ResolveVideoEncoder(plan);
        var settings = mkvExecutionSpec.VideoResolution.Settings;
        var fpsToken = ResolveFrameRateToken(video, plan);
        var gop = ResolveGop(video, plan);
        var compatibilityPart = ResolveVideoCompatibilityPart(video, plan);
        var preset = encodeVideo.EncoderPreset ?? "p6";
        var frameRatePart = encodeVideo.TargetFramesPerSecond.HasValue
            ? $"-fps_mode:v cfr -r {fpsToken} "
            : string.Empty;
        var aqPart = "-spatial_aq 1 -temporal_aq 1 -rc-lookahead 32 ";
        var pixelFormatPart = string.Equals(encodeVideo.PreferredBackend, "gpu", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : "-pix_fmt yuv420p ";
        var rateControlPart = $"-rc vbr_hq -cq {settings.Cq} -b:v 0 -maxrate {FormatRate(settings.Maxrate)} -bufsize {FormatRate(settings.Bufsize)} ";
        var downscale = encodeVideo.Downscale;

        if (plan.ApplyOverlayBackground)
        {
            var filter = BuildOverlayFilter(video, downscale?.TargetHeight, settings.Algorithm);
            return $"-filter_complex {FfmpegExecutionLayout.Quote(filter)} -map \"[v]\" {frameRatePart}" +
                   $"-c:v {encoder} -preset {preset} {rateControlPart}{aqPart}" +
                   $"{pixelFormatPart}{compatibilityPart}-g {gop}";
        }

        if (downscale is not null)
        {
            return $"-map 0:v:0 {frameRatePart}-vf \"scale_cuda=-2:{downscale.TargetHeight}:interp_algo={settings.Algorithm}:format=nv12\" " +
                   $"-c:v {encoder} -preset {preset} {rateControlPart}{aqPart}" +
                   $"{compatibilityPart}-g {gop}";
        }

        return $"-map 0:v:0 {frameRatePart}" +
               $"-c:v {encoder} -preset {preset} {rateControlPart}{aqPart}" +
               $"{pixelFormatPart}{compatibilityPart}-g {gop}";
    }

    private static string BuildAudioPart(TranscodePlan plan)
    {
        return plan.Audio switch
        {
            CopyAudioPlan => "-map 0:a? -c:a copy",
            SynchronizeAudioPlan => "-map 0:a? -c:a aac -ar 48000 -ac 2 -b:a 192k -af \"aresample=async=1:first_pts=0\"",
            RepairAudioPlan => "-map 0:a? -c:a aac -ar 48000 -ac 2 -b:a 192k -af \"aresample=async=1:first_pts=0\"",
            EncodeAudioPlan => "-map 0:a? -c:a aac -ar 48000 -ac 2 -b:a 192k",
            _ => throw new InvalidOperationException("Unsupported audio plan type.")
        };
    }

    private static bool UsesStrongSyncRemux(TranscodePlan plan)
    {
        return plan.Video is CopyVideoPlan &&
               plan.Audio is SynchronizeAudioPlan;
    }

    private static string ResolveVideoEncoder(TranscodePlan plan)
    {
        var encodeVideo = GetRequiredEncodeVideoPlan(plan);
        return encodeVideo.TargetVideoCodec switch
        {
            "h264" => "h264_nvenc",
            "h265" => "hevc_nvenc",
            _ => throw new NotSupportedException($"Video codec '{encodeVideo.TargetVideoCodec}' is not supported by ToMkvGpu ffmpeg tool.")
        };
    }

    private static string ResolveFrameRateToken(SourceVideo video, TranscodePlan plan)
    {
        var encodeVideo = GetRequiredEncodeVideoPlan(plan);
        if (encodeVideo.TargetFramesPerSecond.HasValue)
        {
            return encodeVideo.TargetFramesPerSecond.Value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return video.FramesPerSecond.ToString("0.###", CultureInfo.InvariantCulture);
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
        var (width, height) = ToMkvGpuVideoGeometry.ResolveOutputDimensions(video, plan);
        var fps = encodeVideo.TargetFramesPerSecond ?? video.FramesPerSecond;
        var compatibilityPart = VideoCodecCompatibility.ResolveArguments(
            encodeVideo.TargetVideoCodec,
            encodeVideo.CompatibilityProfile,
            width,
            height,
            fps);
        return string.IsNullOrWhiteSpace(compatibilityPart)
            ? string.Empty
            : $"{compatibilityPart} ";
    }

    private static EncodeVideoPlan GetRequiredEncodeVideoPlan(TranscodePlan plan)
    {
        return plan.Video as EncodeVideoPlan
            ?? throw new InvalidOperationException("Video encode plan is required for this operation.");
    }

    private static ToMkvGpuExecutionSpec GetRequiredExecutionSpec(ToMkvGpuExecutionSpec? executionSpec)
    {
        return executionSpec is not null
            ? executionSpec
            : throw new InvalidOperationException("ToMkvGpu video encode execution spec is required for this operation.");
    }

    private static string FormatRate(decimal value)
    {
        return $"{value.ToString("0.###", CultureInfo.InvariantCulture)}M";
    }

    private void LogAutoSampleResolution(
        string inputPath,
        VideoSettingsDefaults baseSettings,
        VideoSettingsAutoSampleResolution resolution,
        ToMkvGpuResolvedSourceBitrate sourceBitrate)
    {
        _logger.LogInformation(
            "Video settings autosample resolved. InputPath={InputPath} Profile={Profile} Mode={Mode} Path={Path} Reason={Reason} SourceBitrateOrigin={SourceBitrateOrigin} SourceBitrateMbps={SourceBitrateMbps} Corridor={Corridor} Windows={Windows} Iterations={Iterations} LastReductionPercent={LastReductionPercent} InBounds={InBounds} Base={BaseSettings} Resolved={ResolvedSettings}",
            inputPath,
            $"{baseSettings.ContentProfile}/{baseSettings.QualityProfile}",
            resolution.Mode,
            resolution.Path,
            resolution.Reason,
            sourceBitrate.Origin,
            FormatBitrateMbps(sourceBitrate.Bitrate),
            FormatRange(resolution.Corridor),
            FormatWindows(resolution.Windows),
            resolution.IterationCount,
            resolution.LastReductionPercent,
            resolution.InBounds,
            FormatSettings(baseSettings),
            FormatSettings(resolution.Settings));
    }

    private static string BuildOverlayFilter(SourceVideo video, int? targetHeight, string downscaleAlgorithm)
    {
        var (outputWidth, outputHeight) = ToMkvGpuVideoGeometry.ResolveOverlayOutputDimensions(video, targetHeight);

        if (targetHeight.HasValue)
        {
            return "[0:v]split=2[bg0][fg0];" +
                   $"[bg0]scale_cuda={outputWidth}:-2:interp_algo={downscaleAlgorithm}:format=nv12,hwdownload,format=nv12,crop={outputWidth}:{outputHeight},hwupload_cuda[bg];" +
                   $"[fg0]scale_cuda=-2:{outputHeight}:interp_algo={downscaleAlgorithm}:format=nv12[fg];" +
                   "[bg][fg]overlay_cuda=(W-w)/2:0[v]";
        }

        return $"[0:v]scale={outputWidth}:-1,crop={outputWidth}:{outputHeight}[bg];[0:v]scale=-1:{outputHeight}[fg];[bg][fg]overlay=(W-w)/2:0[v]";
    }

    private static string FormatRange(VideoSettingsRange? range)
    {
        if (range is null)
        {
            return "-";
        }

        var min = range.MinInclusive.HasValue
            ? $">={range.MinInclusive.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
            : range.MinExclusive.HasValue
                ? $">{range.MinExclusive.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
                : "-inf";
        var max = range.MaxInclusive.HasValue
            ? $"<={range.MaxInclusive.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
            : range.MaxExclusive.HasValue
                ? $"<{range.MaxExclusive.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
                : "+inf";
        return $"{min}..{max}";
    }

    private static string FormatWindows(IReadOnlyList<VideoSettingsSampleWindow> windows)
    {
        return windows.Count == 0
            ? "-"
            : string.Join(",", windows.Select(static window => $"{window.StartSeconds}+{window.DurationSeconds}"));
    }

    private static string FormatSettings(VideoSettingsDefaults settings)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{settings.ContentProfile}/{settings.QualityProfile} cq{settings.Cq} max{settings.Maxrate:0.###} buf{settings.Bufsize:0.###}");
    }

    private static string? FormatBitrateMbps(long? bitrate)
    {
        if (!bitrate.HasValue || bitrate.Value <= 0)
        {
            return null;
        }

        var value = bitrate.Value / 1_000_000m;
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
