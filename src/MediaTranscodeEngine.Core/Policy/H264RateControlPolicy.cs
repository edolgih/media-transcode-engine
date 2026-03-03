using System.Globalization;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Policy;

public sealed record H264RateControlInput(
    ProbeStream Video,
    bool UseDownscale,
    bool KeepFps,
    int? CqOverride);

public sealed record H264RateControlResult(
    string FpsToken,
    int Gop,
    int Cq);

public sealed class H264RateControlPolicy
{
    public H264RateControlResult Resolve(H264RateControlInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Video);

        var fpsToken = ResolveFpsToken(input.Video, input.UseDownscale, input.KeepFps);
        var fpsValue = ParseFpsToken(fpsToken) ?? 30.0;
        var gop = (int)Math.Max(12, Math.Round(fpsValue * 2.0));

        return new H264RateControlResult(
            FpsToken: fpsToken,
            Gop: gop,
            Cq: input.CqOverride ?? 19);
    }

    private static string ResolveFpsToken(ProbeStream video, bool useDownscale, bool keepFps)
    {
        var sourceFpsToken = video.RFrameRate;
        if (string.IsNullOrWhiteSpace(sourceFpsToken) || sourceFpsToken == "0/0")
        {
            sourceFpsToken = video.AvgFrameRate;
        }

        if (string.IsNullOrWhiteSpace(sourceFpsToken) || sourceFpsToken == "0/0")
        {
            sourceFpsToken = "30/1";
        }

        var sourceFps = ParseFpsToken(sourceFpsToken) ?? 30.0;
        if (useDownscale && !keepFps && sourceFps > 30.0)
        {
            return "30000/1001";
        }

        return sourceFpsToken;
    }

    private static double? ParseFpsToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
            denominator > 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps)
            ? fps
            : null;
    }
}
