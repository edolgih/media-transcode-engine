using System.Globalization;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Runtime.Downscaling;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Scenarios;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Cli.Scenarios;

/*
Это CLI-адаптер для сценария tomkvgpu.
Он знает свои опции, валидирует их, строит runtime-request и переводит ошибки в legacy-compatible вывод.
*/
/// <summary>
/// Implements the CLI contract for the legacy <c>tomkvgpu</c> application scenario.
/// </summary>
internal sealed class ToMkvGpuCliScenarioHandler : ICliScenarioHandler
{
    private const string DownscaleOptionName = "--downscale";
    private const string KeepSourceOptionName = "--keep-source";
    private const string OverlayBackgroundOptionName = "--overlay-bg";
    private const string MaxFramesPerSecondOptionName = "--max-fps";
    private const string SynchronizeAudioOptionName = "--sync-audio";
    private const string ContentProfileOptionName = "--content-profile";
    private const string QualityProfileOptionName = "--quality-profile";
    private const string NoAutoSampleOptionName = "--no-autosample";
    private const string AutoSampleModeOptionName = "--autosample-mode";
    private const string DownscaleAlgorithmOptionName = "--downscale-algo";
    private const string CqOptionName = "--cq";
    private const string MaxrateOptionName = "--maxrate";
    private const string BufsizeOptionName = "--bufsize";
    private const string NvencPresetOptionName = "--nvenc-preset";

    private readonly ToMkvGpuInfoFormatter _infoFormatter;

    /// <summary>
    /// Initializes the CLI handler for the <c>tomkvgpu</c> scenario.
    /// </summary>
    /// <param name="infoFormatter">Formatter used for info-mode output.</param>
    public ToMkvGpuCliScenarioHandler(ToMkvGpuInfoFormatter infoFormatter)
    {
        _infoFormatter = infoFormatter ?? throw new ArgumentNullException(nameof(infoFormatter));
    }

    public string Name => "tomkvgpu";

    public IReadOnlyList<string> LegacyCommandTokens { get; } = ["tomkvgpu"];

    public IReadOnlyList<CliHelpOption> HelpOptions { get; } =
    [
        new CliHelpOption("--downscale <576|480|424>", "Downscale target height."),
        new CliHelpOption("--keep-source", "Keep source file and write output to a new path."),
        new CliHelpOption("--overlay-bg", "Apply overlay background path during encode."),
        new CliHelpOption("--max-fps <50|40|30|24>", "Optional frame-rate cap. Supported values: 50, 40, 30, 24."),
        new CliHelpOption("--sync-audio", "Force sync-safe audio path."),
        new CliHelpOption("--content-profile <anime|mult|film>", "Downscale profile content kind."),
        new CliHelpOption("--quality-profile <high|default|low>", "Downscale profile quality kind."),
        new CliHelpOption("--no-autosample", "Disable downscale autosample adjustments."),
        new CliHelpOption("--autosample-mode <accurate|fast|hybrid>", "Downscale autosample mode."),
        new CliHelpOption("--downscale-algo <bilinear|bicubic|lanczos>", "Explicit downscale algorithm override."),
        new CliHelpOption("--cq <int>", "Explicit NVENC CQ override."),
        new CliHelpOption("--maxrate <number>", "Explicit VBV maxrate in Mbit/s."),
        new CliHelpOption("--bufsize <number>", "Explicit VBV bufsize in Mbit/s."),
        new CliHelpOption("--nvenc-preset <preset>", "Explicit NVENC preset override.")
    ];

    /// <summary>
    /// Gets command examples for the <c>tomkvgpu</c> scenario.
    /// </summary>
    /// <param name="exeName">Executable name used in rendered examples.</param>
    /// <returns>Scenario-specific command examples.</returns>
    public IReadOnlyList<string> GetHelpExamples(string exeName)
    {
        return
        [
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\"",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --info",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --keep-source --downscale 480",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --overlay-bg --sync-audio",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --max-fps 50",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --downscale 576 --content-profile film --quality-profile default",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --downscale 480 --content-profile film --quality-profile default",
            $"Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | {exeName} --scenario tomkvgpu --info"
        ];
    }

    /// <summary>
    /// Validates raw scenario-specific arguments for the <c>tomkvgpu</c> scenario.
    /// </summary>
    /// <param name="args">Raw scenario-specific arguments.</param>
    /// <param name="errorText">Error message on failure.</param>
    /// <returns><see langword="true"/> when the arguments are valid; otherwise <see langword="false"/>.</returns>
    public bool TryValidate(IReadOnlyList<string> args, out string? errorText)
    {
        return TryCreateRuntimeRequest(args, out _, out errorText);
    }

    /// <summary>
    /// Creates the runtime <see cref="ToMkvGpuScenario"/> instance for the supplied CLI request.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <returns>Runtime scenario instance.</returns>
    public TranscodeScenario CreateScenario(CliTranscodeRequest request)
    {
        return new ToMkvGpuScenario(GetRuntimeRequest(request));
    }

