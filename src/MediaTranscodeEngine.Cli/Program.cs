using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Cli.Processing;
using MediaTranscodeEngine.Runtime.Inspection;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Tools.Ffmpeg;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text;

namespace MediaTranscodeEngine.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ConfigureUtf8ConsoleWriters();

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
                               !string.IsNullOrWhiteSpace(options.FfmpegPath),
                    "Invalid Runtime options in configuration.")
                .ValidateOnStart();

            builder.Services.AddSingleton<IVideoProbe>(static services =>
            {
                var options = services.GetRequiredService<IOptions<RuntimeValues>>().Value;
                return new FfprobeVideoProbe(options.FfprobePath!);
            });
            builder.Services.AddSingleton<VideoInspector>(static services =>
                new VideoInspector(services.GetRequiredService<IVideoProbe>()));
            builder.Services.AddSingleton<ITranscodeTool>(static services =>
            {
                var options = services.GetRequiredService<IOptions<RuntimeValues>>().Value;
                var logger = services.GetRequiredService<ILogger<FfmpegTool>>();
                return new FfmpegTool(options.FfmpegPath!, logger);
            });
            builder.Services.AddSingleton<ToMkvGpuInfoFormatter>();
            builder.Services.AddSingleton<ITranscodeProcessor, PrimaryTranscodeProcessor>();

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

    internal static void ConfigureUtf8ConsoleWriters()
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
    }

    internal static int RunCli(
        string[] args,
        Microsoft.Extensions.Logging.ILogger logger,
        IServiceProvider services,
        RuntimeValues runtimeValues)
    {
        return RunCli(args, logger, services, runtimeValues, readRedirectedStdIn: true);
    }

    internal static int RunCli(
        string[] args,
        Microsoft.Extensions.Logging.ILogger logger,
        IServiceProvider services,
        RuntimeValues runtimeValues,
        bool readRedirectedStdIn)
    {
        var effectiveArgs = BuildEffectiveArgs(args, readRedirectedStdIn);
        logger.LogInformation(
            "CLI request received. OriginalArgCount={OriginalArgCount} EffectiveArgCount={EffectiveArgCount} ReadRedirectedStdIn={ReadRedirectedStdIn}",
            args.Length,
            effectiveArgs.Length,
            readRedirectedStdIn);

        if (effectiveArgs.Length == 0 || effectiveArgs.Any(IsHelpToken))
        {
            logger.LogInformation("CLI help requested.");
            Console.WriteLine(CliHelpBuilder.BuildHelpText(runtimeValues));
            return 0;
        }

        if (!CliArgumentParser.TryParse(effectiveArgs, out var parsed, out var errorText))
        {
            logger.LogWarning("CLI parse failed: {ErrorText}. Args={Args}", errorText, string.Join(" ", effectiveArgs));
            Console.Error.WriteLine(errorText);
            return 1;
        }

        logger.LogInformation(
            "CLI request parsed. Scenario={Scenario} Info={Info} InputCount={InputCount} DownscaleTarget={DownscaleTarget} AutoSampleMode={AutoSampleMode} NoAutoSample={NoAutoSample}",
            parsed.RequestTemplate.Scenario,
            parsed.RequestTemplate.Info,
            parsed.Inputs.Count,
            parsed.RequestTemplate.DownscaleTarget,
            parsed.RequestTemplate.AutoSampleMode,
            parsed.RequestTemplate.NoAutoSample);

        if (parsed.Inputs.Count == 0)
        {
            const string noInputError = "No input files provided. Use --input or pipe file paths to stdin.";
            logger.LogWarning(noInputError);
            Console.Error.WriteLine(noInputError);
            return 1;
        }

        var processor = services.GetRequiredService<ITranscodeProcessor>();
        if (!parsed.RequestTemplate.Info)
        {
            Console.WriteLine("chcp 65001");
        }

        foreach (var input in parsed.Inputs)
        {
            logger.LogInformation("CLI input processing started. InputPath={InputPath}", input);
            try
            {
                var request = CliRequestMappers.BuildRequest(parsed.RequestTemplate, input);
                var line = processor.Process(request);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Console.WriteLine(line);
                }

                logger.LogInformation(
                    "CLI input processing completed. InputPath={InputPath} HasOutput={HasOutput} OutputLine={OutputLine}",
                    input,
                    !string.IsNullOrWhiteSpace(line),
                    line);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to process input {InputPath}", input);
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        logger.LogInformation("CLI request completed. InputCount={InputCount}", parsed.Inputs.Count);
        return 0;
    }

    private static string[] BuildEffectiveArgs(string[] args, bool readRedirectedStdIn)
    {
        if (!readRedirectedStdIn || !Console.IsInputRedirected)
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

    private static bool IsHelpToken(string token)
    {
        return string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase);
    }
}
