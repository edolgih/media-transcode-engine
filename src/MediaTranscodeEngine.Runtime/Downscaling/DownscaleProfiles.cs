namespace MediaTranscodeEngine.Runtime.Downscaling;

/*
Это реестр типизированных downscale-профилей runtime.
Он хранит профили по целевой высоте и позволяет сценариям и tool-адаптерам брать их как данные.
*/
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

    public bool TryGetProfile(int targetHeight, out DownscaleProfile profile)
    {
        return _profilesByTargetHeight.TryGetValue(targetHeight, out profile!);
    }

    internal static DownscaleProfiles Create(params DownscaleProfile[] profiles)
    {
        return new DownscaleProfiles(profiles.ToDictionary(static profile => profile.TargetHeight));
    }

    private static DownscaleProfiles CreateDefault()
    {
        return Create(
            Downscale424Profile.Create(),
            Downscale480Profile.Create(),
            Downscale576Profile.Create());
    }
}

/*
Это один профиль downscale для конкретной целевой высоты.
Он хранит defaults, autosample-правила и source-height buckets.
*/
/// <summary>
/// Represents one typed downscale profile keyed by target height.
/// </summary>
internal sealed class DownscaleProfile
{
    private readonly IReadOnlyDictionary<string, DownscaleDefaults> _defaultsByProfile;
    private readonly IReadOnlyDictionary<string, DownscaleRange> _globalContentRangesByProfile;
    private readonly IReadOnlyDictionary<string, DownscaleQualityRange> _globalQualityRangesByProfile;
    private readonly string[] _supportedContentProfiles;
    private readonly string[] _supportedQualityProfiles;

    public DownscaleProfile(
        int targetHeight,
        string defaultContentProfile,
        string defaultQualityProfile,
        DownscaleRateModel rateModel,
        DownscaleAutoSampling autoSampling,
        IReadOnlyList<SourceHeightBucket> sourceBuckets,
        IReadOnlyList<DownscaleDefaults> defaults,
        IReadOnlyList<DownscaleRange>? globalContentRanges = null,
        IReadOnlyList<DownscaleQualityRange>? globalQualityRanges = null)
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
        GlobalContentRanges = globalContentRanges ?? Array.Empty<DownscaleRange>();
        GlobalQualityRanges = globalQualityRanges ?? Array.Empty<DownscaleQualityRange>();
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

    public DownscaleRateModel RateModel { get; }

    public DownscaleAutoSampling AutoSampling { get; }

    public IReadOnlyList<SourceHeightBucket> SourceBuckets { get; }

    public IReadOnlyList<DownscaleDefaults> Defaults { get; }

    public IReadOnlyList<DownscaleRange> GlobalContentRanges { get; }

    public IReadOnlyList<DownscaleQualityRange> GlobalQualityRanges { get; }

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

    public DownscaleDefaults ResolveDefaults(string? contentProfile, string? qualityProfile)
    {
        return ResolveDefaults(sourceHeight: null, contentProfile, qualityProfile);
    }

    public DownscaleDefaults ResolveDefaults(int? sourceHeight, string? contentProfile, string? qualityProfile)
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
            $"Downscale defaults are not configured for content '{effectiveContentProfile}' and quality '{effectiveQualityProfile}'.");
    }

    public IReadOnlyList<DownscaleSampleWindow> GetSampleWindows(TimeSpan duration)
    {
        return AutoSampling.GetSampleWindows(duration);
    }

    public DownscaleRange? ResolveRange(int? sourceHeight, string? contentProfile, string? qualityProfile)
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

/*
Это одна запись default-настроек внутри downscale-профиля.
Она описывает базовые CQ/maxrate/bufsize и алгоритм масштабирования.
*/
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

/*
Это коэффициенты rate model для профиля downscale.
Они используются, когда пользователь переопределяет CQ и нужно пересчитать maxrate/bufsize.
*/
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

