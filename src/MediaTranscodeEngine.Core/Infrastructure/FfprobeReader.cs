using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaTranscodeEngine.Core.Infrastructure;

public sealed class FfprobeReader : IProbeReader
{
    private readonly IProcessRunner _processRunner;
    private readonly string _ffprobePath;
    private readonly int _timeoutMs;
    private readonly ILogger<FfprobeReader> _logger;

    public FfprobeReader(
        IProcessRunner processRunner,
        string ffprobePath = "ffprobe",
        int timeoutMs = 30_000,
        ILogger<FfprobeReader>? logger = null)
    {
        _processRunner = processRunner;
        _ffprobePath = ffprobePath;
        _timeoutMs = timeoutMs;
        _logger = logger ?? NullLogger<FfprobeReader>.Instance;
    }

    public ProbeResult? Read(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var arguments = $"-v error -print_format json -show_format -show_streams {Quote(inputPath)}";
        _logger.LogDebug("Running ffprobe for {InputPath}", inputPath);
        var run = _processRunner.Run(_ffprobePath, arguments, _timeoutMs);

        if (run.ExitCode != 0 || string.IsNullOrWhiteSpace(run.StdOut))
        {
            _logger.LogWarning(
                "ffprobe failed for {InputPath}. ExitCode={ExitCode}. StdErr={StdErr}",
                inputPath,
                run.ExitCode,
                run.StdErr);
            return null;
        }

        var probe = ProbeJsonParser.Parse(run.StdOut);
        if (probe is null)
        {
            _logger.LogWarning("ffprobe output could not be parsed for {InputPath}", inputPath);
            return null;
        }

        _logger.LogDebug(
            "ffprobe succeeded for {InputPath}. Streams={StreamCount}",
            inputPath,
            probe.Streams.Count);
        return probe;
    }

    private static string Quote(string value)
    {
        var escaped = value.Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
}
