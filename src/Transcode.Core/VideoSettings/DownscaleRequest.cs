using Transcode.Core.Tools.Ffmpeg;

namespace Transcode.Core.VideoSettings;

/*
Это явное намерение downscale.
Отдельный тип нужен, чтобы факт изменения высоты не угадывался по полям общего VideoSettingsRequest.
Если owner сценария уже выбрал effective algorithm, этот же value object может нести и его.
*/
/// <summary>
/// Captures downscale intent together with an optional scaling algorithm.
/// </summary>
public sealed class DownscaleRequest
{
    private static readonly int[] SupportedTargetHeightsValues =
        [.. VideoSettingsProfiles.Default.GetSupportedDownscaleTargetHeights()];

    /// <summary>
    /// Gets target heights that are supported by configured downscale profiles.
    /// </summary>
    public static IReadOnlyList<int> SupportedTargetHeights => SupportedTargetHeightsValues;

    /// <summary>
    /// Gets the canonical scaling algorithm values supported by Runtime.
    /// </summary>
    public static IReadOnlyList<string> SupportedAlgorithms => FfmpegScaleAlgorithms.SupportedAlgorithms;

    /// <summary>
    /// Initializes explicit downscale directives.
    /// </summary>
    /// <param name="targetHeight">Requested target height.</param>
    /// <param name="algorithm">Scaling algorithm when already resolved by the owner, or an explicit override.</param>
    public DownscaleRequest(int targetHeight, string? algorithm = null)
    {
        if (targetHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetHeight), targetHeight, "Target height must be greater than zero.");
        }

        if (!IsSupportedTargetHeight(targetHeight))
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetHeight),
                targetHeight,
                $"Supported values: {GetSupportedTargetHeightsDisplay()}.");
        }

        var normalizedAlgorithm = NormalizeName(algorithm);
        if (normalizedAlgorithm is not null && !IsSupportedAlgorithm(normalizedAlgorithm))
        {
            throw new ArgumentOutOfRangeException(
                nameof(algorithm),
                algorithm,
                $"Supported values: {GetSupportedAlgorithmsDisplay()}.");
        }

        TargetHeight = targetHeight;
        Algorithm = normalizedAlgorithm;
    }

    /// <summary>
    /// Gets the requested target height.
    /// </summary>
    public int TargetHeight { get; }

    /// <summary>
    /// Gets the scaling algorithm carried by this request.
    /// Null means the owner kept only resize intent and has not resolved a default algorithm yet.
    /// </summary>
    public string? Algorithm { get; }

    /// <summary>
    /// Returns a request with the supplied default algorithm when no algorithm has been chosen yet.
    /// </summary>
    /// <param name="algorithm">Default scaling algorithm selected by the owner.</param>
    /// <returns>The current request when the algorithm is already present; otherwise a normalized copy.</returns>
    public DownscaleRequest WithDefaultAlgorithm(string algorithm)
    {
        var normalizedAlgorithm = NormalizeName(algorithm);
        if (normalizedAlgorithm is null)
        {
            throw new ArgumentException("Algorithm is required.", nameof(algorithm));
        }

        return Algorithm is not null
            ? this
            : new DownscaleRequest(TargetHeight, normalizedAlgorithm);
    }

    /// <summary>
    /// Determines whether the supplied target height is supported by configured downscale profiles.
    /// </summary>
    public static bool IsSupportedTargetHeight(int targetHeight)
    {
        return Array.IndexOf(SupportedTargetHeightsValues, targetHeight) >= 0;
    }

    /// <summary>
    /// Determines whether the supplied scaling algorithm is supported.
    /// </summary>
    public static bool IsSupportedAlgorithm(string? value)
    {
        return FfmpegScaleAlgorithms.IsSupported(value);
    }

    private static string GetSupportedTargetHeightsDisplay()
    {
        return string.Join(", ", SupportedTargetHeightsValues);
    }

    private static string GetSupportedAlgorithmsDisplay()
    {
        return string.Join(", ", FfmpegScaleAlgorithms.SupportedAlgorithms);
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