    /// <summary>
    /// Formats info-mode output for a successfully built <c>tomkvgpu</c> plan.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <param name="video">Inspected source video facts.</param>
    /// <param name="plan">Built transcode plan.</param>
    /// <returns>Info-mode output line.</returns>
    public string FormatInfo(CliTranscodeRequest request, SourceVideo video, TranscodePlan plan)
    {
        return _infoFormatter.Format(video, plan);
    }

    /// <summary>
    /// Maps a processing exception to legacy-compatible CLI failure output for <c>tomkvgpu</c>.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <param name="exception">Exception raised during processing.</param>
    /// <returns>Scenario-specific failure description.</returns>
    public CliScenarioFailure DescribeFailure(CliTranscodeRequest request, Exception exception)
    {
        var runtimeRequest = GetRuntimeRequest(request);
        var failure = ClassifyFailure(runtimeRequest, exception);
        var fileName = Path.GetFileName(request.InputPath);
        var infoOutput = failure.Kind switch
        {
            HandledFailureKind.IoError => $"{fileName}: [i/o error]",
            HandledFailureKind.UnexpectedFailure => $"{fileName}: [unexpected failure]",
            _ => _infoFormatter.FormatFailure(request.InputPath, exception)
        };
        var nonInfoOutput = failure.Kind switch
        {
            HandledFailureKind.UnknownDimensionsOverlay => $"REM Unknown dimensions: {fileName}",
            HandledFailureKind.NoVideoStream => $"REM Нет видеопотока: {fileName}",
            HandledFailureKind.DownscaleNotImplemented => $"REM Downscale 720 not implemented: {fileName}",
            HandledFailureKind.DownscaleSourceBucket => $"REM {exception.Message}",
            HandledFailureKind.ProbeFailure => $"REM ffprobe failed: {fileName}",
            HandledFailureKind.IoError => $"REM I/O error: {fileName}",
            HandledFailureKind.UnexpectedFailure => $"REM Unexpected failure: {fileName}",
            _ => throw new InvalidOperationException($"Unhandled failure kind '{failure.Kind}'.")
        };

        return new CliScenarioFailure(failure.Level, failure.LogToken, nonInfoOutput, infoOutput);
    }

    private static ToMkvGpuRequest GetRuntimeRequest(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (TryCreateRuntimeRequest(request.ScenarioArgs, out var runtimeRequest, out var errorText))
        {
            return runtimeRequest;
        }

        throw new InvalidOperationException(
            $"CLI request for scenario '{request.ScenarioName}' is invalid: {errorText}");
    }

