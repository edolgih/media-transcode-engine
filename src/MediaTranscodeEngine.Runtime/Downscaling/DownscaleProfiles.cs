namespace MediaTranscodeEngine.Runtime.Downscaling;

/// <summary>
/// Provides typed downscale profiles used by Runtime.
/// </summary>
internal sealed class DownscaleProfiles
{
    private readonly IReadOnlyDictionary<int, DownscaleProfile> _profilesByTargetHeight;

    internal DownscaleProfiles(IReadOnlyDictionary<int, DownscaleProfile> profilesByTargetHeight)
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

    internal static DownscaleProfiles Create(params DownscaleProfile[] profiles)
    {
        return new DownscaleProfiles(profiles.ToDictionary(static profile => profile.TargetHeight));
    }

    private static DownscaleProfiles CreateDefault()
    {
        return Create(Downscale576Profile.Create());
    }
}

/// <summary>
/// Represents one typed downscale profile keyed by target height.
/// </summary>
internal sealed class DownscaleProfile
{
    private readonly IReadOnlyDictionary<string, DownscaleDefaults> _defaultsByProfile;
    private readonly string[] _supportedContentProfiles;
    private readonly string[] _supportedQualityProfiles;

    public DownscaleProfile(
        int targetHeight,
        string defaultContentProfile,
        string defaultQualityProfile,
        DownscaleRateModel rateModel,
        DownscaleAutoSampling autoSampling,
        IReadOnlyList<SourceHeightBucket> sourceBuckets,
        IReadOnlyList<DownscaleDefaults> defaults)
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
        _defaultsByProfile = defaults.ToDictionary(
            static entry => BuildDefaultsKey(entry.ContentProfile, entry.QualityProfile),
            StringComparer.OrdinalIgnoreCase);
        _supportedContentProfiles = defaults.Select(static entry => entry.ContentProfile).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _supportedQualityProfiles = defaults.Select(static entry => entry.QualityProfile).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public int TargetHeight { get; }

    public string DefaultContentProfile { get; }

    public string DefaultQualityProfile { get; }

    public DownscaleRateModel RateModel { get; }

    public DownscaleAutoSampling AutoSampling { get; }

    public IReadOnlyList<SourceHeightBucket> SourceBuckets { get; }

    public IReadOnlyList<DownscaleDefaults> Defaults { get; }

    public string? ResolveSourceBucket(int? sourceHeight)
    {
        return ResolveSourceBucketDefinition(sourceHeight)?.Name;
    }

