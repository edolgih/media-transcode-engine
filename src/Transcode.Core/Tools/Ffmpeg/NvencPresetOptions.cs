namespace Transcode.Core.Tools.Ffmpeg;

/*
Это общий источник supported NVENC preset values.
Он нужен, чтобы request parsing и CLI help брали один и тот же набор значений.
*/
/// <summary>
/// Provides supported NVENC preset values shared by Runtime and CLI help.
/// </summary>
public static class NvencPresetOptions
{
    public const string P1 = "p1";
    public const string P2 = "p2";
    public const string P3 = "p3";
    public const string P4 = "p4";
    public const string P5 = "p5";
    public const string P6 = "p6";
    public const string P7 = "p7";

    private static readonly string[] SupportedPresetsValues = [P1, P2, P3, P4, P5, P6, P7];

    /// <summary>
    /// Gets the canonical NVENC preset values supported by Runtime.
    /// </summary>
    public static IReadOnlyList<string> SupportedPresets => SupportedPresetsValues;

    /// <summary>
    /// Gets the default NVENC preset used by Runtime when the caller did not override it.
    /// </summary>
    public static string DefaultPreset => P6;

    /// <summary>
    /// Determines whether the supplied NVENC preset value is supported.
    /// </summary>
    public static bool IsSupportedPreset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return SupportedPresetsValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
