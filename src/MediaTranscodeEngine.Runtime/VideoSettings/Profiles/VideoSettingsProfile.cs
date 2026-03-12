using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.VideoSettings.Profiles;

/*
Это один профиль video settings для конкретной целевой высоты.
Он хранит defaults, autosample-правила и source-height buckets.
*/
/// <summary>
/// Represents one typed video-settings profile keyed by target height.
/// </summary>
internal sealed class VideoSettingsProfile
{
    private readonly IReadOnlyDictionary<string, VideoSettingsDefaults> _defaultsByProfile;
    private readonly IReadOnlyDictionary<string, VideoSettingsRange> _globalContentRangesByProfile;
    private readonly IReadOnlyDictionary<string, VideoSettingsQualityRange> _globalQualityRangesByProfile;
    private readonly string[] _supportedContentProfiles;
    private readonly string[] _supportedQualityProfiles;

    public VideoSettingsProfile(
        int targetHeight,
        string defaultContentProfile,
        string defaultQualityProfile,
        VideoSettingsRateModel rateModel,
        VideoSettingsAutoSampling autoSampling,
        IReadOnlyList<SourceHeightBucket> sourceBuckets,
        IReadOnlyList<VideoSettingsDefaults> defaults,
        IReadOnlyList<VideoSettingsRange>? globalContentRanges = null,
        IReadOnlyList<VideoSettingsQualityRange>? globalQualityRanges = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultContentProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultQualityProfile);
        ArgumentNullException.ThrowIfNull(rateModel);
        ArgumentNullException.ThrowIfNull(autoSampling);

        TargetHeight = targetHeight;
        DefaultContentProfile = defaultContentProfile.Trim().ToLowerInvariant();
        DefaultQualityProfile = defaultQualityProfile.Trim().ToLowerInvariant();
        RateModel = rateModel;
        AutoSampling = autoSampling;
        SourceBuckets = sourceBuckets;
        Defaults = defaults;
        GlobalContentRanges = globalContentRanges ?? Array.Empty<VideoSettingsRange>();
        GlobalQualityRanges = globalQualityRanges ?? Array.Empty<VideoSettingsQualityRange>();
        _defaultsByProfile = defaults.ToDictionary(
            static entry => BuildDefaultsKey(entry.ContentProfile, entry.QualityProfile),
            StringComparer.OrdinalIgnoreCase);
        _globalContentRangesByProfile = GlobalContentRanges.ToDictionary(
            static entry => BuildDefaultsKey(entry.ContentProfile, entry.QualityProfile),
            StringComparer.OrdinalIgnoreCase);
        _globalQualityRangesByProfile = GlobalQualityRanges.ToDictionary(
            static entry => entry.QualityProfile,
            StringComparer.OrdinalIgnoreCase);
        _supportedContentProfiles = defaults.Select(static entry => entry.ContentProfile).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _supportedQualityProfiles = defaults.Select(static entry => entry.QualityProfile).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public int TargetHeight { get; }

    public string DefaultContentProfile { get; }

    public string DefaultQualityProfile { get; }

    public VideoSettingsRateModel RateModel { get; }

    public VideoSettingsAutoSampling AutoSampling { get; }

    public IReadOnlyList<SourceHeightBucket> SourceBuckets { get; }

    public IReadOnlyList<VideoSettingsDefaults> Defaults { get; }

    public IReadOnlyList<VideoSettingsRange> GlobalContentRanges { get; }

    public IReadOnlyList<VideoSettingsQualityRange> GlobalQualityRanges { get; }

    public string? ResolveSourceBucket(int? sourceHeight)
    {
        return ResolveSourceBucketDefinition(sourceHeight)?.Name;
    }