    public string? ResolveSourceBucketIssue(int? sourceHeight)
    {
        if (!sourceHeight.HasValue)
        {
            return $"{TargetHeight} source bucket missing: height is unknown; add SourceBuckets";
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

    public IReadOnlyList<DownscaleSampleWindow> GetSampleWindows(TimeSpan duration)
    {
        return AutoSampling.GetSampleWindows(duration);
    }

    public DownscaleRange? ResolveRange(int? sourceHeight, string? contentProfile, string? qualityProfile)
    {
        var bucket = ResolveSourceBucketDefinition(sourceHeight);
        if (bucket is null)
        {
            return null;
        }

        var effectiveContentProfile = NormalizeProfileName(contentProfile) ?? DefaultContentProfile;
        var effectiveQualityProfile = NormalizeProfileName(qualityProfile) ?? DefaultQualityProfile;
        return bucket.ResolveRange(effectiveContentProfile, effectiveQualityProfile);
    }

    private SourceHeightBucket? ResolveSourceBucketDefinition(int? sourceHeight)
    {
        if (!sourceHeight.HasValue)
        {
            return null;
        }

        return SourceBuckets.FirstOrDefault(bucket => bucket.Matches(sourceHeight.Value));
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
    string Algorithm,
    int CqMin,
    int CqMax,
    decimal MaxrateMin,
    decimal MaxrateMax)
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

    public int CqMin { get; init; } = CqMin > 0
        ? CqMin
        : throw new ArgumentOutOfRangeException(nameof(CqMin), CqMin, "CQ minimum must be greater than zero.");

    public int CqMax { get; init; } = CqMax >= CqMin
        ? CqMax
        : throw new ArgumentOutOfRangeException(nameof(CqMax), CqMax, "CQ maximum must be greater than or equal to minimum.");

    public decimal MaxrateMin { get; init; } = MaxrateMin > 0m
        ? MaxrateMin
        : throw new ArgumentOutOfRangeException(nameof(MaxrateMin), MaxrateMin, "Maxrate minimum must be greater than zero.");

    public decimal MaxrateMax { get; init; } = MaxrateMax >= MaxrateMin
        ? MaxrateMax
        : throw new ArgumentOutOfRangeException(nameof(MaxrateMax), MaxrateMax, "Maxrate maximum must be greater than or equal to minimum.");

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}

/// <summary>
/// Stores the shared rate-model constants used when CQ is overridden.
/// </summary>
internal sealed record DownscaleRateModel(decimal CqStepToMaxrateStep, decimal BufsizeMultiplier)
{
    public decimal CqStepToMaxrateStep { get; init; } = CqStepToMaxrateStep > 0m
        ? CqStepToMaxrateStep
        : throw new ArgumentOutOfRangeException(nameof(CqStepToMaxrateStep), CqStepToMaxrateStep, "CQ step must be greater than zero.");

    public decimal BufsizeMultiplier { get; init; } = BufsizeMultiplier > 0m
        ? BufsizeMultiplier
        : throw new ArgumentOutOfRangeException(nameof(BufsizeMultiplier), BufsizeMultiplier, "Bufsize multiplier must be greater than zero.");
}

/// <summary>
/// Stores autosample defaults and sample-window rules for one downscale profile.
/// </summary>
internal sealed record DownscaleAutoSampling(
    bool EnabledByDefault,
    string ModeDefault,
    int MaxIterations,
    int HybridAccurateIterations,
    decimal AudioBitrateEstimateMbps,
    TimeSpan LongMinDuration,
    int LongWindowCount,
    TimeSpan LongWindowDuration,
    TimeSpan MediumMinDuration,
    int MediumWindowCount,
    TimeSpan MediumWindowDuration,
    int ShortWindowCount,
    TimeSpan ShortWindowDuration)
{
    public string ModeDefault { get; init; } = NormalizeRequiredToken(ModeDefault, nameof(ModeDefault));

    public int MaxIterations { get; init; } = MaxIterations > 0
        ? MaxIterations
        : throw new ArgumentOutOfRangeException(nameof(MaxIterations), MaxIterations, "MaxIterations must be greater than zero.");

    public int HybridAccurateIterations { get; init; } = HybridAccurateIterations >= 0
        ? HybridAccurateIterations
        : throw new ArgumentOutOfRangeException(nameof(HybridAccurateIterations), HybridAccurateIterations, "HybridAccurateIterations must not be negative.");

    public decimal AudioBitrateEstimateMbps { get; init; } = AudioBitrateEstimateMbps >= 0m
        ? AudioBitrateEstimateMbps
        : throw new ArgumentOutOfRangeException(nameof(AudioBitrateEstimateMbps), AudioBitrateEstimateMbps, "AudioBitrateEstimateMbps must not be negative.");

    public TimeSpan LongMinDuration { get; init; } = LongMinDuration > TimeSpan.Zero
        ? LongMinDuration
        : throw new ArgumentOutOfRangeException(nameof(LongMinDuration), LongMinDuration, "LongMinDuration must be greater than zero.");

    public int LongWindowCount { get; init; } = LongWindowCount > 0
        ? LongWindowCount
        : throw new ArgumentOutOfRangeException(nameof(LongWindowCount), LongWindowCount, "LongWindowCount must be greater than zero.");

    public TimeSpan LongWindowDuration { get; init; } = LongWindowDuration > TimeSpan.Zero
        ? LongWindowDuration
        : throw new ArgumentOutOfRangeException(nameof(LongWindowDuration), LongWindowDuration, "LongWindowDuration must be greater than zero.");

    public TimeSpan MediumMinDuration { get; init; } = MediumMinDuration > TimeSpan.Zero
        ? MediumMinDuration
        : throw new ArgumentOutOfRangeException(nameof(MediumMinDuration), MediumMinDuration, "MediumMinDuration must be greater than zero.");

    public int MediumWindowCount { get; init; } = MediumWindowCount > 0
        ? MediumWindowCount
        : throw new ArgumentOutOfRangeException(nameof(MediumWindowCount), MediumWindowCount, "MediumWindowCount must be greater than zero.");

    public TimeSpan MediumWindowDuration { get; init; } = MediumWindowDuration > TimeSpan.Zero
        ? MediumWindowDuration
        : throw new ArgumentOutOfRangeException(nameof(MediumWindowDuration), MediumWindowDuration, "MediumWindowDuration must be greater than zero.");

    public int ShortWindowCount { get; init; } = ShortWindowCount > 0
        ? ShortWindowCount
        : throw new ArgumentOutOfRangeException(nameof(ShortWindowCount), ShortWindowCount, "ShortWindowCount must be greater than zero.");

    public TimeSpan ShortWindowDuration { get; init; } = ShortWindowDuration > TimeSpan.Zero
        ? ShortWindowDuration
        : throw new ArgumentOutOfRangeException(nameof(ShortWindowDuration), ShortWindowDuration, "ShortWindowDuration must be greater than zero.");

    public IReadOnlyList<DownscaleSampleWindow> GetSampleWindows(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return [];
        }

        if (duration >= LongMinDuration)
        {
            return BuildWindows(
                duration,
                [
                    new DownscaleSampleWindow(StartSeconds: 60, DurationSeconds: (int)LongWindowDuration.TotalSeconds),
                    new DownscaleSampleWindow(
                        StartSeconds: (int)Math.Floor(Math.Max((duration.TotalSeconds / 2.0) - (LongWindowDuration.TotalSeconds / 2.0), 0.0)),
                        DurationSeconds: (int)LongWindowDuration.TotalSeconds),
                    new DownscaleSampleWindow(
                        StartSeconds: (int)Math.Floor(Math.Max(duration.TotalSeconds - LongWindowDuration.TotalSeconds - 60.0, 0.0)),
                        DurationSeconds: (int)LongWindowDuration.TotalSeconds)
                ],
                LongWindowCount);
        }

        if (duration >= MediumMinDuration)
        {
            return BuildWindows(
                duration,
                [
                    new DownscaleSampleWindow(StartSeconds: 45, DurationSeconds: (int)MediumWindowDuration.TotalSeconds),
                    new DownscaleSampleWindow(
                        StartSeconds: (int)Math.Floor(Math.Max((duration.TotalSeconds / 2.0) - (MediumWindowDuration.TotalSeconds / 2.0), 0.0)),
                        DurationSeconds: (int)MediumWindowDuration.TotalSeconds)
                ],
                MediumWindowCount);
        }

        return BuildWindows(
            duration,
            [
                new DownscaleSampleWindow(
                    StartSeconds: (int)Math.Floor(Math.Max((duration.TotalSeconds / 2.0) - (ShortWindowDuration.TotalSeconds / 2.0), 0.0)),
                    DurationSeconds: (int)ShortWindowDuration.TotalSeconds)
            ],
            ShortWindowCount);
    }

    private static IReadOnlyList<DownscaleSampleWindow> BuildWindows(
        TimeSpan totalDuration,
        IReadOnlyList<DownscaleSampleWindow> slots,
        int count)
    {
        if (count < 1)
        {
            count = 1;
        }

        var maxDurationSeconds = Math.Max((int)Math.Floor(totalDuration.TotalSeconds), 1);
        var result = new List<DownscaleSampleWindow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var slot in slots.Take(count))
        {
            var durationSeconds = Math.Min(slot.DurationSeconds, maxDurationSeconds);
            if (durationSeconds < 1)
            {
                continue;
            }

            var maxStart = Math.Max((int)Math.Floor(totalDuration.TotalSeconds) - durationSeconds, 0);
            var startSeconds = Math.Min(Math.Max(slot.StartSeconds, 0), maxStart);
            var key = $"{startSeconds}|{durationSeconds}";
            if (seen.Add(key))
            {
                result.Add(new DownscaleSampleWindow(startSeconds, durationSeconds));
            }
        }

        return result;
    }

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}

