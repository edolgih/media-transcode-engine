using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace MediaTranscodeEngine.Runtime.Inspection;

/// <summary>
/// Reads raw probe metadata from a real ffprobe process.
/// </summary>
public sealed class FfprobeVideoProbe : IVideoProbe
{
    private readonly Func<string, FfprobeProcessResult> _executeProbe;

    /// <summary>
    /// Initializes an ffprobe-backed probe adapter.
    /// </summary>
    /// <param name="ffprobePath">Executable path or command name for ffprobe.</param>
    public FfprobeVideoProbe(string ffprobePath = "ffprobe")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffprobePath);

        var normalizedFfprobePath = ffprobePath.Trim();
        _executeProbe = filePath => ExecuteProcess(normalizedFfprobePath, filePath);
    }

    internal FfprobeVideoProbe(Func<string, FfprobeProcessResult> executeProbe)
    {
        _executeProbe = executeProbe ?? throw new ArgumentNullException(nameof(executeProbe));
    }

    /// <inheritdoc />
    public VideoProbeSnapshot Probe(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = Path.GetFullPath(filePath.Trim());
        var run = _executeProbe(normalizedPath);

        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildProcessFailureMessage(run.ExitCode, run.StandardError));
        }

        if (string.IsNullOrWhiteSpace(run.StandardOutput))
        {
            throw new InvalidOperationException("ffprobe returned empty JSON output.");
        }

        try
        {
            return ParseSnapshot(normalizedPath, run.StandardOutput);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("ffprobe returned invalid JSON output.", exception);
        }
    }

    private static VideoProbeSnapshot ParseSnapshot(string filePath, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("streams", out var streamsElement) ||
            streamsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("ffprobe JSON is missing required field 'streams'.");
        }

        var streams = new List<VideoProbeStream>();
        foreach (var streamElement in streamsElement.EnumerateArray())
        {
            if (streamElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("ffprobe JSON contained an invalid stream entry.");
            }

            var streamType = ReadRequiredString(streamElement, "codec_type", "stream");
            var codec = ReadRequiredString(streamElement, "codec_name", "stream");
            var framesPerSecond = ParseFrameRate(TryGetString(streamElement, "r_frame_rate")) ??
                                  ParseFrameRate(TryGetString(streamElement, "avg_frame_rate"));

            streams.Add(new VideoProbeStream(
                streamType: streamType,
                codec: codec,
                width: TryGetInt(streamElement, "width"),
                height: TryGetInt(streamElement, "height"),
                framesPerSecond: framesPerSecond,
                bitrate: TryGetLong(streamElement, "bit_rate")));
        }

        var duration = TryGetDuration(root);
        var formatBitrate = TryGetFormatBitrate(root);
        var container = ResolveContainer(filePath, root);

        return new VideoProbeSnapshot(
            container: container,
            streams: streams,
            duration: duration,
            formatBitrate: formatBitrate);
    }

    private static string? ResolveContainer(string filePath, JsonElement root)
    {
        var fileExtension = Path.GetExtension(filePath).Trim().TrimStart('.').ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(fileExtension))
        {
            return fileExtension;
        }

        if (!root.TryGetProperty("format", out var formatElement) ||
            formatElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var formatName = TryGetString(formatElement, "format_name");
        if (string.IsNullOrWhiteSpace(formatName))
        {
            return null;
        }

        var firstToken = formatName
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(firstToken)
            ? null
            : firstToken.ToLowerInvariant();
    }

    private static TimeSpan? TryGetDuration(JsonElement root)
    {
        if (!root.TryGetProperty("format", out var formatElement) ||
            formatElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var token = TryGetString(formatElement, "duration");
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < 0)
        {
            return null;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static long? TryGetFormatBitrate(JsonElement root)
    {
        if (!root.TryGetProperty("format", out var formatElement) ||
            formatElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryGetLong(formatElement, "bit_rate");
    }

    private static string ReadRequiredString(JsonElement element, string propertyName, string scope)
    {
        var value = TryGetString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"ffprobe JSON is missing required field '{propertyName}' in {scope}.");
        }

        return value.Trim();
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

    private static long? TryGetLong(JsonElement element, string propertyName)
    {
        var token = TryGetString(element, propertyName);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? ParseFrameRate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var normalizedToken = token.Trim();
        if (double.TryParse(normalizedToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed > 0
                ? parsed
                : null;
        }

        var separatorIndex = normalizedToken.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex >= normalizedToken.Length - 1)
        {
            return null;
        }

        var numeratorToken = normalizedToken[..separatorIndex];
        var denominatorToken = normalizedToken[(separatorIndex + 1)..];

        if (!double.TryParse(numeratorToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) ||
            !double.TryParse(denominatorToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) ||
            denominator <= 0)
        {
            return null;
        }

        var framesPerSecond = numerator / denominator;
        return framesPerSecond > 0
            ? framesPerSecond
            : null;
    }

    private static FfprobeProcessResult ExecuteProcess(string ffprobePath, string filePath)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(ffprobePath, filePath)
        };

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException("ffprobe process failed to start.", exception);
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        return new FfprobeProcessResult(
            ExitCode: process.ExitCode,
            StandardOutput: standardOutputTask.GetAwaiter().GetResult(),
            StandardError: standardErrorTask.GetAwaiter().GetResult());
    }

    private static ProcessStartInfo CreateStartInfo(string ffprobePath, string filePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffprobePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-print_format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("-show_format");
        startInfo.ArgumentList.Add("-show_streams");
        startInfo.ArgumentList.Add(filePath);

        return startInfo;
    }

    private static string BuildProcessFailureMessage(int exitCode, string standardError)
    {
        if (string.IsNullOrWhiteSpace(standardError))
        {
            return $"ffprobe process failed. ExitCode={exitCode}.";
        }

        return $"ffprobe process failed. ExitCode={exitCode}. StdErr={standardError.Trim()}";
    }
}

internal sealed record FfprobeProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
