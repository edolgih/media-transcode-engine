using MediaTranscodeEngine.Runtime.VideoSettings.Profiles;

namespace MediaTranscodeEngine.Runtime.VideoSettings;

/*
Это реестр типизированных video-settings профилей runtime.
Он хранит profile data по целевой высоте и выступает единым source of truth для ordinary encode и downscale.
*/
/// <summary>
/// Provides typed video-settings profiles used by Runtime.
/// </summary>
internal sealed class VideoSettingsProfiles
{
    private readonly IReadOnlyDictionary<int, VideoSettingsProfile> _profilesByTargetHeight;
    private readonly int[] _supportedDownscaleTargetHeights;
    private readonly string[] _supportedContentProfiles;
    private readonly string[] _supportedQualityProfiles;

    internal VideoSettingsProfiles(IReadOnlyDictionary<int, VideoSettingsProfile> profilesByTargetHeight)
    {
        _profilesByTargetHeight = profilesByTargetHeight;
        _supportedDownscaleTargetHeights = profilesByTargetHeight.Values
            .Where(static profile => profile.SupportsDownscale)
            .OrderByDescending(static profile => profile.TargetHeight)
            .Select(static profile => profile.TargetHeight)
            .ToArray();
        _supportedContentProfiles = profilesByTargetHeight.Values
            .SelectMany(static profile => profile.Defaults)
            .Select(static defaults => defaults.ContentProfile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _supportedQualityProfiles = profilesByTargetHeight.Values
            .SelectMany(static profile => profile.Defaults)
            .Select(static defaults => defaults.QualityProfile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static VideoSettingsProfiles Default { get; } = CreateDefault();

    public VideoSettingsProfile GetRequiredProfile(int targetHeight)
    {
        if (_profilesByTargetHeight.TryGetValue(targetHeight, out var profile))
        {
            return profile;
        }

        throw new InvalidOperationException($"Video settings profile '{targetHeight}' is not configured.");
    }

    public bool TryGetProfile(int targetHeight, out VideoSettingsProfile profile)
    {
        return _profilesByTargetHeight.TryGetValue(targetHeight, out profile!);
    }

    public VideoSettingsProfile ResolveOutputProfile(int outputHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);

        return _profilesByTargetHeight.Values
            .OrderBy(profile => Math.Abs(profile.TargetHeight - outputHeight))
            .ThenByDescending(profile => profile.TargetHeight)
            .First();
    }

    public IReadOnlyList<int> GetSupportedDownscaleTargetHeights()
    {
        return _supportedDownscaleTargetHeights;
    }

    public IReadOnlyList<string> GetSupportedContentProfiles()
    {
        return _supportedContentProfiles;
    }

    public IReadOnlyList<string> GetSupportedQualityProfiles()
    {
        return _supportedQualityProfiles;
    }

    public bool SupportsDownscaleTargetHeight(int targetHeight)
    {
        return _profilesByTargetHeight.TryGetValue(targetHeight, out var profile) &&
               profile.SupportsDownscale;
    }

    internal static VideoSettingsProfiles Create(params VideoSettingsProfile[] profiles)
    {
        return new VideoSettingsProfiles(profiles.ToDictionary(static profile => profile.TargetHeight));
    }

    private static VideoSettingsProfiles CreateDefault()
    {
        return Create(
            VideoSettings1080Profile.Create(),
            VideoSettings720Profile.Create(),
            VideoSettings424Profile.Create(),
            VideoSettings480Profile.Create(),
            VideoSettings576Profile.Create());
    }
}

/*
Это одна запись default-настроек внутри video-settings профиля.
Она описывает базовые CQ/maxrate/bufsize и алгоритм масштабирования.
*/
/// <summary>
/// Represents one default settings entry inside a typed video-settings profile.
/// </summary>
internal sealed record VideoSettingsDefaults(
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
Это коэффициенты rate model для video-settings профиля.
Они используются, когда пользователь переопределяет CQ и нужно пересчитать maxrate/bufsize.
*/
/// <summary>
/// Stores the shared rate-model constants used when CQ is overridden.
/// </summary>
internal sealed record VideoSettingsRateModel(decimal CqStepToMaxrateStep, decimal BufsizeMultiplier)
{
    public decimal CqStepToMaxrateStep { get; init; } = CqStepToMaxrateStep > 0m
        ? CqStepToMaxrateStep
        : throw new ArgumentOutOfRangeException(nameof(CqStepToMaxrateStep), CqStepToMaxrateStep, "CQ step must be greater than zero.");

    public decimal BufsizeMultiplier { get; init; } = BufsizeMultiplier > 0m
        ? BufsizeMultiplier
        : throw new ArgumentOutOfRangeException(nameof(BufsizeMultiplier), BufsizeMultiplier, "Bufsize multiplier must be greater than zero.");
}

/*
Это autosample-настройки одного video-settings профиля.
Они определяют corridor, windowing и число итераций для fast/accurate/hybrid путей.
*/
/// <summary>
/// Stores autosample mode defaults, iteration budget, and sample-window rules for one video-settings profile.
/// </summary>
internal sealed record VideoSettingsAutoSampling(
    string ModeDefault,
    int MaxIterations,
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

    public IReadOnlyList<VideoSettingsSampleWindow> GetSampleWindows(TimeSpan duration)
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

    private static IReadOnlyList<VideoSettingsSampleWindow> BuildWindows(
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

        var result = new List<VideoSettingsSampleWindow>();
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
                result.Add(new VideoSettingsSampleWindow(startSeconds, durationSeconds));
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
internal sealed record VideoSettingsRange(
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
internal sealed record VideoSettingsQualityRange(
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

    public VideoSettingsRange ToContentRange(string contentProfile)
    {
        return new VideoSettingsRange(
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
/// Represents one autosample window inside a video-settings profile.
/// </summary>
internal sealed record VideoSettingsSampleWindow(
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
internal sealed record VideoSettingsBoundsOverride(
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
Это bucket исходной высоты для video-settings профиля.
Через него профиль различает правила для разных source-size диапазонов.
*/
/// <summary>
/// Represents one source-height bucket used by video-settings profiles.
/// </summary>
internal sealed record SourceHeightBucket(
    string Name,
    int MinHeight,
    int MaxHeight,
    IReadOnlyList<VideoSettingsRange>? Ranges = null,
    IReadOnlyList<VideoSettingsQualityRange>? QualityRanges = null,
    IReadOnlyList<VideoSettingsBoundsOverride>? BoundsOverrides = null,
    bool IsDefault = false)
{
    public string Name { get; init; } = NormalizeRequiredToken(Name, nameof(Name));

    public int MinHeight { get; init; } = MinHeight > 0
        ? MinHeight
        : throw new ArgumentOutOfRangeException(nameof(MinHeight), MinHeight, "Minimum height must be greater than zero.");

    public int MaxHeight { get; init; } = MaxHeight >= MinHeight
        ? MaxHeight
        : throw new ArgumentOutOfRangeException(nameof(MaxHeight), MaxHeight, "Maximum height must be greater than or equal to minimum height.");

    public IReadOnlyList<VideoSettingsRange> Ranges { get; init; } = Ranges ?? Array.Empty<VideoSettingsRange>();

    public IReadOnlyList<VideoSettingsQualityRange> QualityRanges { get; init; } = QualityRanges ?? Array.Empty<VideoSettingsQualityRange>();

    public IReadOnlyList<VideoSettingsBoundsOverride> BoundsOverrides { get; init; } = BoundsOverrides ?? Array.Empty<VideoSettingsBoundsOverride>();

    public bool IsDefault { get; init; } = IsDefault;

    public bool Matches(int height)
    {
        return height >= MinHeight && height <= MaxHeight;
    }

    public VideoSettingsRange? ResolveRange(string contentProfile, string qualityProfile)
    {
        var range = Ranges.FirstOrDefault(range => range.Matches(contentProfile, qualityProfile));
        if (range is not null)
        {
            return range;
        }

        var qualityRange = QualityRanges.FirstOrDefault(range => range.Matches(qualityProfile));
        return qualityRange?.ToContentRange(contentProfile);
    }

    public VideoSettingsBoundsOverride? ResolveBoundsOverride(string contentProfile, string qualityProfile)
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