/*
Это autosample-настройки одного downscale-профиля.
Они определяют corridor, windowing и число итераций для fast/accurate/hybrid путей.
*/
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
    IReadOnlyList<double> LongWindowAnchors,
    TimeSpan MediumMinDuration,
    int MediumWindowCount,
    IReadOnlyList<double> MediumWindowAnchors,
    int ShortWindowCount,
    TimeSpan SampleWindowDuration,
    IReadOnlyList<double> ShortWindowAnchors)
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

    public IReadOnlyList<double> LongWindowAnchors { get; init; } = NormalizeAnchors(LongWindowAnchors, nameof(LongWindowAnchors));

    public TimeSpan MediumMinDuration { get; init; } = MediumMinDuration > TimeSpan.Zero
        ? MediumMinDuration
        : throw new ArgumentOutOfRangeException(nameof(MediumMinDuration), MediumMinDuration, "MediumMinDuration must be greater than zero.");

    public int MediumWindowCount { get; init; } = MediumWindowCount > 0
        ? MediumWindowCount
        : throw new ArgumentOutOfRangeException(nameof(MediumWindowCount), MediumWindowCount, "MediumWindowCount must be greater than zero.");

    public IReadOnlyList<double> MediumWindowAnchors { get; init; } = NormalizeAnchors(MediumWindowAnchors, nameof(MediumWindowAnchors));

    public int ShortWindowCount { get; init; } = ShortWindowCount > 0
        ? ShortWindowCount
        : throw new ArgumentOutOfRangeException(nameof(ShortWindowCount), ShortWindowCount, "ShortWindowCount must be greater than zero.");

    public TimeSpan SampleWindowDuration { get; init; } = SampleWindowDuration > TimeSpan.Zero
        ? SampleWindowDuration
        : throw new ArgumentOutOfRangeException(nameof(SampleWindowDuration), SampleWindowDuration, "SampleWindowDuration must be greater than zero.");

    public IReadOnlyList<double> ShortWindowAnchors { get; init; } = NormalizeAnchors(ShortWindowAnchors, nameof(ShortWindowAnchors));

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
                SampleWindowDuration,
                LongWindowAnchors,
                LongWindowCount);
        }

        if (duration >= MediumMinDuration)
        {
            return BuildWindows(
                duration,
                SampleWindowDuration,
                MediumWindowAnchors,
                MediumWindowCount);
        }

        return BuildWindows(
            duration,
            SampleWindowDuration,
            ShortWindowAnchors,
            ShortWindowCount);
    }

    private static IReadOnlyList<DownscaleSampleWindow> BuildWindows(
        TimeSpan totalDuration,
        TimeSpan sampleDuration,
        IReadOnlyList<double> anchors,
        int count)
    {
        if (count < 1)
        {
            count = 1;
        }

        var maxDurationSeconds = Math.Max((int)Math.Floor(totalDuration.TotalSeconds), 1);
        var durationSeconds = Math.Min((int)Math.Floor(sampleDuration.TotalSeconds), maxDurationSeconds);
        if (durationSeconds < 1)
        {
            return [];
        }

        var result = new List<DownscaleSampleWindow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var anchor in anchors.Take(count))
        {
            var maxStart = Math.Max((int)Math.Floor(totalDuration.TotalSeconds) - durationSeconds, 0);
            var centerSeconds = totalDuration.TotalSeconds * anchor;
            // Keep anchor-based windows stable across runtimes despite binary floating-point drift.
            var startSeconds = (int)Math.Floor(centerSeconds - (durationSeconds / 2.0) + 1e-9);
            startSeconds = Math.Min(Math.Max(startSeconds, 0), maxStart);
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

    private static IReadOnlyList<double> NormalizeAnchors(IReadOnlyList<double>? values, string paramName)
    {
        if (values is null || values.Count == 0)
        {
            throw new ArgumentException("Anchor list must not be null or empty.", paramName);
        }

        var normalized = new List<double>(values.Count);
        foreach (var value in values)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d || value >= 1d)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Anchor value must be between 0 and 1.");
            }

            if (!normalized.Contains(value))
            {
                normalized.Add(value);
            }
        }

        normalized.Sort();
        return normalized.ToArray();
    }
}

/*
Это corridor для content+quality комбинации в autosample.
По нему определяется, попадает ли reduction в допустимый диапазон.
*/
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