    private static bool TryCreateRuntimeRequest(
        IReadOnlyList<string> args,
        out ToMkvGpuRequest request,
        out string? errorText)
    {
        request = default!;
        errorText = null;

        var overlayBackground = false;
        var synchronizeAudio = false;
        var keepSource = false;
        var noAutoSample = false;
        int? downscale = null;
        int? maxFramesPerSecond = null;
        int? cq = null;
        decimal? maxrate = null;
        decimal? bufsize = null;
        string? contentProfile = null;
        string? qualityProfile = null;
        string? autoSampleMode = null;
        string? algorithm = null;
        string? nvencPreset = null;

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (string.Equals(token, KeepSourceOptionName, StringComparison.OrdinalIgnoreCase))
            {
                keepSource = true;
                continue;
            }

            if (string.Equals(token, OverlayBackgroundOptionName, StringComparison.OrdinalIgnoreCase))
            {
                overlayBackground = true;
                continue;
            }

            if (string.Equals(token, SynchronizeAudioOptionName, StringComparison.OrdinalIgnoreCase))
            {
                synchronizeAudio = true;
                continue;
            }

            if (string.Equals(token, NoAutoSampleOptionName, StringComparison.OrdinalIgnoreCase))
            {
                noAutoSample = true;
                continue;
            }

            if (string.Equals(token, DownscaleOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadInt(args, ref i, token, "--downscale must be an integer.", out downscale, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, MaxFramesPerSecondOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadInt(args, ref i, token, "--max-fps must be an integer.", out maxFramesPerSecond, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, CqOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadInt(args, ref i, token, "--cq must be an integer.", out cq, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, MaxrateOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadDecimal(args, ref i, token, "--maxrate must be a number.", out maxrate, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, BufsizeOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadDecimal(args, ref i, token, "--bufsize must be a number.", out bufsize, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, ContentProfileOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(args, ref i, token, out contentProfile, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, QualityProfileOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(args, ref i, token, out qualityProfile, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, AutoSampleModeOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(args, ref i, token, out autoSampleMode, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, DownscaleAlgorithmOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(args, ref i, token, out algorithm, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, NvencPresetOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(args, ref i, token, out nvencPreset, out errorText))
                {
                    return false;
                }

                continue;
            }

            errorText = token.StartsWith("-", StringComparison.Ordinal)
                ? $"Unknown option: {token}"
                : $"Unexpected argument: {token}";
            return false;
        }

        if (maxFramesPerSecond.HasValue &&
            !ToMkvGpuRequest.IsSupportedMaxFramesPerSecond(maxFramesPerSecond.Value))
        {
            errorText = $"--max-fps must be one of: {ToMkvGpuRequest.SupportedMaxFramesPerSecondDisplay}.";
            return false;
        }

        try
        {
            var downscaleRequest = new DownscaleRequest(
                targetHeight: downscale,
                contentProfile: contentProfile,
                qualityProfile: qualityProfile,
                noAutoSample: noAutoSample,
                autoSampleMode: autoSampleMode,
                algorithm: algorithm,
                cq: cq,
                maxrate: maxrate,
                bufsize: bufsize);

            request = new ToMkvGpuRequest(
                overlayBackground: overlayBackground,
                synchronizeAudio: synchronizeAudio,
                keepSource: keepSource,
                downscale: downscaleRequest.HasValue ? downscaleRequest : null,
                nvencPreset: nvencPreset,
                maxFramesPerSecond: maxFramesPerSecond);

            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            errorText = exception.ParamName switch
            {
                "targetHeight" => "--downscale must be greater than zero.",
                "cq" => "--cq must be greater than zero.",
                "maxrate" => "--maxrate must be greater than zero.",
                "bufsize" => "--bufsize must be greater than zero.",
                "maxFramesPerSecond" => $"--max-fps must be one of: {ToMkvGpuRequest.SupportedMaxFramesPerSecondDisplay}.",
                _ => exception.Message
            };

            return false;
        }
    }

    private static bool TryReadString(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        out string value,
        out string? errorText)
    {
        return TryReadRequiredValue(args, ref index, optionName, out value, out errorText);
    }

    private static bool TryReadInt(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        string invalidValueError,
        out int? value,
        out string? errorText)
    {
        value = null;
        errorText = null;

        if (!TryReadRequiredValue(args, ref index, optionName, out var token, out errorText))
        {
            return false;
        }

        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            errorText = invalidValueError;
            return false;
        }

        value = parsedValue;
        return true;
    }

    private static bool TryReadDecimal(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        string invalidValueError,
        out decimal? value,
        out string? errorText)
    {
        value = null;
        errorText = null;

        if (!TryReadRequiredValue(args, ref index, optionName, out var token, out errorText))
        {
            return false;
        }

        if (!decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
        {
            errorText = invalidValueError;
            return false;
        }

        value = parsedValue;
        return true;
    }

    private static bool TryReadRequiredValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        out string value,
        out string? errorText)
    {
        value = string.Empty;
        errorText = null;

        var valueIndex = index + 1;
        if (valueIndex >= args.Count)
        {
            errorText = $"{optionName} requires a value.";
            return false;
        }

        var token = args[valueIndex];
        if (token.StartsWith("-", StringComparison.Ordinal))
        {
            errorText = $"{optionName} requires a value.";
            return false;
        }

        value = token;
        index = valueIndex;
        return true;
    }

    private static HandledFailure ClassifyFailure(ToMkvGpuRequest request, Exception exception)
    {
        if (exception is IOException or UnauthorizedAccessException)
        {
            return new HandledFailure(HandledFailureKind.IoError, "io_error", LogLevel.Error);
        }

        var message = exception.Message;
        if (request.OverlayBackground &&
            (message.Contains("valid video width", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("valid video height", StringComparison.OrdinalIgnoreCase)))
        {
            return new HandledFailure(HandledFailureKind.UnknownDimensionsOverlay, "unknown_dimensions", LogLevel.Warning);
        }

        if (message.Contains("video stream", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.NoVideoStream, "no_video_stream", LogLevel.Warning);
        }

        if (message.Contains("downscale", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("720", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.DownscaleNotImplemented, "downscale_not_implemented", LogLevel.Warning);
        }

        if (message.Contains("source bucket missing", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("source bucket invalid", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.DownscaleSourceBucket, "downscale_source_bucket", LogLevel.Warning);
        }

        if (message.Contains("ffprobe", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("video probe", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("streams", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.ProbeFailure, "probe_failure", LogLevel.Warning);
        }

        return new HandledFailure(HandledFailureKind.UnexpectedFailure, "unexpected_failure", LogLevel.Warning);
    }

    private enum HandledFailureKind
    {
        UnknownDimensionsOverlay,
        NoVideoStream,
        DownscaleNotImplemented,
        DownscaleSourceBucket,
        ProbeFailure,
        IoError,
        UnexpectedFailure
    }

    private sealed class HandledFailure
    {
        public HandledFailure(HandledFailureKind kind, string logToken, LogLevel level)
        {
            Kind = kind;
            LogToken = logToken ?? throw new ArgumentNullException(nameof(logToken));
            Level = level;
        }

        public HandledFailureKind Kind { get; }

        public string LogToken { get; }

        public LogLevel Level { get; }
    }
}
