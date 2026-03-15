using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Parsing;
using Transcode.Cli.Core.Processing;
using Transcode.Cli.Core.Scenarios;
using Transcode.Core.Inspection;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.Videos;
using Transcode.Scenarios.ToH264Gpu.Cli;
using Transcode.Scenarios.ToH264Gpu.Core;
using Transcode.Scenarios.ToMkvGpu.Cli;
using Transcode.Scenarios.ToMkvGpu.Core;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Transcode.Cli;

/*
Это точка входа CLI. Здесь поднимается host, собираются зависимости,
разбираются аргументы и запускается обработка входных файлов.
*/
/// <summary>
/// Hosts the command-line entry point for media inspection and scenario-driven command generation.
/// </summary>
public static class Program
{
	/// <summary>
	/// Runs the CLI application.
	/// </summary>
	/// <param name="args">Raw command-line arguments.</param>
	/// <returns>Process exit code.</returns>
	public static async Task<int> Main(string[] args)
	{
		ConfigureUtf8ConsoleWriters();

		ILogger? startupLogger = null;
		try
		{
			var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
			{
				Args = args,
				ContentRootPath = AppContext.BaseDirectory
			});
			builder.Configuration["Serilog:WriteTo:0:Args:path"] = ResolveLogFilePath(
				builder.Configuration["Serilog:WriteTo:0:Args:path"],
				AppContext.BaseDirectory);

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
			builder.Services.AddSingleton(static services =>
			{
				var options = services.GetRequiredService<IOptions<RuntimeValues>>().Value;
				var logger = services.GetRequiredService<ILogger<ToMkvGpuFfmpegTool>>();
				return new ToMkvGpuFfmpegTool(options.FfmpegPath!, logger);
			});
			builder.Services.AddSingleton(static services =>
			{
				var options = services.GetRequiredService<IOptions<RuntimeValues>>().Value;
				var logger = services.GetRequiredService<ILogger<ToH264GpuFfmpegTool>>();
				return new ToH264GpuFfmpegTool(options.FfmpegPath!, logger);
			});
			builder.Services.AddSingleton(static services =>
				new FfmpegSampleMeasurer(
					services.GetRequiredService<IOptions<RuntimeValues>>().Value.FfmpegPath!));

			builder.Services.AddSingleton<ToMkvGpuInfoFormatter>();
			builder.Services.AddSingleton<ICliScenarioHandler>(static services =>
				new ToMkvGpuCliScenarioHandler(
					services.GetRequiredService<ToMkvGpuInfoFormatter>(),
					services.GetRequiredService<ToMkvGpuFfmpegTool>(),
					services.GetRequiredService<FfmpegSampleMeasurer>()));

			builder.Services.AddSingleton<ToH264GpuInfoFormatter>();
			builder.Services.AddSingleton<ICliScenarioHandler>(static services =>
				new ToH264GpuCliScenarioHandler(
					services.GetRequiredService<ToH264GpuInfoFormatter>(),
					services.GetRequiredService<ToH264GpuFfmpegTool>(),
					services.GetRequiredService<FfmpegSampleMeasurer>()));
			builder.Services.AddSingleton(static services =>
				new CliScenarioRegistry(services.GetServices<ICliScenarioHandler>()));
			builder.Services.AddSingleton<ITranscodeProcessor, PrimaryTranscodeProcessor>();

			using var host = builder.Build();
			var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(Program));
			startupLogger = logger;
			var runtimeOptions = host.Services.GetRequiredService<IOptions<RuntimeValues>>().Value;
			var scenarioRegistry = host.Services.GetRequiredService<CliScenarioRegistry>();

			return RunCli(args, logger, host.Services, runtimeOptions, scenarioRegistry, readRedirectedStdIn: true);
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

	/// <summary>
	/// Configures UTF-8 console writers without a BOM for standard output and error.
	/// </summary>
	internal static void ConfigureUtf8ConsoleWriters()
	{
		var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
		Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
		Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
	}

	/// <summary>
	/// Resolves the effective log file path from configuration and the CLI base directory.
	/// </summary>
	/// <param name="configuredPath">Configured path, absolute or relative.</param>
	/// <param name="cliBaseDirectory">CLI base directory used for relative paths.</param>
	/// <returns>Absolute log file path.</returns>
	internal static string ResolveLogFilePath(string? configuredPath, string cliBaseDirectory)
	{
		if (string.IsNullOrWhiteSpace(configuredPath))
		{
			return Path.GetFullPath(Path.Combine(cliBaseDirectory, "logs", "transcode-.log"));
		}

		if (Path.IsPathRooted(configuredPath))
		{
			return Path.GetFullPath(configuredPath);
		}

		return Path.GetFullPath(Path.Combine(cliBaseDirectory, configuredPath));
	}