/// <summary>
/// Represents one content+quality reduction corridor entry.
/// </summary>
internal sealed record DownscaleRange(
    string ContentProfile,
    string QualityProfile,
    decimal? MinExclusive = null,
    decimal? MinInclusive = null,
    decimal? MaxExclusive = null,
    decimal? MaxInclusive = null)
{
    public string ContentProfile { get; init; } = NormalizeRequiredToken(ContentProfile, nameof(ContentProfile));

    public string QualityProfile { get; init; } = NormalizeRequiredToken(QualityProfile, nameof(QualityProfile));

    public decimal? MinExclusive { get; init; } = NormalizeOptional(MinExclusive, nameof(MinExclusive));

    public decimal? MinInclusive { get; init; } = NormalizeOptional(MinInclusive, nameof(MinInclusive));

    public decimal? MaxExclusive { get; init; } = NormalizeOptional(MaxExclusive, nameof(MaxExclusive));

    public decimal? MaxInclusive { get; init; } = NormalizeOptional(MaxInclusive, nameof(MaxInclusive));

    public bool Matches(string contentProfile, string qualityProfile)
    {
        return ContentProfile.Equals(contentProfile, StringComparison.OrdinalIgnoreCase) &&
               QualityProfile.Equals(qualityProfile, StringComparison.OrdinalIgnoreCase);
    }

    public bool Contains(decimal value)
    {
        if (MinInclusive.HasValue && value < MinInclusive.Value)
        {
            return false;
        }

        if (MinExclusive.HasValue && value <= MinExclusive.Value)
        {
            return false;
        }

        if (MaxInclusive.HasValue && value > MaxInclusive.Value)
        {
            return false;
        }

        if (MaxExclusive.HasValue && value >= MaxExclusive.Value)
        {
            return false;
        }

        return true;
    }

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }

    private static decimal? NormalizeOptional(decimal? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value >= 0m
            ? value.Value
            : throw new ArgumentOutOfRangeException(paramName, value.Value, "Range value must not be negative.");
    }
}

