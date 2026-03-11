using System.Globalization;
using MediaTranscodeEngine.Runtime.Downscaling;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Tools.Ffmpeg;
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
    private readonly DownscaleProfiles _downscaleProfiles;
    private readonly DownscaleAutoSampler _autoSampler;
    private readonly FfmpegSampleMeasurer _sampleMeasurer;
    private readonly Func<string, int, DownscaleDefaults, IReadOnlyList<DownscaleSampleWindow>, decimal?> _sampleReductionProvider;
    private readonly ILogger<ToMkvGpuFfmpegTool> _logger;

    /// <summary>
    /// Initializes the mkv-oriented ffmpeg tool.
    /// </summary>
    public ToMkvGpuFfmpegTool(string ffmpegPath, ILogger<ToMkvGpuFfmpegTool> logger)
        : this(ffmpegPath, DownscaleProfiles.Default, null, logger)
    {
    }

    internal ToMkvGpuFfmpegTool(
        string ffmpegPath,
        DownscaleProfiles downscaleProfiles,
        Func<string, int, DownscaleDefaults, IReadOnlyList<DownscaleSampleWindow>, decimal?>? sampleReductionProvider,
        ILogger<ToMkvGpuFfmpegTool> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentNullException.ThrowIfNull(logger);

        _ffmpegPath = ffmpegPath.Trim();
        _downscaleProfiles = downscaleProfiles ?? throw new ArgumentNullException(nameof(downscaleProfiles));
        _autoSampler = new DownscaleAutoSampler(_downscaleProfiles);
        _sampleMeasurer = new FfmpegSampleMeasurer(_ffmpegPath);
        _sampleReductionProvider = sampleReductionProvider ?? _sampleMeasurer.MeasureAverageReduction;
        _logger = logger;
    }

    /// <summary>
    /// Gets the stable tool name.
    /// </summary>
    public string Name => "ffmpeg";

    /// <summary>
    /// Determines whether the mkv-oriented ffmpeg tool can execute the supplied plan.
    /// </summary>
    public bool CanHandle(TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.FfmpegOptions is not null || plan.UseFrameInterpolation)
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
    public ToolExecution BuildExecution(SourceVideo video, TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        if (!CanHandle(plan))
        {
            throw new NotSupportedException("The supplied transcode plan is not supported by ToMkvGpu ffmpeg tool.");
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

        if (plan.RequiresVideoEncode &&
            string.Equals(plan.PreferredBackend, "gpu", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("-hwaccel cuda -hwaccel_output_format cuda");
        }

        parts.Add("-i");
        parts.Add(FfmpegExecutionLayout.Quote(video.FilePath));
        parts.Add(BuildVideoPart(video, plan));
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

    private string BuildVideoPart(SourceVideo video, TranscodePlan plan)
    {
        if (plan.CopyVideo)
        {
            return UsesStrongSyncRemux(plan)
                ? "-map 0:v:0 -c:v copy -copytb 1"
                : "-map 0:v:0 -c:v copy";
        }

        var encoder = ResolveVideoEncoder(plan);
        var settings = ResolveVideoSettings(video, plan);
        var fpsToken = ResolveFrameRateToken(video, plan);
        var gop = ResolveGop(video, plan);
        var compatibilityPart = ResolveVideoCompatibilityPart(video, plan);
        var preset = plan.EncoderPreset ?? "p6";
        var frameRatePart = plan.TargetFramesPerSecond.HasValue
            ? $"-fps_mode:v cfr -r {fpsToken} "
            : string.Empty;
        var aqPart = "-spatial_aq 1 -temporal_aq 1 -rc-lookahead 32 ";
        var pixelFormatPart = string.Equals(plan.PreferredBackend, "gpu", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : "-pix_fmt yuv420p ";
        var rateControlPart = $"-rc vbr_hq -cq {settings.Cq} -b:v 0 -maxrate {FormatRate(settings.Maxrate)} -bufsize {FormatRate(settings.Bufsize)} ";

        if (plan.ApplyOverlayBackground)
        {
            var filter = BuildOverlayFilter(video, plan.TargetHeight, settings.Algorithm);
            return $"-filter_complex {FfmpegExecutionLayout.Quote(filter)} -map \"[v]\" {frameRatePart}" +
                   $"-c:v {encoder} -preset {preset} {rateControlPart}{aqPart}" +
                   $"{pixelFormatPart}{compatibilityPart}-g {gop}";
        }

        if (plan.TargetHeight.HasValue)
        {
            return $"-map 0:v:0 {frameRatePart}-vf \"scale_cuda=-2:{plan.TargetHeight.Value}:interp_algo={settings.Algorithm}:format=nv12\" " +
                   $"-c:v {encoder} -preset {preset} {rateControlPart}{aqPart}" +
                   $"{compatibilityPart}-g {gop}";
        }

        return $"-map 0:v:0 {frameRatePart}" +
               $"-c:v {encoder} -preset {preset} {rateControlPart}{aqPart}" +
               $"{pixelFormatPart}{compatibilityPart}-g {gop}";
    }

    private static string BuildAudioPart(TranscodePlan plan)
    {
        return plan.CopyAudio
            ? "-map 0:a? -c:a copy"
            : RequiresAudioRepair(plan)
                ? "-map 0:a? -c:a aac -ar 48000 -ac 2 -b:a 192k -af \"aresample=async=1:first_pts=0\""
                : "-map 0:a? -c:a aac -ar 48000 -ac 2 -b:a 192k";
    }

    private static bool UsesStrongSyncRemux(TranscodePlan plan)
    {
        return plan.CopyVideo && plan.SynchronizeAudio;
    }

    private static bool RequiresAudioRepair(TranscodePlan plan)
    {
        return plan.SynchronizeAudio || plan.FixTimestamps;
    }

    private static string ResolveVideoEncoder(TranscodePlan plan)
    {
        return plan.TargetVideoCodec switch
        {
            "h264" => "h264_nvenc",
            "h265" => "hevc_nvenc",
            _ => throw new NotSupportedException($"Video codec '{plan.TargetVideoCodec}' is not supported by ToMkvGpu ffmpeg tool.")
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

    private static string ResolveVideoCompatibilityPart(SourceVideo video, TranscodePlan plan)
    {
        var (width, height) = ResolveOutputDimensions(video, plan);
        var fps = plan.TargetFramesPerSecond ?? video.FramesPerSecond;
        var compatibilityPart = VideoCodecCompatibility.ResolveArguments(
            plan.TargetVideoCodec!,
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
        if (plan.ApplyOverlayBackground)
        {
            return ResolveOverlayOutputDimensions(video, plan.TargetHeight);
        }

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

    private static (int Width, int Height) ResolveOverlayOutputDimensions(SourceVideo video, int? targetHeight)
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

        return (MakeEven(outputWidth), MakeEven(outputHeight));
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

    private DownscaleDefaults ResolveVideoSettings(SourceVideo video, TranscodePlan plan)
    {
        if (plan.Downscale is not null)
        {
            var defaults = ResolveBaseDefaults(video, plan);
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
        if (!plan.TargetHeight.HasValue || plan.Downscale is null)
        {
            return defaults;
        }

        if (!_downscaleProfiles.TryGetProfile(plan.TargetHeight.Value, out _))
        {
            return defaults;
        }

        var sourceBitrate = ResolveSourceBitrate(video);
        var resolution = _autoSampler.ResolveWithDiagnostics(
            request: plan.Downscale,
            baseSettings: defaults,
            sourceHeight: video.Height,
            duration: video.Duration,
            sourceBitrate: sourceBitrate.Bitrate,
            hasAudio: video.AudioCodecs.Count > 0,
            accurateReductionProvider: (settings, windows) => _sampleReductionProvider(video.FilePath, plan.TargetHeight.Value, settings, windows));
        LogAutoSampleResolution(video.FilePath, defaults, resolution, sourceBitrate);
        return resolution.Settings;
    }

    private DownscaleDefaults ResolveBaseDefaults(SourceVideo video, TranscodePlan plan)
    {
        if (plan.TargetHeight.HasValue &&
            _downscaleProfiles.TryGetProfile(plan.TargetHeight.Value, out var profile))
        {
            return profile.ResolveDefaults(
                sourceHeight: video.Height,
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
        DownscaleProfile? profile = null;
        var hasProfile = request.TargetHeight.HasValue &&
                         _downscaleProfiles.TryGetProfile(request.TargetHeight.Value, out profile);

        if (!maxrate.HasValue && request.Cq.HasValue && hasProfile && profile is not null)
        {
            var delta = defaults.Cq - cq;
            var resolved = defaults.Maxrate + (delta * profile.RateModel.CqStepToMaxrateStep);
            maxrate = Clamp(resolved, defaults.MaxrateMin, defaults.MaxrateMax);
        }

        maxrate ??= defaults.Maxrate;

        var bufsize = request.Bufsize;
        if (!bufsize.HasValue && (request.Maxrate.HasValue || request.Cq.HasValue))
        {
            var multiplier = hasProfile && profile is not null
                ? profile.RateModel.BufsizeMultiplier
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

    private void LogAutoSampleResolution(
        string inputPath,
        DownscaleDefaults baseSettings,
        DownscaleAutoSampleResolution resolution,
        ResolvedSourceBitrate sourceBitrate)
    {
        _logger.LogInformation(
            "Downscale autosample resolved. InputPath={InputPath} Profile={Profile} Mode={Mode} Path={Path} Reason={Reason} SourceBitrateOrigin={SourceBitrateOrigin} SourceBitrateMbps={SourceBitrateMbps} Corridor={Corridor} Windows={Windows} Iterations={Iterations} LastReductionPercent={LastReductionPercent} InBounds={InBounds} Base={BaseSettings} Resolved={ResolvedSettings}",
            inputPath,
            $"{baseSettings.ContentProfile}/{baseSettings.QualityProfile}",
            resolution.Mode,
            resolution.Path,
            resolution.Reason,
            sourceBitrate.Origin,
            FormatBitrateMbps(sourceBitrate.Bitrate),
            FormatRange(resolution.Range),
            FormatWindows(resolution.Windows),
            resolution.Iterations,
            resolution.LastReduction,
            resolution.InBounds,
            FormatSettings(baseSettings),
            FormatSettings(resolution.Settings));
    }

    private static ResolvedSourceBitrate ResolveSourceBitrate(SourceVideo video)
    {
        if (video.Bitrate.HasValue)
        {
            return new ResolvedSourceBitrate(video.Bitrate.Value, "probe");
        }

        if (video.Duration <= TimeSpan.Zero || string.IsNullOrWhiteSpace(video.FilePath) || !File.Exists(video.FilePath))
        {
            return new ResolvedSourceBitrate(null, "missing");
        }

        var fileSizeBytes = new FileInfo(video.FilePath).Length;
        if (fileSizeBytes <= 0)
        {
            return new ResolvedSourceBitrate(null, "missing");
        }

        var estimatedBitsPerSecond = Math.Round((fileSizeBytes * 8m) / (decimal)video.Duration.TotalSeconds, MidpointRounding.AwayFromZero);
        if (estimatedBitsPerSecond <= 0m || estimatedBitsPerSecond > long.MaxValue)
        {
            return new ResolvedSourceBitrate(null, "missing");
        }

        return new ResolvedSourceBitrate((long)estimatedBitsPerSecond, "file_size_estimate");
    }

    private static string BuildOverlayFilter(SourceVideo video, int? targetHeight, string downscaleAlgorithm)
    {
        var (outputWidth, outputHeight) = ResolveOverlayOutputDimensions(video, targetHeight);

        if (targetHeight.HasValue)
        {
            return "[0:v]split=2[bg0][fg0];" +
                   $"[bg0]scale_cuda={outputWidth}:-2:interp_algo={downscaleAlgorithm}:format=nv12,hwdownload,format=nv12,crop={outputWidth}:{outputHeight},hwupload_cuda[bg];" +
                   $"[fg0]scale_cuda=-2:{outputHeight}:interp_algo={downscaleAlgorithm}:format=nv12[fg];" +
                   "[bg][fg]overlay_cuda=(W-w)/2:0[v]";
        }

        return $"[0:v]scale={outputWidth}:-1,crop={outputWidth}:{outputHeight}[bg];[0:v]scale=-1:{outputHeight}[fg];[bg][fg]overlay=(W-w)/2:0[v]";
    }

    private sealed record ResolvedSourceBitrate(long? Bitrate, string Origin);

    private static string FormatRange(DownscaleRange? range)
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

    private static string FormatWindows(IReadOnlyList<DownscaleSampleWindow> windows)
    {
        return windows.Count == 0
            ? "-"
            : string.Join(",", windows.Select(static window => $"{window.StartSeconds}+{window.DurationSeconds}"));
    }

    private static string FormatSettings(DownscaleDefaults settings)
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
