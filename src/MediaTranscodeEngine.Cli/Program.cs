using MediaTranscodeEngine.Cli;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Policy;

var app = new CliApp();
return app.Run(args);

internal sealed class CliApp
{
    public int Run(string[] args)
    {
        if (args.Length == 0)
        {
            WriteRootHelp();
            return 0;
        }

        var command = args[0];
        if (command.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("-h", StringComparison.OrdinalIgnoreCase))
        {
            WriteRootHelp();
            return 0;
        }

        if (!command.Equals("tomkvgpu", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unsupported command: {command}");
            WriteRootHelp();
            return 1;
        }

        var parser = new ToMkvCliArgumentParser();
        var parse = parser.Parse(args.Skip(1).ToArray());
        if (parse.ShowHelp)
        {
            WriteToMkvHelp();
            return 0;
        }

        if (!parse.IsValid || parse.Options is null)
        {
            Console.Error.WriteLine(parse.ErrorMessage ?? "Invalid arguments.");
            WriteToMkvHelp();
            return 1;
        }

        var options = parse.Options;
        var inputPaths = options.Inputs.ToList();
        if (Console.IsInputRedirected)
        {
            inputPaths.AddRange(ReadInputPathsFromStdIn());
        }

        if (inputPaths.Count == 0)
        {
            Console.Error.WriteLine("No input files provided. Use --input or pipe file paths to stdin.");
            return 1;
        }

        IProfileRepository profileRepository = string.IsNullOrWhiteSpace(options.ProfilesYamlPath)
            ? new StaticProfileRepository()
            : new YamlProfileRepository(options.ProfilesYamlPath);

        var processRunner = new ProcessRunner();
        var probeReader = new FfprobeReader(processRunner, "ffprobe", 30_000);
        var autoSampleReductionProvider = new FfmpegAutoSampleReductionProvider(
            probeReader,
            processRunner,
            ffmpegPath: "ffmpeg",
            timeoutMs: 30_000);

        var engine = new TranscodeEngine(
            probeReader: probeReader,
            profileRepository: profileRepository,
            policy: new TranscodePolicy(),
            commandBuilder: new FfmpegCommandBuilder(),
            autoSampleReductionProvider: autoSampleReductionProvider);

        foreach (var path in inputPaths)
        {
            var request = new TranscodeRequest(
                InputPath: path,
                Info: options.Info,
                OverlayBg: options.OverlayBg,
                Downscale: options.Downscale,
                DownscaleAlgoOverride: options.DownscaleAlgo,
                ContentProfile: options.ContentProfile,
                QualityProfile: options.QualityProfile,
                NoAutoSample: options.NoAutoSample,
                AutoSampleMode: options.AutoSampleMode,
                SyncAudio: options.SyncAudio,
                CqOverride: options.Cq,
                MaxrateOverride: options.Maxrate,
                BufsizeOverride: options.Bufsize,
                NvencPreset: options.NvencPreset,
                ForceVideoEncode: options.ForceVideoEncode);

            var line = engine.Process(request);
            if (!string.IsNullOrWhiteSpace(line))
            {
                Console.WriteLine(line);
            }
        }

        return 0;
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

    private static void WriteRootHelp()
    {
        Console.WriteLine("MediaTranscodeEngine CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  tomkvgpu   Generate ffmpeg commands for ToMkvGPU flow.");
        Console.WriteLine();
        Console.WriteLine("Use 'MediaTranscodeEngine.Cli tomkvgpu --help' for command options.");
    }

    private static void WriteToMkvHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  MediaTranscodeEngine.Cli tomkvgpu [options] [--input <path> ...]");
        Console.WriteLine("  <producer> | MediaTranscodeEngine.Cli tomkvgpu [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --input <path>               Add input file path (repeatable).");
        Console.WriteLine("  --info                       Info mode (no ffmpeg command).");
        Console.WriteLine("  --overlay-bg                 Force overlay background mode.");
        Console.WriteLine("  --downscale <576|720>        Enable downscale path.");
        Console.WriteLine("  --downscale-algo <value>     bicubic|lanczos|bilinear.");
        Console.WriteLine("  --content-profile <value>    anime|mult|film. Default: film.");
        Console.WriteLine("  --quality-profile <value>    high|default|low. Default: default.");
        Console.WriteLine("  --no-auto-sample             Disable auto-sample for 576 path.");
        Console.WriteLine("  --auto-sample-mode <value>   accurate|fast|hybrid. Default: accurate.");
        Console.WriteLine("  --sync-audio                 Force audio resample path.");
        Console.WriteLine("  --force-video-encode         Force video encode even if copyable.");
        Console.WriteLine("  --cq <int>                   CQ override.");
        Console.WriteLine("  --maxrate <double>           Maxrate override (Mbps).");
        Console.WriteLine("  --bufsize <double>           Bufsize override (Mbps).");
        Console.WriteLine("  --nvenc-preset <value>       p1..p7. Default: p6.");
        Console.WriteLine("  --profiles-yaml <path>       Optional profile yaml override.");
        Console.WriteLine("  -h, --help                   Show this help.");
    }
}
