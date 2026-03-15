namespace Transcode.Core.VideoSettings;

/*
Это fully resolved selection для profile-driven video settings.
Он отделяет selection defaults от raw override-request и не допускает nullable в profile-layer.
*/
/// <summary>
/// Represents the resolved non-null profile selection used inside the video-settings pipeline.
/// </summary>
internal sealed record EffectiveVideoSettingsSelection(
    string ContentProfile,
    string QualityProfile,
    string AutoSampleMode)
{
    public string ContentProfile { get; init; } = NormalizeSupportedValue(
        ContentProfile,
        nameof(ContentProfile),
        VideoSettingsRequest.IsSupportedContentProfile,
        VideoSettingsRequest.SupportedContentProfiles);

    public string QualityProfile { get; init; } = NormalizeSupportedValue(
        QualityProfile,
        nameof(QualityProfile),
        VideoSettingsRequest.IsSupportedQualityProfile,
        VideoSettingsRequest.SupportedQualityProfiles);

    public string AutoSampleMode { get; init; } = NormalizeSupportedValue(
        AutoSampleMode,
        nameof(AutoSampleMode),
        VideoSettingsRequest.IsSupportedAutoSampleMode,
        VideoSettingsRequest.SupportedAutoSampleModes);

    private static string NormalizeSupportedValue(
        string? value,
        string paramName,
        Func<string?, bool> isSupported,
        IReadOnlyList<string> supportedValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        var normalized = value.Trim().ToLowerInvariant();
        if (!isSupported(normalized))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {string.Join(", ", supportedValues)}.");
        }

        return normalized;
    }
}
