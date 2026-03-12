using System.Globalization;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Scenarios;
using MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Cli.Scenarios;

/*
Это CLI-адаптер для сценария toh264gpu.
Он валидирует и интерпретирует свои аргументы, строит runtime-request и переводит ошибки в короткие legacy-style маркеры.
*/
/// <summary>
/// Implements the CLI contract for the legacy <c>toh264gpu</c> application scenario.
/// </summary>
internal sealed class ToH264GpuCliScenarioHandler : ICliScenarioHandler
{
    private const string KeepSourceOptionName = "--keep-source";
    private const string DownscaleOptionName = "--downscale";
    private const string KeepFpsOptionName = "--keep-fps";
    private const string ContentProfileOptionName = "--content-profile";
    private const string QualityProfileOptionName = "--quality-profile";
    private const string AutoSampleModeOptionName = "--autosample-mode";
    private const string DownscaleAlgoOptionName = "--downscale-algo";
    private const string CqOptionName = "--cq";
    private const string MaxrateOptionName = "--maxrate";
    private const string BufsizeOptionName = "--bufsize";
    private const string NvencPresetOptionName = "--nvenc-preset";
    private const string DenoiseOptionName = "--denoise";
    private const string SynchronizeAudioOptionName = "--sync-audio";
    private const string MkvOptionName = "--mkv";

    private readonly ToH264GpuInfoFormatter _infoFormatter;

    public ToH264GpuCliScenarioHandler(ToH264GpuInfoFormatter infoFormatter)
    {
        _infoFormatter = infoFormatter ?? throw new ArgumentNullException(nameof(infoFormatter));
    }

    public string Name => "toh264gpu";

    public IReadOnlyList<string> LegacyCommandTokens => ["toh264gpu"];

    public IReadOnlyList<CliHelpOption> HelpOptions =>
    [
        new CliHelpOption("--keep-source", "Keep the source file instead of replacing it when output path matches the input."),
        new CliHelpOption("--downscale <720|576|480|424>", "GPU downscale when the source is higher than the target."),
        new CliHelpOption("--keep-fps", "Keep the source FPS in downscale mode instead of capping to 30000/1001."),
        new CliHelpOption("--content-profile <anime|mult|film>", "Quality-oriented content profile."),
        new CliHelpOption("--quality-profile <high|default|low>", "Quality-oriented quality profile."),
        new CliHelpOption("--autosample-mode <accurate|fast|hybrid>", "Autosample mode."),
        new CliHelpOption("--downscale-algo <bicubic|lanczos|bilinear>", "scale_cuda interpolation algorithm."),
        new CliHelpOption("--cq <1..51>", "Explicit CQ override."),
        new CliHelpOption("--maxrate <number>", "Explicit VBV maxrate in Mbit/s."),
        new CliHelpOption("--bufsize <number>", "Explicit VBV bufsize in Mbit/s."),
        new CliHelpOption("--nvenc-preset <p1..p7>", "Explicit NVENC preset override."),
        new CliHelpOption("--denoise", "Enable hqdn3d in normal encode mode."),
        new CliHelpOption("--sync-audio", "Use the sync-safe repair path and disable audio copy."),
        new CliHelpOption("--mkv", "Write MKV instead of MP4.")
    ];

    public IReadOnlyList<string> GetHelpExamples(string exeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeName);

