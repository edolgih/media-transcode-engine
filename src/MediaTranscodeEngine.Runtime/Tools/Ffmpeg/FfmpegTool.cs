using System.Globalization;
using MediaTranscodeEngine.Runtime.Downscaling;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tools.Ffmpeg;

/// <summary>
/// Renders a tool-agnostic transcode plan into an ffmpeg execution recipe for the current runtime model.
/// </summary>
public sealed class FfmpegTool : ITranscodeTool
{
    private readonly string _ffmpegPath;
    private readonly DownscaleProfiles _downscaleProfiles;

    /// <summary>
    /// Initializes an ffmpeg-backed transcode tool.
    /// </summary>
    /// <param name="ffmpegPath">Executable path or command name for ffmpeg.</param>
    public FfmpegTool(string ffmpegPath = "ffmpeg")
        : this(ffmpegPath, DownscaleProfiles.Default)
    {
    }

    internal FfmpegTool(string ffmpegPath, DownscaleProfiles downscaleProfiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        _ffmpegPath = ffmpegPath.Trim();
        _downscaleProfiles = downscaleProfiles ?? throw new ArgumentNullException(nameof(downscaleProfiles));
    }

    /// <summary>
    /// Gets the stable tool name.
    /// </summary>
    public string Name => "ffmpeg";

    /// <summary>
    /// Determines whether the current ffmpeg renderer can execute the supplied plan.
    /// </summary>
    /// <param name="plan">Tool-agnostic transcode plan.</param>
    /// <returns><see langword="true"/> when the plan can be rendered by this tool; otherwise <see langword="false"/>.</returns>
    public bool CanHandle(TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.UseFrameInterpolation)
        {
            return false;
        }

