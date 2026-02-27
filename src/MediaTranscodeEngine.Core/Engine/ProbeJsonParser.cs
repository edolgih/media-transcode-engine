using System.Globalization;
using System.Text.Json;

namespace MediaTranscodeEngine.Core.Engine;

public static class ProbeJsonParser
{
    public static ProbeResult? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new ProbeResult(
                Format: ParseFormat(root),
                Streams: ParseStreams(root));
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
            BitrateBps: TryGetDouble(formatElement, "bit_rate"),
            FormatName: TryGetString(formatElement, "format_name"));
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
                BitrateBps: TryGetDouble(streamElement, "bit_rate"),
                RFrameRate: TryGetString(streamElement, "r_frame_rate"),
                AvgFrameRate: TryGetString(streamElement, "avg_frame_rate")));
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
}