        return
        [
            $"{exeName} --scenario toh264gpu --input C:\\video\\input.m4v",
            $"{exeName} --scenario toh264gpu --input C:\\video\\input.mkv --keep-source --sync-audio",
            $"{exeName} --scenario toh264gpu --input C:\\video\\input.mkv --content-profile film --quality-profile default"
        ];
    }

    public bool TryValidate(IReadOnlyList<string> args, out string? errorText)
    {
        return TryParseRequest(args, out _, out errorText);
    }

    public TranscodeScenario CreateScenario(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryParseRequest(request.ScenarioArgs, out var runtimeRequest, out var errorText))
        {
            throw new InvalidOperationException(errorText ?? "Invalid toh264gpu arguments.");
        }

        return new ToH264GpuScenario(runtimeRequest);
    }

    public string FormatInfo(CliTranscodeRequest request, SourceVideo video, TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        return _infoFormatter.Format(video, plan);
    }

    public CliScenarioFailure DescribeFailure(CliTranscodeRequest request, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(exception);

        var fileName = Path.GetFileName(request.InputPath);
        if (exception is IOException or UnauthorizedAccessException)
        {
            return new CliScenarioFailure(
                LogLevel.Error,
                "io_error",
                $"REM I/O error: {fileName}",
                $"{fileName}: [i/o error]");
        }

        if (exception.Message.Contains("video stream", StringComparison.OrdinalIgnoreCase))
        {
            return new CliScenarioFailure(
                LogLevel.Warning,
                "no_video_stream",
                $"REM Нет видеопотока: {fileName}",
                _infoFormatter.FormatFailure(request.InputPath, exception));
        }

        if (exception.Message.Contains("ffprobe", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("video probe", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("streams", StringComparison.OrdinalIgnoreCase))
        {
            return new CliScenarioFailure(
                LogLevel.Warning,
                "probe_failure",
                $"REM ffprobe failed: {fileName}",
                _infoFormatter.FormatFailure(request.InputPath, exception));
        }

        return new CliScenarioFailure(
            LogLevel.Warning,
            "unexpected_failure",
            $"REM Unexpected failure: {fileName}",
            $"{fileName}: [unexpected failure]");
    }

    private static bool TryParseRequest(
        IReadOnlyList<string> args,
        out ToH264GpuRequest request,
        out string? errorText)
    {
        request = null!;
        errorText = null;

        var keepSource = false;
        var downscaleTargetHeight = (int?)null;
        var keepFramesPerSecond = false;
        string? downscaleAlgorithm = null;
        var cq = (int?)null;
        var maxrate = (decimal?)null;
        var bufsize = (decimal?)null;
        string? contentProfile = null;
        string? qualityProfile = null;
        string? autoSampleMode = null;
        string? nvencPreset = null;
        var denoise = false;
        var synchronizeAudio = false;
        var outputMkv = false;

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            switch (token)
            {
                case KeepSourceOptionName:
                    keepSource = true;
                    break;

                case DownscaleOptionName:
                    if (!TryReadInt(args, ref index, DownscaleOptionName, "--downscale must be 720, 576, 480 or 424.", out downscaleTargetHeight, out errorText))
                    {
                        return false;
                    }

                    if (downscaleTargetHeight is not (720 or 576 or 480 or 424))
                    {
                        errorText = "--downscale must be 720, 576, 480 or 424.";
                        return false;
                    }

                    break;

                case KeepFpsOptionName:
                    keepFramesPerSecond = true;
                    break;

                case ContentProfileOptionName:
                    if (!TryReadString(args, ref index, ContentProfileOptionName, out contentProfile, out errorText))
                    {
                        return false;
                    }

                    break;

                case QualityProfileOptionName:
                    if (!TryReadString(args, ref index, QualityProfileOptionName, out qualityProfile, out errorText))
                    {
                        return false;
                    }

                    break;

                case AutoSampleModeOptionName:
                    if (!TryReadString(args, ref index, AutoSampleModeOptionName, out autoSampleMode, out errorText))
                    {
                        return false;
                    }

                    break;

                case DownscaleAlgoOptionName:
                    if (!TryReadString(args, ref index, DownscaleAlgoOptionName, out downscaleAlgorithm, out errorText))
                    {
                        return false;
                    }

                    if (downscaleAlgorithm is not ("bicubic" or "lanczos" or "bilinear"))
                    {
                        errorText = "--downscale-algo must be one of: bicubic, lanczos, bilinear.";
                        return false;
                    }

                    break;

                case CqOptionName:
                    if (!TryReadInt(args, ref index, CqOptionName, "--cq must be an integer from 1 to 51.", out cq, out errorText))
                    {
                        return false;
                    }

                    if (!cq.HasValue || cq.Value <= 0 || cq.Value > 51)
                    {
                        errorText = "--cq must be an integer from 1 to 51.";
                        return false;
                    }

                    break;

                case MaxrateOptionName:
                    if (!TryReadDecimal(args, ref index, MaxrateOptionName, "--maxrate must be a number.", out maxrate, out errorText))
                    {
                        return false;
                    }

                    if (!maxrate.HasValue || maxrate.Value <= 0m)
                    {
                        errorText = "--maxrate must be greater than zero.";
                        return false;
                    }

                    break;

                case BufsizeOptionName:
                    if (!TryReadDecimal(args, ref index, BufsizeOptionName, "--bufsize must be a number.", out bufsize, out errorText))
                    {
                        return false;
                    }

                    if (!bufsize.HasValue || bufsize.Value <= 0m)
                    {
                        errorText = "--bufsize must be greater than zero.";
                        return false;
                    }

                    break;

                case NvencPresetOptionName:
                    if (!TryReadString(args, ref index, NvencPresetOptionName, out nvencPreset, out errorText))
                    {
                        return false;
                    }

                    if (nvencPreset is not ("p1" or "p2" or "p3" or "p4" or "p5" or "p6" or "p7"))
                    {
                        errorText = "--nvenc-preset must be one of: p1, p2, p3, p4, p5, p6, p7.";
                        return false;
                    }

                    break;

                case DenoiseOptionName:
                    denoise = true;
                    break;

                case SynchronizeAudioOptionName:
                    synchronizeAudio = true;
                    break;

                case MkvOptionName:
                    outputMkv = true;
                    break;

                default:
                    errorText = $"Unexpected argument: {token}";
                    return false;
            }
        }

        request = new ToH264GpuRequest(
            keepSource: keepSource,
            downscaleTargetHeight: downscaleTargetHeight,
            keepFramesPerSecond: keepFramesPerSecond,
            contentProfile: contentProfile,
            qualityProfile: qualityProfile,
            autoSampleMode: autoSampleMode,
            downscaleAlgorithm: downscaleAlgorithm,
            cq: cq,
            maxrate: maxrate,
            bufsize: bufsize,
            nvencPreset: nvencPreset,
            denoise: denoise,
            synchronizeAudio: synchronizeAudio,
            outputMkv: outputMkv);
        return true;
    }

    private static bool TryReadString(
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

        value = token.Trim().ToLowerInvariant();
        index = valueIndex;
        return true;
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

        if (!TryReadString(args, ref index, optionName, out var token, out errorText))
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

        if (!TryReadString(args, ref index, optionName, out var token, out errorText))
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
}
