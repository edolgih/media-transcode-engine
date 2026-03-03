using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Policy;
using MediaTranscodeEngine.Cli.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace MediaTranscodeEngine.Cli;

public static class Program
{
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
            builder.Services.AddSingleton<H264RemuxEligibilityPolicy>();
            builder.Services.AddSingleton<H264CommandBuilder>();
            builder.Services.AddSingleton<H264TranscodeEngine>();

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

        if (!CliArgumentParser.TryParse(effectiveArgs, out var parsed, out var errorText))
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

        switch (parsed.ScenarioName.ToLowerInvariant())
        {
            case CliContracts.ToMkvGpuScenario:
            {
                var engine = services.GetRequiredService<TranscodeEngine>();
                foreach (var input in parsed.Inputs)
                {
                    var request = CliScenarioMappers.BuildToMkvRequest(parsed.ToMkvRequestTemplate, input);
                    var line = engine.Process(request);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        Console.WriteLine(line);
                    }
                }

                break;
            }
            case CliContracts.ToH264GpuScenario:
            {
                var engine = services.GetRequiredService<H264TranscodeEngine>();
                foreach (var input in parsed.Inputs)
                {
                    var request = CliScenarioMappers.BuildToH264Request(parsed.ToH264RequestTemplate, input);
                    var line = engine.Process(request);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        Console.WriteLine(line);
                    }
                }

                break;
            }
            default:
                logger.LogWarning("Unknown scenario: {Scenario}", parsed.ScenarioName);
                Console.Error.WriteLine($"Unknown scenario: {parsed.ScenarioName}");
                return 1;
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
        return CliHelpBuilder.BuildHelpText(runtimeValues);
    }

    private static bool IsHelpToken(string token)
    {
        return string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase);
    }

}