        if (!plan.TargetContainer.Equals("mkv", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (plan.CopyVideo)
        {
            return true;
        }

        return plan.PreferredBackend?.Equals("gpu", StringComparison.OrdinalIgnoreCase) == true &&
               (plan.TargetVideoCodec?.Equals("h264", StringComparison.OrdinalIgnoreCase) == true ||
                plan.TargetVideoCodec?.Equals("h265", StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Builds an ffmpeg execution recipe for the supplied source video and plan.
    /// </summary>
    /// <param name="video">Normalized source video facts.</param>
    /// <param name="plan">Tool-agnostic transcode plan.</param>
    /// <returns>An ffmpeg execution recipe.</returns>
    public ToolExecution BuildExecution(SourceVideo video, TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        if (!CanHandle(plan))
        {
            throw new NotSupportedException("The supplied transcode plan is not supported by ffmpeg tool.");
        }

        if (IsNoOp(video, plan))
        {
            return new ToolExecution(Name, Array.Empty<string>());
        }

        var finalOutputPath = ResolveFinalOutputPath(video, plan);
        var workingOutputPath = ResolveWorkingOutputPath(video, plan, finalOutputPath);
        var ffmpegCommand = BuildFfmpegCommand(video, plan, workingOutputPath);
        var commands = new List<string> { ffmpegCommand };

        AppendPostOperations(commands, video, plan, workingOutputPath, finalOutputPath);

        return new ToolExecution(Name, commands);
    }

    private static bool IsNoOp(SourceVideo video, TranscodePlan plan)
    {
        var finalOutputPath = ResolveFinalOutputPath(video, plan);
        return plan.CopyVideo &&
               plan.CopyAudio &&
               video.Container.Equals(plan.TargetContainer, StringComparison.OrdinalIgnoreCase) &&
               finalOutputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveFinalOutputPath(SourceVideo video, TranscodePlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.OutputPath))
        {
            return plan.OutputPath;
        }

        var directory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}.{plan.TargetContainer}");
    }

    private static string ResolveWorkingOutputPath(SourceVideo video, TranscodePlan plan, string finalOutputPath)
    {
        if (plan.KeepSource)
        {
            return finalOutputPath;
        }

        if (finalOutputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(finalOutputPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = ".";
            }

            return Path.Combine(directory, $"{video.FileNameWithoutExtension}_temp{Path.GetExtension(finalOutputPath)}");
        }

        return finalOutputPath;
    }

    private static void AppendPostOperations(
        List<string> commands,
        SourceVideo video,
        TranscodePlan plan,
        string workingOutputPath,
        string finalOutputPath)
    {
        if (plan.KeepSource)
        {
            return;
        }

        if (finalOutputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            commands.Add($"del {Quote(video.FilePath)}");
            commands.Add($"ren {Quote(workingOutputPath)} {Quote(Path.GetFileName(finalOutputPath))}");
            return;
        }

        commands.Add($"del {Quote(video.FilePath)}");
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

        if (plan.RequiresVideoEncode && plan.TargetHeight.HasValue)
        {
            parts.Add("-hwaccel cuda -hwaccel_output_format cuda");
        }

        parts.Add("-i");
        parts.Add(Quote(video.FilePath));
        parts.Add(BuildVideoPart(video, plan));
        parts.Add(BuildAudioPart(plan));
        parts.Add("-sn");
        parts.Add("-max_muxing_queue_size 4096");
        parts.Add(Quote(outputPath));

        return string.Join(" ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildSanitizePart(SourceVideo video, TranscodePlan plan)
    {
        if (plan.RequiresVideoEncode)
        {
            return "-fflags +genpts+igndts -avoid_negative_ts make_zero";
        }

        var needsContainerChange = !video.Container.Equals(plan.TargetContainer, StringComparison.OrdinalIgnoreCase);
        if (plan.RequiresAudioEncode || needsContainerChange)
        {
            return needsContainerChange || plan.SynchronizeAudio
                ? "-fflags +genpts -avoid_negative_ts make_zero"
                : "-avoid_negative_ts make_zero";
        }

        return string.Empty;
    }

    private string BuildVideoPart(SourceVideo video, TranscodePlan plan)
    {
        if (plan.CopyVideo)
        {
            return "-map 0:v:0 -c:v copy";
        }

        var encoder = ResolveVideoEncoder(plan);
        var settings = ResolveVideoSettings(video, plan);
        var fpsToken = ResolveFrameRateToken(video, plan);
        var gop = ResolveGop(video, plan);
        var aqPart = "-spatial_aq 1 -temporal_aq 1 -rc-lookahead 32";
        var preset = plan.EncoderPreset ?? "p6";

        if (plan.ApplyOverlayBackground)
        {
            var filter = BuildOverlayFilter(video, plan.TargetHeight, settings.Algorithm);
            return $"-filter_complex {Quote(filter)} -map \"[v]\" -fps_mode:v cfr " +
                   $"-c:v {encoder} -preset {preset} -rc vbr_hq -cq {settings.Cq} -b:v 0 -maxrate {FormatRate(settings.Maxrate)} -bufsize {FormatRate(settings.Bufsize)} {aqPart} " +
                   $"-pix_fmt yuv420p -profile:v high -level:v 4.1 -r {fpsToken} -g {gop}";
        }

        if (plan.TargetHeight.HasValue)
        {
            return $"-map 0:v:0 -fps_mode:v cfr -vf \"scale_cuda=-2:{plan.TargetHeight.Value}:interp_algo={settings.Algorithm}:format=nv12\" " +
                   $"-c:v {encoder} -preset {preset} -rc vbr_hq -cq {settings.Cq} -b:v 0 -maxrate {FormatRate(settings.Maxrate)} -bufsize {FormatRate(settings.Bufsize)} {aqPart} " +
                   $"-profile:v high -level:v 4.1 -r {fpsToken} -g {gop}";
        }

        return $"-map 0:v:0 -fps_mode:v cfr " +
               $"-c:v {encoder} -preset {preset} -rc vbr_hq -cq {settings.Cq} -b:v 0 -maxrate {FormatRate(settings.Maxrate)} -bufsize {FormatRate(settings.Bufsize)} {aqPart} " +
               $"-pix_fmt yuv420p -profile:v high -level:v 4.1 -r {fpsToken} -g {gop}";
    }

    private static string BuildAudioPart(TranscodePlan plan)
    {
        return plan.CopyAudio
            ? "-map 0:a? -c:a copy"
            : "-map 0:a? -c:a aac -ar 48000 -ac 2 -b:a 192k -af \"aresample=async=1:first_pts=0\"";
    }

    private static string ResolveVideoEncoder(TranscodePlan plan)
    {
        return plan.TargetVideoCodec switch
        {
            "h264" => "h264_nvenc",
            "h265" => "hevc_nvenc",
            _ => throw new NotSupportedException($"Video codec '{plan.TargetVideoCodec}' is not supported by ffmpeg tool.")
        };
    }

    private static string ResolveFrameRateToken(SourceVideo video, TranscodePlan plan)
    {
        if (plan.TargetFramesPerSecond.HasValue)
        {
            return plan.TargetFramesPerSecond.Value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return video.FramesPerSecond.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static int ResolveGop(SourceVideo video, TranscodePlan plan)
    {
        var fps = plan.TargetFramesPerSecond ?? video.FramesPerSecond;
        return (int)Math.Max(12, Math.Round(fps * 2.0));
    }

    private DownscaleDefaults ResolveVideoSettings(SourceVideo video, TranscodePlan plan)
    {
        if (plan.Downscale is not null)
        {
            var defaults = ResolveBaseDefaults(plan);
            defaults = ResolveAutoSampledDefaults(video, plan, defaults);
            return ApplyOverrides(defaults, plan.Downscale);
        }

        return new DownscaleDefaults(
            ContentProfile: "default",
            QualityProfile: "default",
            Cq: 21,
            Maxrate: 4m,
            Bufsize: 8m,
            Algorithm: "bilinear",
            CqMin: 1,
            CqMax: int.MaxValue,
            MaxrateMin: 0.001m,
            MaxrateMax: decimal.MaxValue);
    }

    private DownscaleDefaults ResolveAutoSampledDefaults(SourceVideo video, TranscodePlan plan, DownscaleDefaults defaults)
    {
        if (plan.TargetHeight != 576 || plan.Downscale is null)
        {
            return defaults;
        }

        var request = plan.Downscale;
        if (request.NoAutoSample || request.Cq.HasValue || request.Maxrate.HasValue || request.Bufsize.HasValue)
        {
            return defaults;
        }

        var profile = _downscaleProfiles.GetRequiredProfile(576);
        var mode = request.AutoSampleMode ?? profile.AutoSampling.ModeDefault;
        if (!mode.Equals("fast", StringComparison.OrdinalIgnoreCase))
        {
            return defaults;
        }

        if (video.Duration <= TimeSpan.Zero || !video.Bitrate.HasValue || video.Bitrate.Value <= 0)
        {
            return defaults;
        }

        var range = profile.ResolveRange(video.Height, request.ContentProfile, request.QualityProfile);
        if (range is null)
        {
            return defaults;
        }

        var cq = defaults.Cq;
        var maxrate = defaults.Maxrate;
        var hasAudio = video.AudioCodecs.Count > 0;

        for (var i = 0; i < profile.AutoSampling.MaxIterations; i++)
        {
            var reduction = EstimateReductionFromBitrate(
                sourceBitrateBps: video.Bitrate.Value,
                maxrateMbps: maxrate,
                hasAudio: hasAudio,
                audioBitrateEstimateMbps: profile.AutoSampling.AudioBitrateEstimateMbps);

            if (range.Contains(reduction))
            {
                break;
            }

            var previousCq = cq;
            var previousMaxrate = maxrate;

            if (IsBelowRange(range, reduction))
            {
                if (cq < defaults.CqMax)
                {
                    cq++;
                }

                maxrate = Math.Max(maxrate - profile.RateModel.CqStepToMaxrateStep, defaults.MaxrateMin);
            }
            else
            {
                if (cq > defaults.CqMin)
                {
                    cq--;
                }

                maxrate = Math.Min(maxrate + profile.RateModel.CqStepToMaxrateStep, defaults.MaxrateMax);
            }

            if (previousCq == cq && previousMaxrate == maxrate)
            {
                break;
            }
        }

        var bufsize = maxrate * profile.RateModel.BufsizeMultiplier;
        return defaults with { Cq = cq, Maxrate = maxrate, Bufsize = bufsize };
    }

    private DownscaleDefaults ResolveBaseDefaults(TranscodePlan plan)
    {
        if (plan.TargetHeight == 576)
        {
            var profile = _downscaleProfiles.GetRequiredProfile(576);
            return profile.ResolveDefaults(
                contentProfile: plan.Downscale?.ContentProfile,
                qualityProfile: plan.Downscale?.QualityProfile);
        }

        return new DownscaleDefaults(
            ContentProfile: "default",
            QualityProfile: "default",
            Cq: 21,
            Maxrate: 4m,
            Bufsize: 8m,
            Algorithm: "bilinear",
            CqMin: 1,
            CqMax: int.MaxValue,
            MaxrateMin: 0.001m,
            MaxrateMax: decimal.MaxValue);
    }

    private DownscaleDefaults ApplyOverrides(DownscaleDefaults defaults, DownscaleRequest request)
    {
        var cq = request.Cq ?? defaults.Cq;
        var maxrate = request.Maxrate;

        if (!maxrate.HasValue && request.Cq.HasValue && request.TargetHeight == 576)
        {
            var profile = _downscaleProfiles.GetRequiredProfile(576);
            var delta = defaults.Cq - cq;
            var resolved = defaults.Maxrate + (delta * profile.RateModel.CqStepToMaxrateStep);
            maxrate = Clamp(resolved, defaults.MaxrateMin, defaults.MaxrateMax);
        }

        maxrate ??= defaults.Maxrate;

        var bufsize = request.Bufsize;
        if (!bufsize.HasValue && (request.Maxrate.HasValue || request.Cq.HasValue))
        {
            var multiplier = request.TargetHeight == 576
                ? _downscaleProfiles.GetRequiredProfile(576).RateModel.BufsizeMultiplier
                : 2.0m;
            bufsize = maxrate.Value * multiplier;
        }

        bufsize ??= defaults.Bufsize;

        return new DownscaleDefaults(
            ContentProfile: defaults.ContentProfile,
            QualityProfile: defaults.QualityProfile,
            Cq: cq,
            Maxrate: maxrate.Value,
            Bufsize: bufsize.Value,
            Algorithm: request.Algorithm ?? defaults.Algorithm,
            CqMin: defaults.CqMin,
            CqMax: defaults.CqMax,
            MaxrateMin: defaults.MaxrateMin,
            MaxrateMax: defaults.MaxrateMax);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static string FormatRate(decimal value)
    {
        return $"{value.ToString("0.###", CultureInfo.InvariantCulture)}M";
    }

    private static decimal EstimateReductionFromBitrate(
        long sourceBitrateBps,
        decimal maxrateMbps,
        bool hasAudio,
        decimal audioBitrateEstimateMbps)
    {
        var sourceMbps = sourceBitrateBps / 1_000_000m;
        if (sourceMbps <= 0m)
        {
            return 0m;
        }

        var targetMbps = maxrateMbps + (hasAudio ? audioBitrateEstimateMbps : 0m);
        var reduction = (1m - (targetMbps / sourceMbps)) * 100m;
        reduction = Math.Round(reduction, 2, MidpointRounding.AwayFromZero);
        return Clamp(reduction, -100m, 100m);
    }

    private static bool IsBelowRange(DownscaleRange range, decimal value)
    {
        if (range.MinInclusive.HasValue)
        {
            return value < range.MinInclusive.Value;
        }

        if (range.MinExclusive.HasValue)
        {
            return value <= range.MinExclusive.Value;
        }

        return false;
    }

    private static string BuildOverlayFilter(SourceVideo video, int? targetHeight, string downscaleAlgorithm)
    {
        var outputWidth = video.Width;
        var outputHeight = video.Height;

        if (outputWidth <= 0 || outputHeight <= 0)
        {
            outputWidth = 1920;
            outputHeight = 1080;
        }

        if (outputWidth < outputHeight)
        {
            (outputWidth, outputHeight) = (outputHeight, outputWidth);
        }

        if (targetHeight.HasValue)
        {
            var ratio = (double)targetHeight.Value / outputHeight;
            outputWidth = (int)Math.Round(outputWidth * ratio);
            outputHeight = targetHeight.Value;
        }

        if ((outputWidth % 2) != 0)
        {
            outputWidth++;
        }

        if ((outputHeight % 2) != 0)
        {
            outputHeight++;
        }

        if (targetHeight.HasValue)
        {
            return "[0:v]split=2[bg0][fg0];" +
                   $"[bg0]scale_cuda={outputWidth}:-2:interp_algo={downscaleAlgorithm}:format=nv12,hwdownload,format=nv12,crop={outputWidth}:{outputHeight},hwupload_cuda[bg];" +
                   $"[fg0]scale_cuda=-2:{outputHeight}:interp_algo={downscaleAlgorithm}:format=nv12[fg];" +
                   "[bg][fg]overlay_cuda=(W-w)/2:0[v]";
        }

        return $"[0:v]scale={outputWidth}:-1,crop={outputWidth}:{outputHeight}[bg];[0:v]scale=-1:{outputHeight}[fg];[bg][fg]overlay=(W-w)/2:0[v]";
    }

    private static string Quote(string value) => $"\"{value}\"";
}
