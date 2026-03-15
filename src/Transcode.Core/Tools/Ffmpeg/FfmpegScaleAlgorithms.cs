namespace Transcode.Core.Tools.Ffmpeg;

/*
Это общий каталог canonical ffmpeg scale algorithm values.
Он нужен, чтобы request validation, profile defaults и CLI help не дублировали одни и те же строки.
*/
/// <summary>
/// Provides supported ffmpeg scale algorithm values shared by Runtime and CLI help.
/// </summary>
public static class FfmpegScaleAlgorithms
{
    public const string Bilinear = "bilinear";
    public const string Bicubic = "bicubic";
    public const string Lanczos = "lanczos";

    private static readonly string[] SupportedAlgorithmsValues = [Bilinear, Bicubic, Lanczos];

    /// <summary>
    /// Gets the canonical scale algorithm values supported by Runtime.
    /// </summary>
    public static IReadOnlyList<string> SupportedAlgorithms => SupportedAlgorithmsValues;

    /// <summary>
    /// Determines whether the supplied scale algorithm value is supported.
    /// </summary>
    public static bool IsSupported(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return SupportedAlgorithmsValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
