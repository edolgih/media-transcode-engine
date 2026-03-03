using System.Globalization;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Policy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace MediaTranscodeEngine.Cli;

public static class Program
{
    private const string LegacyCommandToken = "tomkvgpu";

    public static async Task<int> Main(string[] args)
    {
        Microsoft.Extensions.Logging.ILogger? startupLogger = null;
        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            });

            builder.Services.AddSerilog((services, loggerConfiguration) =>
                loggerConfiguration
                    .ReadFrom.Configuration(builder.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext());
            builder.Services
                .AddOptions<RuntimeValues>()
                .Bind(builder.Configuration.GetSection(nameof(RuntimeValues)))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.FfprobePath) &&
                               !string.IsNullOrWhiteSpace(options.FfmpegPath) &&
                               !string.IsNullOrWhiteSpace(options.AutoSampleNvencPreset) &&
                               options.ProcessTimeoutMs > 0 &&
                               options.SampleEncodeInactivityTimeoutMs > 0 &&
                               options.SampleDurationSeconds > 0 &&
                               options.SampleEncodeMaxRetries >= 0,
                    "Invalid Runtime options in configuration.")
                .ValidateOnStart();
            builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
            builder.Services.AddSingleton<IProfileRepository>(static services =>
            {
                var options = services.GetRequiredService<IOptions<RuntimeValues>>().Value;
                var contentRootPath = services.GetRequiredService<IHostEnvironment>().ContentRootPath;
                var profilesPath = ResolveConfiguredPath(options.ProfilesYamlPath, contentRootPath);

                return string.IsNullOrWhiteSpace(profilesPath)
                    ? new StaticProfileRepository()
                    : new YamlProfileRepository(profilesPath);
            });
            builder.Services.AddSingleton<IProbeReader>(static services =>
            {
                var options = services.GetRequiredService<IOptions<RuntimeValues>>().Value;
                return new FfprobeReader(
                    processRunner: services.GetRequiredService<IProcessRunner>(),
                    ffprobePath: options.FfprobePath!,
                    timeoutMs: options.ProcessTimeoutMs,
                    logger: services.GetRequiredService<ILogger<FfprobeReader>>());
            });
            builder.Services.AddSingleton<IAutoSampleReductionProvider>(static services =>
            {
                var options = services.GetRequiredService<IOptions<RuntimeValues>>().Value;
                return new FfmpegAutoSampleReductionProvider(
                    probeReader: services.GetRequiredService<IProbeReader>(),
                    processRunner: services.GetRequiredService<IProcessRunner>(),
                    ffmpegPath: options.FfmpegPath!,
                    timeoutMs: options.ProcessTimeoutMs,
                    sampleEncodeInactivityTimeoutMs: options.SampleEncodeInactivityTimeoutMs,
                    sampleDurationSeconds: options.SampleDurationSeconds,
                    nvencPreset: options.AutoSampleNvencPreset!,
                    sampleEncodeMaxRetries: options.SampleEncodeMaxRetries,
                    logger: services.GetRequiredService<ILogger<FfmpegAutoSampleReductionProvider>>());
            });
            builder.Services.AddSingleton<TranscodePolicy>();
            builder.Services.AddSingleton<FfmpegCommandBuilder>();
            builder.Services.AddSingleton<TranscodeEngine>();

            using var host = builder.Build();
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(Program));
            startupLogger = logger;
            var runtimeOptions = host.Services.GetRequiredService<IOptions<RuntimeValues>>().Value;

            return RunCli(args, logger, host.Services, runtimeOptions);
        }
        catch (Exception ex)
        {
            if (startupLogger is not null)
            {
                startupLogger.LogError(ex, "CLI failed.");
            }
            else
            {
                Log.Error(ex, "CLI failed.");
            }

            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static int RunCli(
        string[] args,
        Microsoft.Extensions.Logging.ILogger logger,
        IServiceProvider services,
        RuntimeValues runtimeValues)
    {
        var effectiveArgs = BuildEffectiveArgs(args);

        if (effectiveArgs.Length == 0 || effectiveArgs.Any(IsHelpToken))
        {
            Console.WriteLine(GetHelpText(runtimeValues));
            return 0;
        }

        if (!TryParse(effectiveArgs, out var parsed, out var errorText))
        {
            logger.LogWarning("CLI parse failed: {ErrorText}. Args={Args}", errorText, string.Join(" ", effectiveArgs));
            Console.Error.WriteLine(errorText);
            return 1;
        }

        if (parsed.Inputs.Count == 0)
        {
            const string noInputError = "No input files provided. Use --input or pipe file paths to stdin.";
            logger.LogWarning(noInputError);
            Console.Error.WriteLine(noInputError);
            return 1;
        }

        var engine = services.GetRequiredService<TranscodeEngine>();
        foreach (var input in parsed.Inputs)
        {
            var request = parsed.CreateRequest(input);
            var line = engine.Process(request);
            if (!string.IsNullOrWhiteSpace(line))
            {
                Console.WriteLine(line);
            }
        }

        return 0;
    }

    private static string ResolveConfiguredPath(string? configuredPath, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, configuredPath));
    }

    private static string[] BuildEffectiveArgs(string[] args)
    {
        if (!Console.IsInputRedirected)
        {
            return args;
        }

        var effective = new List<string>(args);
        foreach (var path in ReadInputPathsFromStdIn())
        {
            effective.Add("--input");
            effective.Add(path);
        }

        return effective.ToArray();
    }

    private static IEnumerable<string> ReadInputPathsFromStdIn()
    {
        while (true)
        {
            var rawLine = Console.ReadLine();
            if (rawLine is null)
            {
                yield break;
            }

            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Length >= 2 && line[0] == '"' && line[^1] == '"')
            {
                line = line[1..^1];
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    private static string GetHelpText(RuntimeValues runtimeValues)
    {
        var exeName = Path.GetFileName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(exeName))
        {
            exeName = "MediaTranscodeEngine.Cli.exe";
        }

        var lines = new[]
        {
            "MediaTranscodeEngine CLI",
            string.Empty,
            $"Usage: {exeName} [options]",
            string.Empty,
            "Options:",
            "  --help, -h                                   Show this help.",
            "  --input <path>                               Add input file path (repeatable).",
            "  --info                                       Info mode (no ffmpeg command).",
            "  --overlay-bg                                 Force overlay background mode.",
            "  --downscale <int>                            Enable downscale path (576|720).",
            "  --downscale-algo <value>                     bicubic|lanczos|bilinear.",
            "  --content-profile <value>                    anime|mult|film (default: film).",
            "  --quality-profile <value>                    high|default|low (default: default).",
            "  --no-auto-sample                             Disable auto-sample for 576 path.",
            "  --auto-sample-mode <value>                   accurate|fast|hybrid (default: accurate).",
            "  --sync-audio                                 Force audio resample path.",
            "  --force-video-encode                         Force video encode even if copyable.",
            "  --cq <int>                                   CQ override (0..51).",
            "  --maxrate <number>                           Maxrate override (Mbps).",
            "  --bufsize <number>                           Bufsize override (Mbps).",
            "  --nvenc-preset <value>                       p1..p7 (default: p6).",
            string.Empty,
            "Configuration (appsettings / environment):",
            $"  {nameof(RuntimeValues)}:ProfilesYamlPath                 current: {runtimeValues.ProfilesYamlPath}",
            $"  {nameof(RuntimeValues)}:FfprobePath                     current: {runtimeValues.FfprobePath}",
            $"  {nameof(RuntimeValues)}:FfmpegPath                      current: {runtimeValues.FfmpegPath}",
            $"  {nameof(RuntimeValues)}:ProcessTimeoutMs                current: {runtimeValues.ProcessTimeoutMs}",
            $"  {nameof(RuntimeValues)}:SampleEncodeInactivityTimeoutMs current: {runtimeValues.SampleEncodeInactivityTimeoutMs}",
            $"  {nameof(RuntimeValues)}:SampleDurationSeconds           current: {runtimeValues.SampleDurationSeconds}",
            $"  {nameof(RuntimeValues)}:SampleEncodeMaxRetries          current: {runtimeValues.SampleEncodeMaxRetries}",
            $"  {nameof(RuntimeValues)}:AutoSampleNvencPreset           current: {runtimeValues.AutoSampleNvencPreset}",
            string.Empty,
            "Examples:",
            $"  {exeName} --input \"C:\\video\\movie.mkv\"",
            $"  {exeName} --input \"C:\\video\\movie.mkv\" --info",
            $"  {exeName} --input \"C:\\video\\movie.mkv\" --downscale 576 --content-profile film --quality-profile default",
            $"  Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | {exeName} --info"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsHelpToken(string token)
    {
        return string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParse(string[] args, out ParsedArguments parsed, out string? errorText)
    {
        parsed = default!;
        errorText = null;

        var inputs = new List<string>();
        var info = false;
        var overlayBg = false;
        int? downscale = null;
        string? downscaleAlgo = null;
        var contentProfile = "film";
        var qualityProfile = "default";
        var noAutoSample = false;
        var autoSampleMode = "accurate";
        var syncAudio = false;
        var forceVideoEncode = false;
        int? cq = null;
        double? maxrate = null;
        double? bufsize = null;
        var nvencPreset = "p6";

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (string.Equals(token, LegacyCommandToken, StringComparison.OrdinalIgnoreCase))
            {
                errorText = "Do not use 'tomkvgpu' command token. Use CLI switches directly.";
                return false;
            }

            switch (token)
            {
                case "--help":
                case "-h":
                    break;
                case "--input":
                    if (!TryReadValue(args, ref i, token, out var inputPath, out errorText))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(inputPath))
                    {
                        inputs.Add(inputPath);
                    }

                    break;
                case "--info":
                    info = true;
                    break;
                case "--overlay-bg":
                    overlayBg = true;
                    break;
                case "--downscale":
                    if (!TryReadValue(args, ref i, token, out var downscaleToken, out errorText))
                    {
                        return false;
                    }

                    if (!int.TryParse(downscaleToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDownscale))
                    {
                        errorText = "--downscale must be an integer.";
                        return false;
                    }

                    downscale = parsedDownscale;
                    break;
                case "--downscale-algo":
                    if (!TryReadValue(args, ref i, token, out downscaleAlgo, out errorText))
                    {
                        return false;
                    }

                    break;
                case "--content-profile":
                    if (!TryReadValue(args, ref i, token, out contentProfile, out errorText))
                    {
                        return false;
                    }

                    break;
                case "--quality-profile":
                    if (!TryReadValue(args, ref i, token, out qualityProfile, out errorText))
                    {
                        return false;
                    }

                    break;
                case "--no-auto-sample":
                    noAutoSample = true;
                    break;
                case "--auto-sample-mode":
                    if (!TryReadValue(args, ref i, token, out autoSampleMode, out errorText))
                    {
                        return false;
                    }

                    break;
                case "--sync-audio":
                    syncAudio = true;
                    break;
                case "--force-video-encode":
                    forceVideoEncode = true;
                    break;
                case "--cq":
                    if (!TryReadValue(args, ref i, token, out var cqToken, out errorText))
                    {
                        return false;
                    }

                    if (!int.TryParse(cqToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCq))
                    {
                        errorText = "--cq must be an integer.";
                        return false;
                    }

                    cq = parsedCq;
                    break;
                case "--maxrate":
                    if (!TryReadValue(args, ref i, token, out var maxrateToken, out errorText))
                    {
                        return false;
                    }

                    if (!double.TryParse(maxrateToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMaxrate))
                    {
                        errorText = "--maxrate must be a number.";
                        return false;
                    }

                    maxrate = parsedMaxrate;
                    break;
                case "--bufsize":
                    if (!TryReadValue(args, ref i, token, out var bufsizeToken, out errorText))
                    {
                        return false;
                    }

                    if (!double.TryParse(bufsizeToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedBufsize))
                    {
                        errorText = "--bufsize must be a number.";
                        return false;
                    }

                    bufsize = parsedBufsize;
                    break;
                case "--nvenc-preset":
                    if (!TryReadValue(args, ref i, token, out nvencPreset, out errorText))
                    {
                        return false;
                    }

                    break;
                default:
                    errorText = token.StartsWith("-", StringComparison.Ordinal)
                        ? $"Unknown option: {token}"
                        : $"Unexpected argument: {token}";
                    return false;
            }
        }

        parsed = new ParsedArguments(
            Inputs: inputs,
            RequestTemplate: new TranscodeRequest(
                InputPath: "__input__",
                Info: info,
                OverlayBg: overlayBg,
                Downscale: downscale,
                DownscaleAlgoOverride: downscaleAlgo,
                ContentProfile: contentProfile,
                QualityProfile: qualityProfile,
                NoAutoSample: noAutoSample,
                AutoSampleMode: autoSampleMode,
                SyncAudio: syncAudio,
                Cq: cq,
                Maxrate: maxrate,
                Bufsize: bufsize,
                NvencPreset: nvencPreset,
                ForceVideoEncode: forceVideoEncode));
        return true;
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string optionName,
        out string value,
        out string? errorText)
    {
        value = string.Empty;
        errorText = null;

        var valueIndex = index + 1;
        if (valueIndex >= args.Length)
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

    private sealed record ParsedArguments(
        IReadOnlyList<string> Inputs,
        TranscodeRequest RequestTemplate)
    {
        public TranscodeRequest CreateRequest(string inputPath)
        {
            return RequestTemplate with { InputPath = inputPath };
        }
    }
}