/*
Это fallback-corridor только по quality-профилю.
Он используется, когда нет более точного content+quality совпадения.
*/
/// <summary>
/// Represents one quality-only fallback corridor.
/// </summary>
internal sealed record DownscaleQualityRange(
    string QualityProfile,
    decimal? MinExclusive = null,
    decimal? MinInclusive = null,
    decimal? MaxExclusive = null,
    decimal? MaxInclusive = null)
{
    public string QualityProfile { get; init; } = NormalizeRequiredToken(QualityProfile, nameof(QualityProfile));

    public decimal? MinExclusive { get; init; } = NormalizeOptional(MinExclusive, nameof(MinExclusive));

    public decimal? MinInclusive { get; init; } = NormalizeOptional(MinInclusive, nameof(MinInclusive));

    public decimal? MaxExclusive { get; init; } = NormalizeOptional(MaxExclusive, nameof(MaxExclusive));

    public decimal? MaxInclusive { get; init; } = NormalizeOptional(MaxInclusive, nameof(MaxInclusive));

    public bool Matches(string qualityProfile)
    {
        return QualityProfile.Equals(qualityProfile, StringComparison.OrdinalIgnoreCase);
    }

    public DownscaleRange ToContentRange(string contentProfile)
    {
        return new DownscaleRange(
            ContentProfile: contentProfile,
            QualityProfile: QualityProfile,
            MinExclusive: MinExclusive,
            MinInclusive: MinInclusive,
            MaxExclusive: MaxExclusive,
            MaxInclusive: MaxInclusive);
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

/*
Это одно sample-окно для измерений autosample.
Оно задаёт старт и длительность фрагмента, который будет измеряться через ffmpeg.
*/
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

/*
Это необязательный bounds-override для конкретного source-height bucket.
Он позволяет локально подправить допустимые границы профиля.
*/
/// <summary>
/// Represents one optional bounds override attached to a source-height bucket.
/// </summary>
internal sealed record DownscaleBoundsOverride(
    string ContentProfile,
    string QualityProfile,
    int? CqMin = null,
    int? CqMax = null,
    decimal? MaxrateMin = null,
    decimal? MaxrateMax = null)
{
    public string ContentProfile { get; init; } = NormalizeRequiredToken(ContentProfile, nameof(ContentProfile));

    public string QualityProfile { get; init; } = NormalizeRequiredToken(QualityProfile, nameof(QualityProfile));

    public int? CqMin { get; init; } = NormalizeOptionalPositiveInt(CqMin, nameof(CqMin));

    public int? CqMax { get; init; } = NormalizeOptionalPositiveInt(CqMax, nameof(CqMax));

    public decimal? MaxrateMin { get; init; } = NormalizeOptionalPositiveDecimal(MaxrateMin, nameof(MaxrateMin));

    public decimal? MaxrateMax { get; init; } = NormalizeOptionalPositiveDecimal(MaxrateMax, nameof(MaxrateMax));

    public bool Matches(string contentProfile, string qualityProfile)
    {
        return ContentProfile.Equals(contentProfile, StringComparison.OrdinalIgnoreCase) &&
               QualityProfile.Equals(qualityProfile, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }

    private static int? NormalizeOptionalPositiveInt(int? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value > 0
            ? value.Value
            : throw new ArgumentOutOfRangeException(paramName, value.Value, "Value must be greater than zero.");
    }

    private static decimal? NormalizeOptionalPositiveDecimal(decimal? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value > 0m
            ? value.Value
            : throw new ArgumentOutOfRangeException(paramName, value.Value, "Value must be greater than zero.");
    }
}

/*
Это bucket исходной высоты для профиля downscale.
Через него профиль различает правила для разных source-size диапазонов.
*/
/// <summary>
/// Represents one source-height bucket used by downscale profiles.
/// </summary>
internal sealed record SourceHeightBucket(
    string Name,
    int MinHeight,
    int MaxHeight,
    IReadOnlyList<DownscaleRange>? Ranges = null,
    IReadOnlyList<DownscaleQualityRange>? QualityRanges = null,
    IReadOnlyList<DownscaleBoundsOverride>? BoundsOverrides = null,
    bool IsDefault = false)
{
    public string Name { get; init; } = NormalizeRequiredToken(Name, nameof(Name));

    public int MinHeight { get; init; } = MinHeight > 0
        ? MinHeight
        : throw new ArgumentOutOfRangeException(nameof(MinHeight), MinHeight, "Minimum height must be greater than zero.");

    public int MaxHeight { get; init; } = MaxHeight >= MinHeight
        ? MaxHeight
        : throw new ArgumentOutOfRangeException(nameof(MaxHeight), MaxHeight, "Maximum height must be greater than or equal to minimum height.");

    public IReadOnlyList<DownscaleRange> Ranges { get; init; } = Ranges ?? Array.Empty<DownscaleRange>();

    public IReadOnlyList<DownscaleQualityRange> QualityRanges { get; init; } = QualityRanges ?? Array.Empty<DownscaleQualityRange>();

    public IReadOnlyList<DownscaleBoundsOverride> BoundsOverrides { get; init; } = BoundsOverrides ?? Array.Empty<DownscaleBoundsOverride>();

    public bool IsDefault { get; init; } = IsDefault;

    public bool Matches(int height)
    {
        return height >= MinHeight && height <= MaxHeight;
    }

    public DownscaleRange? ResolveRange(string contentProfile, string qualityProfile)
    {
        var range = Ranges.FirstOrDefault(range => range.Matches(contentProfile, qualityProfile));
        if (range is not null)
        {
            return range;
        }

        var qualityRange = QualityRanges.FirstOrDefault(range => range.Matches(qualityProfile));
        return qualityRange?.ToContentRange(contentProfile);
    }

    public DownscaleBoundsOverride? ResolveBoundsOverride(string contentProfile, string qualityProfile)
    {
        return BoundsOverrides.FirstOrDefault(overrideEntry => overrideEntry.Matches(contentProfile, qualityProfile));
    }

    public string? ResolveMissingRange(IEnumerable<string> supportedContentProfiles, IEnumerable<string> supportedQualityProfiles)
    {
        if (Ranges.Count == 0 && QualityRanges.Count == 0)
        {
            return "ranges";
        }

        if (Ranges.Count > 0)
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

        foreach (var qualityProfile in supportedQualityProfiles)
        {
            if (!QualityRanges.Any(range => range.Matches(qualityProfile)))
            {
                return qualityProfile;
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