    public string? ResolveSourceBucketIssue(int? sourceHeight)
    {
        if (!sourceHeight.HasValue)
        {
            var fallbackBucket = ResolveSourceBucketDefinition(sourceHeight);
            if (fallbackBucket is null)
            {
                return $"{TargetHeight} source bucket missing: height is unknown; add SourceBuckets";
            }

            var fallbackIssue = fallbackBucket.ResolveMissingRange(_supportedContentProfiles, _supportedQualityProfiles);
            if (fallbackIssue is not null)
            {
                return $"{TargetHeight} source bucket invalid: missing corridor '{fallbackIssue}'";
            }

            return null;
        }

        var bucket = ResolveSourceBucketDefinition(sourceHeight);
        if (bucket is null)
        {
            return $"{TargetHeight} source bucket missing: height {sourceHeight.Value}; add SourceBuckets";
        }

        var missingRange = bucket.ResolveMissingRange(_supportedContentProfiles, _supportedQualityProfiles);
        if (missingRange is not null)
        {
            return $"{TargetHeight} source bucket invalid: missing corridor '{missingRange}'";
        }

        return null;
    }

    public VideoSettingsDefaults ResolveDefaults(string? contentProfile, string? qualityProfile)
    {
        return ResolveDefaults(sourceHeight: null, contentProfile, qualityProfile);
    }

    public VideoSettingsDefaults ResolveDefaults(int? sourceHeight, string? contentProfile, string? qualityProfile)
    {
        var effectiveContentProfile = NormalizeProfileName(contentProfile) ?? DefaultContentProfile;
        var effectiveQualityProfile = NormalizeProfileName(qualityProfile) ?? DefaultQualityProfile;
        var key = BuildDefaultsKey(effectiveContentProfile, effectiveQualityProfile);
        if (_defaultsByProfile.TryGetValue(key, out var defaults))
        {
            var boundsOverride = ResolveSourceBucketDefinition(sourceHeight)?.ResolveBoundsOverride(effectiveContentProfile, effectiveQualityProfile);
            return boundsOverride is null
                ? defaults
                : defaults with
                {
                    CqMin = boundsOverride.CqMin ?? defaults.CqMin,
                    CqMax = boundsOverride.CqMax ?? defaults.CqMax,
                    MaxrateMin = boundsOverride.MaxrateMin ?? defaults.MaxrateMin,
                    MaxrateMax = boundsOverride.MaxrateMax ?? defaults.MaxrateMax
                };
        }

        throw new InvalidOperationException(
            $"Video settings defaults are not configured for content '{effectiveContentProfile}' and quality '{effectiveQualityProfile}'.");
    }

    public IReadOnlyList<VideoSettingsSampleWindow> GetSampleWindows(TimeSpan duration)
    {
        return AutoSampling.GetSampleWindows(duration);
    }

    public VideoSettingsRange? ResolveRange(int? sourceHeight, string? contentProfile, string? qualityProfile)
    {
        var effectiveContentProfile = NormalizeProfileName(contentProfile) ?? DefaultContentProfile;
        var effectiveQualityProfile = NormalizeProfileName(qualityProfile) ?? DefaultQualityProfile;
        var bucket = ResolveSourceBucketDefinition(sourceHeight);
        var bucketRange = bucket?.ResolveRange(effectiveContentProfile, effectiveQualityProfile);
        if (bucketRange is not null)
        {
            return bucketRange;
        }

        var key = BuildDefaultsKey(effectiveContentProfile, effectiveQualityProfile);
        if (_globalContentRangesByProfile.TryGetValue(key, out var globalContentRange))
        {
            return globalContentRange;
        }

        if (_globalQualityRangesByProfile.TryGetValue(effectiveQualityProfile, out var globalQualityRange))
        {
            return globalQualityRange.ToContentRange(effectiveContentProfile);
        }

        return null;
    }

    private SourceHeightBucket? ResolveSourceBucketDefinition(int? sourceHeight)
    {
        if (sourceHeight.HasValue)
        {
            var matched = SourceBuckets.FirstOrDefault(bucket => bucket.Matches(sourceHeight.Value));
            if (matched is not null)
            {
                return matched;
            }
        }

        return SourceBuckets.FirstOrDefault(static bucket => bucket.IsDefault);
    }

    private static string? NormalizeProfileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string BuildDefaultsKey(string contentProfile, string qualityProfile)
    {
        return $"{contentProfile.Trim().ToLowerInvariant()}::{qualityProfile.Trim().ToLowerInvariant()}";
    }
}
