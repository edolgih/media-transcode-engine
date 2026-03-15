using Transcode.Core.VideoSettings;

namespace Transcode.Core.VideoSettings.Profiles;

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
        IReadOnlyList<VideoSettingsRange> globalContentRanges,
        IReadOnlyList<VideoSettingsQualityRange> globalQualityRanges,
        bool supportsDownscale = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultContentProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultQualityProfile);
        ArgumentNullException.ThrowIfNull(rateModel);
        ArgumentNullException.ThrowIfNull(autoSampling);
        ArgumentNullException.ThrowIfNull(sourceBuckets);
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(globalContentRanges);
        ArgumentNullException.ThrowIfNull(globalQualityRanges);

        TargetHeight = targetHeight;
        SupportsDownscale = supportsDownscale;
        DefaultContentProfile = defaultContentProfile.Trim().ToLowerInvariant();
        DefaultQualityProfile = defaultQualityProfile.Trim().ToLowerInvariant();
        RateModel = rateModel;
        AutoSampling = autoSampling;
        SourceBuckets = sourceBuckets;
        Defaults = defaults;
        GlobalContentRanges = globalContentRanges;
        GlobalQualityRanges = globalQualityRanges;
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

    public bool SupportsDownscale { get; }

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

    public VideoSettingsDefaults ResolveDefaults(EffectiveVideoSettingsSelection selection)
    {
        return ResolveDefaults(sourceHeight: null, selection);
    }

    public VideoSettingsDefaults ResolveDefaults(int? sourceHeight, EffectiveVideoSettingsSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var key = BuildDefaultsKey(selection.ContentProfile, selection.QualityProfile);
        if (_defaultsByProfile.TryGetValue(key, out var defaults))
        {
            var boundsOverride = ResolveSourceBucketDefinition(sourceHeight)?.ResolveBoundsOverride(selection.ContentProfile, selection.QualityProfile);
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
            $"Video settings defaults are not configured for content '{selection.ContentProfile}' and quality '{selection.QualityProfile}'.");
    }

    public IReadOnlyList<VideoSettingsSampleWindow> GetSampleWindows(TimeSpan duration)
    {
        return AutoSampling.GetSampleWindows(duration);
    }

    public VideoSettingsRange? ResolveRange(int? sourceHeight, EffectiveVideoSettingsSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var bucket = ResolveSourceBucketDefinition(sourceHeight);
        var bucketRange = bucket?.ResolveRange(selection.ContentProfile, selection.QualityProfile);
        if (bucketRange is not null)
        {
            return bucketRange;
        }

        var key = BuildDefaultsKey(selection.ContentProfile, selection.QualityProfile);
        if (_globalContentRangesByProfile.TryGetValue(key, out var globalContentRange))
        {
            return globalContentRange;
        }

        if (_globalQualityRangesByProfile.TryGetValue(selection.QualityProfile, out var globalQualityRange))
        {
            return globalQualityRange.ToContentRange(selection.ContentProfile);
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

    private static string BuildDefaultsKey(string contentProfile, string qualityProfile)
    {
        return $"{contentProfile.Trim().ToLowerInvariant()}::{qualityProfile.Trim().ToLowerInvariant()}";
    }
}