	/// <summary>
	/// Runs the CLI using the registry available from the service provider or the default registry.
	/// </summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="logger">Logger used for CLI lifecycle events.</param>
	/// <param name="services">Application services.</param>
	/// <param name="runtimeValues">Runtime executable paths.</param>
	/// <returns>Process exit code.</returns>
	internal static int RunCli(
		string[] args,
		ILogger logger,
		IServiceProvider services,
		RuntimeValues runtimeValues)
	{
		return RunCli(
			args,
			logger,
			services,
			runtimeValues,
			services.GetService<CliScenarioRegistry>() ?? CreateDefaultScenarioRegistry(),
			readRedirectedStdIn: true);
	}

	/// <summary>
	/// Runs the CLI using the registry available from the service provider or the default registry.
	/// </summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="logger">Logger used for CLI lifecycle events.</param>
	/// <param name="services">Application services.</param>
	/// <param name="runtimeValues">Runtime executable paths.</param>
	/// <param name="readRedirectedStdIn">Whether redirected standard input should be treated as additional input paths.</param>
	/// <returns>Process exit code.</returns>
	internal static int RunCli(
		string[] args,
		ILogger logger,
		IServiceProvider services,
		RuntimeValues runtimeValues,
		bool readRedirectedStdIn)
	{
		return RunCli(
			args,
			logger,
			services,
			runtimeValues,
			services.GetService<CliScenarioRegistry>() ?? CreateDefaultScenarioRegistry(),
			readRedirectedStdIn);
	}

	/// <summary>
	/// Runs the CLI using the supplied scenario registry.
	/// </summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="logger">Logger used for CLI lifecycle events.</param>
	/// <param name="services">Application services.</param>
	/// <param name="runtimeValues">Runtime executable paths.</param>
	/// <param name="scenarioRegistry">Registered CLI scenarios.</param>
	/// <param name="readRedirectedStdIn">Whether redirected standard input should be treated as additional input paths.</param>
	/// <returns>Process exit code.</returns>
	internal static int RunCli(
		string[] args,
		ILogger logger,
		IServiceProvider services,
		RuntimeValues runtimeValues,
		CliScenarioRegistry scenarioRegistry,
		bool readRedirectedStdIn)
	{
		ArgumentNullException.ThrowIfNull(args);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(runtimeValues);
		ArgumentNullException.ThrowIfNull(scenarioRegistry);

		var effectiveArgs = BuildEffectiveArgs(args, readRedirectedStdIn);
		logger.LogInformation(
			"CLI request received. OriginalArgCount={OriginalArgCount} EffectiveArgCount={EffectiveArgCount} ReadRedirectedStdIn={ReadRedirectedStdIn}",
			args.Length,
			effectiveArgs.Length,
			readRedirectedStdIn);

		if (effectiveArgs.Length == 0 || effectiveArgs.Any(IsHelpToken))
		{
			logger.LogInformation("CLI help requested.");
			Console.WriteLine(CliHelpBuilder.BuildHelpText(runtimeValues, scenarioRegistry));
			return 0;
		}

		if (!CliArgumentParser.TryParse(effectiveArgs, scenarioRegistry, out var parsed, out var errorText))
		{
			logger.LogWarning("CLI parse failed: {ErrorText}. Args={Args}", errorText, string.Join(" ", effectiveArgs));
			Console.Error.WriteLine(errorText);
			return 1;
		}

		logger.LogInformation(
			"CLI request parsed. Scenario={Scenario} Info={Info} InputCount={InputCount} ScenarioArgCount={ScenarioArgCount}",
			parsed.Scenario,
			parsed.Info,
			parsed.Inputs.Count,
			parsed.ScenarioArgCount);

		if (parsed.Inputs.Count == 0)
		{
			const string noInputError = "No input files provided. Use --input or pipe file paths to stdin.";
			logger.LogWarning(noInputError);
			Console.Error.WriteLine(noInputError);
			return 1;
		}

		var processor = services.GetRequiredService<ITranscodeProcessor>();
		if (!parsed.Info)
		{
			Console.WriteLine("chcp 65001");
		}

		foreach (var input in parsed.Inputs)
		{
			logger.LogInformation("CLI input processing started. InputPath={InputPath}", input);
			try
			{
				var request = new CliTranscodeRequest(
					inputPath: input,
					scenarioName: parsed.Scenario,
					info: parsed.Info,
					scenarioInput: parsed.ScenarioInput,
					scenarioArgCount: parsed.ScenarioArgCount);
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

	private static CliScenarioRegistry CreateDefaultScenarioRegistry()
	{
		var sampleMeasurer = new FfmpegSampleMeasurer("ffmpeg");
		return new CliScenarioRegistry(
		[
			new ToH264GpuCliScenarioHandler(
				new ToH264GpuInfoFormatter(),
				new ToH264GpuFfmpegTool("ffmpeg", NullLogger<ToH264GpuFfmpegTool>.Instance),
				sampleMeasurer),
			new ToMkvGpuCliScenarioHandler(
				new ToMkvGpuInfoFormatter(),
				new ToMkvGpuFfmpegTool("ffmpeg", NullLogger<ToMkvGpuFfmpegTool>.Instance),
				sampleMeasurer)
		]);
	}
}
