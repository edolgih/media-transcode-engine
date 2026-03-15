namespace Transcode.Core.VideoSettings;

/*
Это общая request-модель для video settings.
Она хранит только общие quality/rate overrides и используется и в ordinary encode, и рядом с explicit downscale,
но сама по себе не несет намерения менять разрешение.
*/
/// <summary>
/// Captures reusable video-settings directives independent from a specific scenario.
/// </summary>
public sealed class VideoSettingsRequest
{
    private static readonly string[] SupportedContentProfilesValues =
        [.. VideoSettingsProfiles.Default.GetSupportedContentProfiles()];
    private static readonly string[] SupportedQualityProfilesValues =
        [.. VideoSettingsProfiles.Default.GetSupportedQualityProfiles()];
    private static readonly string[] SupportedAutoSampleModesValues = ["accurate", "fast", "hybrid"];

    /// <summary>
    /// Gets content-profile values supported by the runtime profile catalog.
    /// </summary>
    public static IReadOnlyList<string> SupportedContentProfiles => SupportedContentProfilesValues;

    /// <summary>
    /// Gets quality-profile values supported by the runtime profile catalog.
    /// </summary>
    public static IReadOnlyList<string> SupportedQualityProfiles => SupportedQualityProfilesValues;

    /// <summary>
    /// Gets autosample mode values supported by Runtime.
    /// </summary>
    public static IReadOnlyList<string> SupportedAutoSampleModes => SupportedAutoSampleModesValues;

    /// <summary>
    /// Initializes reusable video-settings directives.
    /// </summary>
    /// <param name="contentProfile">Requested content profile for profile-driven video settings.</param>
    /// <param name="qualityProfile">Requested quality profile for profile-driven video settings.</param>
    /// <param name="autoSampleMode">Requested autosample mode.</param>
    /// <param name="cq">Explicit CQ override.</param>
    /// <param name="maxrate">Explicit maxrate override in Mbit/s.</param>
    /// <param name="bufsize">Explicit bufsize override in Mbit/s.</param>
    /// <exception cref="ArgumentException">Thrown when no overrides are supplied.</exception>
    public VideoSettingsRequest(
        string? contentProfile = null,
        string? qualityProfile = null,
        string? autoSampleMode = null,
        int? cq = null,
        decimal? maxrate = null,
        decimal? bufsize = null)
    {
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

        ContentProfile = NormalizeSupportedValue(
            contentProfile,
            nameof(contentProfile),
            SupportedContentProfilesValues,
            GetSupportedValuesDisplay(SupportedContentProfilesValues));
        QualityProfile = NormalizeSupportedValue(
            qualityProfile,
            nameof(qualityProfile),
            SupportedQualityProfilesValues,
            GetSupportedValuesDisplay(SupportedQualityProfilesValues));
        AutoSampleMode = NormalizeSupportedValue(
            autoSampleMode,
            nameof(autoSampleMode),
            SupportedAutoSampleModesValues,
            GetSupportedValuesDisplay(SupportedAutoSampleModesValues));
        Cq = cq;
        Maxrate = maxrate;
        Bufsize = bufsize;

        if (!HasAnyValue(ContentProfile, QualityProfile, AutoSampleMode, Cq, Maxrate, Bufsize))
        {
            throw new ArgumentException("At least one video settings override is required.");
        }
    }

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
    /// Creates a request when at least one override is provided; otherwise returns <see langword="null"/>.
    /// </summary>
    public static VideoSettingsRequest? CreateOrNull(
        string? contentProfile = null,
        string? qualityProfile = null,
        string? autoSampleMode = null,
        int? cq = null,
        decimal? maxrate = null,
        decimal? bufsize = null)
    {
        return HasAnyValue(contentProfile, qualityProfile, autoSampleMode, cq, maxrate, bufsize)
            ? new VideoSettingsRequest(contentProfile, qualityProfile, autoSampleMode, cq, maxrate, bufsize)
            : null;
    }

    /// <summary>
    /// Determines whether the supplied content-profile value is supported.
    /// </summary>
    public static bool IsSupportedContentProfile(string? value)
    {
        return IsSupportedValue(value, SupportedContentProfilesValues);
    }

    /// <summary>
    /// Determines whether the supplied quality-profile value is supported.
    /// </summary>
    public static bool IsSupportedQualityProfile(string? value)
    {
        return IsSupportedValue(value, SupportedQualityProfilesValues);
    }

    /// <summary>
    /// Determines whether the supplied autosample mode value is supported.
    /// </summary>
    public static bool IsSupportedAutoSampleMode(string? value)
    {
        return IsSupportedValue(value, SupportedAutoSampleModesValues);
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static bool IsSupportedValue(string? value, IReadOnlyList<string> supportedValues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return supportedValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static string GetSupportedValuesDisplay(IReadOnlyList<string> supportedValues)
    {
        return string.Join(", ", supportedValues);
    }

    private static string? NormalizeSupportedValue(
        string? value,
        string paramName,
        IReadOnlyList<string> supportedValues,
        string display)
    {
        var normalizedValue = NormalizeName(value);
        if (normalizedValue is null)
        {
            return null;
        }

        if (!supportedValues.Contains(normalizedValue, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {display}.");
        }

        return normalizedValue;
    }

    private static bool HasAnyValue(
        string? contentProfile,
        string? qualityProfile,
        string? autoSampleMode,
        int? cq,
        decimal? maxrate,
        decimal? bufsize)
    {
        return !string.IsNullOrWhiteSpace(contentProfile) ||
               !string.IsNullOrWhiteSpace(qualityProfile) ||
               !string.IsNullOrWhiteSpace(autoSampleMode) ||
               cq.HasValue ||
               maxrate.HasValue ||
               bufsize.HasValue;
    }
}
