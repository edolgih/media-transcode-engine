namespace MediaTranscodeEngine.Runtime.VideoSettings;

/*
Это общая request-модель для video settings.
Она описывает целевую высоту и возможные overrides профиля без привязки к конкретному сценарию.
*/
/// <summary>
/// Captures reusable video-settings directives independent from a specific scenario.
/// </summary>
public sealed class VideoSettingsRequest
{
    /// <summary>
    /// Initializes reusable video-settings directives.
    /// </summary>
    /// <param name="targetHeight">Requested target height.</param>
    /// <param name="contentProfile">Requested content profile for profile-driven video settings.</param>
    /// <param name="qualityProfile">Requested quality profile for profile-driven video settings.</param>
    /// <param name="autoSampleMode">Requested autosample mode.</param>
    /// <param name="algorithm">Explicit scaling algorithm override.</param>
    /// <param name="cq">Explicit CQ override.</param>
    /// <param name="maxrate">Explicit maxrate override in Mbit/s.</param>
    /// <param name="bufsize">Explicit bufsize override in Mbit/s.</param>
    public VideoSettingsRequest(
        int? targetHeight = null,
        string? contentProfile = null,
        string? qualityProfile = null,
        string? autoSampleMode = null,
        string? algorithm = null,
        int? cq = null,
        decimal? maxrate = null,
        decimal? bufsize = null)
    {
        if (targetHeight.HasValue && targetHeight.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetHeight), targetHeight.Value, "Target height must be greater than zero.");
        }

        if (cq.HasValue && cq.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cq), cq.Value, "CQ must be greater than zero.");
        }

        if (maxrate.HasValue && maxrate.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(maxrate), maxrate.Value, "Maxrate must be greater than zero.");
        }

        if (bufsize.HasValue && bufsize.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(bufsize), bufsize.Value, "Bufsize must be greater than zero.");
        }

        TargetHeight = targetHeight;
        ContentProfile = NormalizeName(contentProfile);
        QualityProfile = NormalizeName(qualityProfile);
        AutoSampleMode = NormalizeName(autoSampleMode);
        Algorithm = NormalizeName(algorithm);
        Cq = cq;
        Maxrate = maxrate;
        Bufsize = bufsize;
    }

    /// <summary>
    /// Gets the requested target height.
    /// </summary>
    public int? TargetHeight { get; }

    /// <summary>
    /// Gets the requested content profile.
    /// </summary>
    public string? ContentProfile { get; }

    /// <summary>
    /// Gets the requested quality profile.
    /// </summary>
    public string? QualityProfile { get; }

    /// <summary>
    /// Gets the requested autosample mode.
    /// </summary>
    public string? AutoSampleMode { get; }

    /// <summary>
    /// Gets the explicit scaling algorithm override.
    /// </summary>
    public string? Algorithm { get; }

    /// <summary>
    /// Gets the explicit CQ override.
    /// </summary>
    public int? Cq { get; }

    /// <summary>
    /// Gets the explicit maxrate override in Mbit/s.
    /// </summary>
    public decimal? Maxrate { get; }

    /// <summary>
    /// Gets the explicit bufsize override in Mbit/s.
    /// </summary>
    public decimal? Bufsize { get; }

    /// <summary>
    /// Gets a value indicating whether any video-settings directive is actually present.
    /// </summary>
    public bool HasValue =>
        TargetHeight.HasValue ||
        !string.IsNullOrWhiteSpace(ContentProfile) ||
        !string.IsNullOrWhiteSpace(QualityProfile) ||
        !string.IsNullOrWhiteSpace(AutoSampleMode) ||
        !string.IsNullOrWhiteSpace(Algorithm) ||
        Cq.HasValue ||
        Maxrate.HasValue ||
        Bufsize.HasValue;

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
