namespace MediaTranscodeEngine.Runtime.Downscaling;

/// <summary>
/// Provides typed downscale profiles used by Runtime.
/// </summary>
internal sealed class DownscaleProfiles
{
    private readonly IReadOnlyDictionary<int, DownscaleProfile> _profilesByTargetHeight;

    private DownscaleProfiles(IReadOnlyDictionary<int, DownscaleProfile> profilesByTargetHeight)
    {
        _profilesByTargetHeight = profilesByTargetHeight;
    }

    public static DownscaleProfiles Default { get; } = CreateDefault();

    public DownscaleProfile GetRequiredProfile(int targetHeight)
    {
        if (_profilesByTargetHeight.TryGetValue(targetHeight, out var profile))
        {
            return profile;
        }

        throw new InvalidOperationException($"Downscale profile '{targetHeight}' is not configured.");
    }

    private static DownscaleProfiles CreateDefault()
    {
        var profile576 = new DownscaleProfile(
            targetHeight: 576,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            sourceBuckets:
            [
                new SourceHeightBucket("hd_720", MinHeight: 650, MaxHeight: 899),
                new SourceHeightBucket("fhd_1080", MinHeight: 1000, MaxHeight: 1300)
            ],
            defaults:
            [
                new DownscaleDefaults("anime", "high", Cq: 22, Maxrate: 3.3m, Bufsize: 6.5m, Algorithm: "bilinear"),
                new DownscaleDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: "bilinear"),
                new DownscaleDefaults("anime", "low", Cq: 29, Maxrate: 2.1m, Bufsize: 4.1m, Algorithm: "bilinear"),
                new DownscaleDefaults("mult", "high", Cq: 24, Maxrate: 2.7m, Bufsize: 5.3m, Algorithm: "bilinear"),
                new DownscaleDefaults("mult", "default", Cq: 26, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: "bilinear"),
                new DownscaleDefaults("mult", "low", Cq: 29, Maxrate: 1.7m, Bufsize: 3.5m, Algorithm: "bilinear"),
                new DownscaleDefaults("film", "high", Cq: 24, Maxrate: 3.7m, Bufsize: 7.4m, Algorithm: "bilinear"),
                new DownscaleDefaults("film", "default", Cq: 26, Maxrate: 3.4m, Bufsize: 6.9m, Algorithm: "bilinear"),
                new DownscaleDefaults("film", "low", Cq: 30, Maxrate: 2.2m, Bufsize: 4.5m, Algorithm: "bilinear")
            ]);

        return new DownscaleProfiles(
            new Dictionary<int, DownscaleProfile>
            {
                [profile576.TargetHeight] = profile576
            });
    }
}

/// <summary>
/// Represents one typed downscale profile keyed by target height.
/// </summary>
internal sealed class DownscaleProfile
{
    private readonly IReadOnlyDictionary<string, DownscaleDefaults> _defaultsByProfile;

    public DownscaleProfile(
        int targetHeight,
        string defaultContentProfile,
        string defaultQualityProfile,
        IReadOnlyList<SourceHeightBucket> sourceBuckets,
        IReadOnlyList<DownscaleDefaults> defaults)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultContentProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultQualityProfile);

        TargetHeight = targetHeight;
        DefaultContentProfile = defaultContentProfile.Trim().ToLowerInvariant();
        DefaultQualityProfile = defaultQualityProfile.Trim().ToLowerInvariant();
        SourceBuckets = sourceBuckets;
        Defaults = defaults;
        _defaultsByProfile = defaults.ToDictionary(
            static entry => BuildDefaultsKey(entry.ContentProfile, entry.QualityProfile),
            StringComparer.OrdinalIgnoreCase);
    }

    public int TargetHeight { get; }

    public string DefaultContentProfile { get; }

    public string DefaultQualityProfile { get; }

    public IReadOnlyList<SourceHeightBucket> SourceBuckets { get; }

    public IReadOnlyList<DownscaleDefaults> Defaults { get; }

    public string? ResolveSourceBucket(int? sourceHeight)
    {
        if (!sourceHeight.HasValue)
        {
            return null;
        }

        return SourceBuckets.FirstOrDefault(bucket => bucket.Matches(sourceHeight.Value))?.Name;
    }

    public DownscaleDefaults ResolveDefaults(string? contentProfile, string? qualityProfile)
    {
        var effectiveContentProfile = NormalizeProfileName(contentProfile) ?? DefaultContentProfile;
        var effectiveQualityProfile = NormalizeProfileName(qualityProfile) ?? DefaultQualityProfile;
        var key = BuildDefaultsKey(effectiveContentProfile, effectiveQualityProfile);
        if (_defaultsByProfile.TryGetValue(key, out var defaults))
        {
            return defaults;
        }

        throw new InvalidOperationException(
            $"Downscale defaults are not configured for content '{effectiveContentProfile}' and quality '{effectiveQualityProfile}'.");
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

/// <summary>
/// Represents one default settings entry inside a typed downscale profile.
/// </summary>
internal sealed record DownscaleDefaults(
    string ContentProfile,
    string QualityProfile,
    int Cq,
    decimal Maxrate,
    decimal Bufsize,
    string Algorithm)
{
    public string ContentProfile { get; init; } = NormalizeRequiredToken(ContentProfile, nameof(ContentProfile));

    public string QualityProfile { get; init; } = NormalizeRequiredToken(QualityProfile, nameof(QualityProfile));

    public int Cq { get; init; } = Cq > 0
        ? Cq
        : throw new ArgumentOutOfRangeException(nameof(Cq), Cq, "CQ must be greater than zero.");

    public decimal Maxrate { get; init; } = Maxrate > 0m
        ? Maxrate
        : throw new ArgumentOutOfRangeException(nameof(Maxrate), Maxrate, "Maxrate must be greater than zero.");

    public decimal Bufsize { get; init; } = Bufsize > 0m
        ? Bufsize
        : throw new ArgumentOutOfRangeException(nameof(Bufsize), Bufsize, "Bufsize must be greater than zero.");

    public string Algorithm { get; init; } = NormalizeRequiredToken(Algorithm, nameof(Algorithm));

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}

/// <summary>
/// Represents one source-height bucket used by downscale profiles.
/// </summary>
internal sealed record SourceHeightBucket(string Name, int MinHeight, int MaxHeight)
{
    public string Name { get; init; } = NormalizeRequiredToken(Name, nameof(Name));

    public int MinHeight { get; init; } = MinHeight > 0
        ? MinHeight
        : throw new ArgumentOutOfRangeException(nameof(MinHeight), MinHeight, "Minimum height must be greater than zero.");

    public int MaxHeight { get; init; } = MaxHeight >= MinHeight
        ? MaxHeight
        : throw new ArgumentOutOfRangeException(nameof(MaxHeight), MaxHeight, "Maximum height must be greater than or equal to minimum height.");

    public bool Matches(int height)
    {
        return height >= MinHeight && height <= MaxHeight;
    }

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}
