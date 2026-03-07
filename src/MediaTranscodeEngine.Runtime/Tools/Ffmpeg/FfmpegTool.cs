using System.Diagnostics;
using System.Globalization;
using MediaTranscodeEngine.Runtime.Downscaling;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Runtime.Tools.Ffmpeg;

/// <summary>
/// Renders a tool-agnostic transcode plan into an ffmpeg execution recipe for the current runtime model.
/// </summary>
public sealed class FfmpegTool : ITranscodeTool
{
    private readonly string _ffmpegPath;
    private readonly DownscaleProfiles _downscaleProfiles;
    private readonly DownscaleAutoSampler _autoSampler;
    private readonly Func<string, DownscaleDefaults, IReadOnlyList<DownscaleSampleWindow>, decimal?> _sampleReductionProvider;
    private readonly ILogger<FfmpegTool> _logger;

    /// <summary>
    /// Initializes an ffmpeg-backed transcode tool.
    /// </summary>
    /// <param name="ffmpegPath">Executable path or command name for ffmpeg.</param>
    /// <param name="logger">Application logger used for execution diagnostics.</param>
    public FfmpegTool(string ffmpegPath, ILogger<FfmpegTool> logger)
        : this(ffmpegPath, DownscaleProfiles.Default, null, logger)
    {
    }

    internal FfmpegTool(
        string ffmpegPath,
        DownscaleProfiles downscaleProfiles,
        Func<string, DownscaleDefaults, IReadOnlyList<DownscaleSampleWindow>, decimal?>? sampleReductionProvider,
        ILogger<FfmpegTool> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentNullException.ThrowIfNull(logger);
        _ffmpegPath = ffmpegPath.Trim();
        _downscaleProfiles = downscaleProfiles ?? throw new ArgumentNullException(nameof(downscaleProfiles));
        _autoSampler = new DownscaleAutoSampler(_downscaleProfiles);
        _sampleReductionProvider = sampleReductionProvider ?? MeasureSampleAverageReduction;
        _logger = logger;
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
        if (plan.TargetHeight != 576 || plan.Downscale is null)
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
            accurateReductionProvider: (settings, windows) => _sampleReductionProvider(video.FilePath, settings, windows));
        LogAutoSampleResolution(video.FilePath, defaults, resolution, sourceBitrate);
        return resolution.Settings;
    }

    private DownscaleDefaults ResolveBaseDefaults(SourceVideo video, TranscodePlan plan)
    {
        if (plan.TargetHeight == 576)
        {
            var profile = _downscaleProfiles.GetRequiredProfile(576);
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

    private decimal? MeasureSampleAverageReduction(
        string inputPath,
        DownscaleDefaults settings,
        IReadOnlyList<DownscaleSampleWindow> windows)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || windows.Count == 0)
        {
            return null;
        }

        var reductions = new List<decimal>();
        foreach (var window in windows)
        {
            var sourceSample = CreateSourceSample(inputPath, window);
            if (sourceSample is null)
            {
                continue;
            }

            try
            {
                var encodedSize = EncodeSample(sourceSample.Path, settings);
                if (!encodedSize.HasValue || encodedSize.Value <= 0 || sourceSample.SizeBytes <= 0)
                {
                    continue;
                }

                var reduction = (1m - (encodedSize.Value / sourceSample.SizeBytes)) * 100m;
                reduction = Clamp(reduction, -100m, 100m);
                reductions.Add(Math.Round(reduction, 2, MidpointRounding.AwayFromZero));
            }
            finally
            {
                TryDelete(sourceSample.Path);
            }
        }

        if (reductions.Count == 0)
        {
            return null;
        }

        return Math.Round(reductions.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private SourceSample? CreateSourceSample(string inputPath, DownscaleSampleWindow window)
    {
        if (!File.Exists(inputPath) || window.DurationSeconds < 1)
        {
            return null;
        }

        var samplePath = Path.Combine(Path.GetTempPath(), $"tomkvgpu-srcsample-{Guid.NewGuid():N}.mkv");
        var arguments = new[]
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-ss", window.StartSeconds.ToString(CultureInfo.InvariantCulture),
            "-t", window.DurationSeconds.ToString(CultureInfo.InvariantCulture),
            "-i", inputPath,
            "-map", "0:v:0",
            "-map", "0:a?",
            "-c", "copy",
            "-sn",
            samplePath
        };

        if (!TryExecuteProcess(arguments) || !File.Exists(samplePath))
        {
            TryDelete(samplePath);
            return null;
        }

        var size = new FileInfo(samplePath).Length;
        if (size <= 0)
        {
            TryDelete(samplePath);
            return null;
        }

        return new SourceSample(samplePath, size);
    }

    private long? EncodeSample(string samplePath, DownscaleDefaults settings)
    {
        if (!File.Exists(samplePath))
        {
            return null;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"tomkvgpu-outsample-{Guid.NewGuid():N}.mkv");
        var arguments = new[]
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-hwaccel", "cuda",
            "-hwaccel_output_format", "cuda",
            "-i", samplePath,
            "-map", "0:v:0",
            "-fps_mode:v", "cfr",
            "-vf", $"scale_cuda=-2:576:interp_algo={settings.Algorithm}:format=nv12",
            "-c:v", "h264_nvenc",
            "-preset", "p6",
            "-rc", "vbr_hq",
            "-cq", settings.Cq.ToString(CultureInfo.InvariantCulture),
            "-b:v", "0",
            "-maxrate", FormatRate(settings.Maxrate),
            "-bufsize", FormatRate(settings.Bufsize),
            "-spatial_aq", "1",
            "-temporal_aq", "1",
            "-rc-lookahead", "32",
            "-profile:v", "high",
            "-level:v", "4.1",
            "-g", "48",
            "-map", "0:a?",
            "-c:a", "aac",
            "-ar", "48000",
            "-ac", "2",
            "-b:a", "192k",
            "-af", "aresample=async=1:first_pts=0",
            "-sn",
            "-max_muxing_queue_size", "4096",
            outputPath
        };

        try
        {
            if (!TryExecuteProcess(arguments) || !File.Exists(outputPath))
            {
                return null;
            }

            var size = new FileInfo(outputPath).Length;
            return size > 0 ? size : null;
        }
        finally
        {
            TryDelete(outputPath);
        }
    }

    private bool TryExecuteProcess(IReadOnlyList<string> arguments)
    {
        using var process = new Process();
        process.StartInfo = CreateStartInfo(arguments);
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void TryDelete(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
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

    private sealed record SourceSample(string Path, decimal SizeBytes);

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
