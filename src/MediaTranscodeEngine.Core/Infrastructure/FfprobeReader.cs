using System.Globalization;
using System.Text.Json;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Infrastructure;

public sealed class FfprobeReader : IProbeReader
{
    private readonly IProcessRunner _processRunner;
    private readonly string _ffprobePath;
    private readonly int _timeoutMs;

    public FfprobeReader(
        IProcessRunner processRunner,
        string ffprobePath = "ffprobe",
        int timeoutMs = 30_000)
    {
        _processRunner = processRunner;
        _ffprobePath = ffprobePath;
        _timeoutMs = timeoutMs;
    }

    public ProbeResult? Read(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var arguments = $"-v error -print_format json -show_format -show_streams {Quote(inputPath)}";
        var run = _processRunner.Run(_ffprobePath, arguments, _timeoutMs);

        if (run.ExitCode != 0 || string.IsNullOrWhiteSpace(run.StdOut))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(run.StdOut);
            var root = doc.RootElement;

            var format = ParseFormat(root);
            var streams = ParseStreams(root);

            return new ProbeResult(
                Format: format,
                Streams: streams);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ProbeFormat? ParseFormat(JsonElement root)
    {
        if (!root.TryGetProperty("format", out var formatElement))
        {
            return null;
        }

        return new ProbeFormat(
            DurationSeconds: TryGetDouble(formatElement, "duration"),
            BitrateBps: TryGetDouble(formatElement, "bit_rate"));
    }

    private static IReadOnlyList<ProbeStream> ParseStreams(JsonElement root)
    {
        if (!root.TryGetProperty("streams", out var streamsElement) ||
            streamsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<ProbeStream>();
        foreach (var streamElement in streamsElement.EnumerateArray())
        {
            var codecType = TryGetString(streamElement, "codec_type");
            var codecName = TryGetString(streamElement, "codec_name");

            if (string.IsNullOrWhiteSpace(codecType) || string.IsNullOrWhiteSpace(codecName))
            {
                continue;
            }

            result.Add(new ProbeStream(
                CodecType: codecType,
                CodecName: codecName,
                Width: TryGetInt(streamElement, "width"),
                Height: TryGetInt(streamElement, "height"),
                BitrateBps: TryGetDouble(streamElement, "bit_rate")));
        }

        return result;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        var token = TryGetString(element, propertyName);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
    {
        var token = TryGetString(element, propertyName);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string Quote(string value)
    {
        var escaped = value.Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
}