/// <summary>
/// Represents one autosample window inside a downscale profile.
/// </summary>
internal sealed record DownscaleSampleWindow(
    int StartSeconds,
    int DurationSeconds)
{
    public int StartSeconds { get; init; } = StartSeconds >= 0
        ? StartSeconds
        : throw new ArgumentOutOfRangeException(nameof(StartSeconds), StartSeconds, "StartSeconds must not be negative.");

    public int DurationSeconds { get; init; } = DurationSeconds > 0
        ? DurationSeconds
        : throw new ArgumentOutOfRangeException(nameof(DurationSeconds), DurationSeconds, "DurationSeconds must be greater than zero.");
}

/// <summary>
/// Represents one source-height bucket used by downscale profiles.
/// </summary>
internal sealed record SourceHeightBucket(string Name, int MinHeight, int MaxHeight, IReadOnlyList<DownscaleRange>? Ranges = null)
{
    public string Name { get; init; } = NormalizeRequiredToken(Name, nameof(Name));

    public int MinHeight { get; init; } = MinHeight > 0
        ? MinHeight
        : throw new ArgumentOutOfRangeException(nameof(MinHeight), MinHeight, "Minimum height must be greater than zero.");

    public int MaxHeight { get; init; } = MaxHeight >= MinHeight
        ? MaxHeight
        : throw new ArgumentOutOfRangeException(nameof(MaxHeight), MaxHeight, "Maximum height must be greater than or equal to minimum height.");

    public IReadOnlyList<DownscaleRange> Ranges { get; init; } = Ranges ?? Array.Empty<DownscaleRange>();

    public bool Matches(int height)
    {
        return height >= MinHeight && height <= MaxHeight;
    }

    public DownscaleRange? ResolveRange(string contentProfile, string qualityProfile)
    {
        return Ranges.FirstOrDefault(range => range.Matches(contentProfile, qualityProfile));
    }

    public string? ResolveMissingRange(IEnumerable<string> supportedContentProfiles, IEnumerable<string> supportedQualityProfiles)
    {
        foreach (var contentProfile in supportedContentProfiles)
        {
            foreach (var qualityProfile in supportedQualityProfiles)
            {
                if (!Ranges.Any(range => range.Matches(contentProfile, qualityProfile)))
                {
                    return $"{contentProfile}/{qualityProfile}";
                }
            }
        }

        return null;
    }

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}
