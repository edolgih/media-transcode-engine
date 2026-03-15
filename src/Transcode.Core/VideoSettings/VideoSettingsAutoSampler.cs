using Transcode.Core.VideoSettings.Profiles;

namespace Transcode.Core.VideoSettings;

/*
Это общий механизм autosample для video settings.
Он используется и для ordinary encode, и для explicit downscale, когда сценарий хочет profile-driven подбор внутри corridor профиля.
*/
/// <summary>
/// Resolves autosampled video settings from a profile, a resolved selection, optional overrides, and source facts.
/// </summary>
internal sealed class VideoSettingsAutoSampler
{
    /// <summary>
    /// Initializes an autosampler backed by the supplied profile catalog.
    /// </summary>
    public VideoSettingsAutoSampler(VideoSettingsProfiles profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
    }

    public VideoSettingsDefaults Resolve(
        VideoSettingsProfile profile,
        EffectiveVideoSettingsSelection selection,
        VideoSettingsRequest? overrides,
        VideoSettingsDefaults baseSettings,
        int? sourceHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider = null)
    {
        return ResolveWithDiagnostics(
            profile,
            selection,
            overrides,
            baseSettings,
            sourceHeight,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider).Settings;
    }

    internal VideoSettingsAutoSampleResolution ResolveWithDiagnostics(
        VideoSettingsProfile profile,
        EffectiveVideoSettingsSelection selection,
        VideoSettingsRequest? overrides,
        VideoSettingsDefaults baseSettings,
        int? sourceHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(baseSettings);

        if (HasManualRateOverrides(overrides))
        {
            return VideoSettingsAutoSampleResolution.Skip(baseSettings, "manual_override");
        }

        if (duration <= TimeSpan.Zero)
        {
            return VideoSettingsAutoSampleResolution.Skip(baseSettings, "invalid_duration");
        }

        var corridor = profile.ResolveRange(sourceHeight, selection);

        if (corridor is null)
        {
            return VideoSettingsAutoSampleResolution.Skip(baseSettings, "missing_corridor");
        }

        var mode = selection.AutoSampleMode;

        return mode switch
        {
            "fast" => ResolveFast(profile, baseSettings, corridor, sourceBitrate, duration, hasAudio),
            "hybrid" => ResolveHybrid(profile, baseSettings, corridor, duration, sourceBitrate, hasAudio, accurateReductionProvider),
            _ => RunAccurate(profile, baseSettings, corridor, duration, accurateReductionProvider)
        };
    }

    private static VideoSettingsAutoSampleResolution ResolveFast(
        VideoSettingsProfile profile,
        VideoSettingsDefaults baseSettings,
        VideoSettingsRange corridor,
        long? sourceBitrate,
        TimeSpan duration,
        bool hasAudio)
    {
        var sourceBitrateResolution = ResolveSourceBitrate(sourceBitrate, duration, hasAudio);
        if (!sourceBitrateResolution.Bitrate.HasValue)
        {
            return VideoSettingsAutoSampleResolution.Skip(baseSettings, sourceBitrateResolution.Reason);
        }

        return RunFast(profile, baseSettings, corridor, sourceBitrateResolution.Bitrate.Value);
    }

    private static VideoSettingsAutoSampleResolution ResolveHybrid(
        VideoSettingsProfile profile,
        VideoSettingsDefaults baseSettings,
        VideoSettingsRange corridor,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider)
    {
        var sourceBitrateResolution = ResolveSourceBitrate(sourceBitrate, duration, hasAudio);
        if (!sourceBitrateResolution.Bitrate.HasValue)
        {
            if (accurateReductionProvider is null)
            {
                return VideoSettingsAutoSampleResolution.Skip(baseSettings, sourceBitrateResolution.Reason);
            }

            var accurateResult = RunAccurate(profile, baseSettings, corridor, duration, accurateReductionProvider);
            return accurateResult with { Mode = "hybrid", Path = "sample_only" };
        }

        return RunHybrid(profile, baseSettings, corridor, duration, sourceBitrateResolution.Bitrate.Value, accurateReductionProvider);
    }

    private static VideoSettingsAutoSampleResolution RunFast(
        VideoSettingsProfile profile,
        VideoSettingsDefaults baseSettings,
        VideoSettingsRange corridor,
        long sourceBitrate)
    {
        var settings = baseSettings;
        decimal? lastReduction = null;

        for (var iteration = 1; iteration <= profile.AutoSampling.MaxIterations; iteration++)
        {
            lastReduction = EstimateReductionPercent(sourceBitrate, settings);
            if (lastReduction is null)
            {
                return VideoSettingsAutoSampleResolution.Skip(baseSettings, "estimate_failed");
            }

            var inBounds = corridor.Contains(lastReduction.Value);
            if (inBounds)
            {
                return VideoSettingsAutoSampleResolution.Success(
                    settings,
                    mode: "fast",
                    path: "estimate",
                    corridor,
                    windows: Array.Empty<VideoSettingsSampleWindow>(),
                    iterationCount: iteration,
                    lastReductionPercent: lastReduction,
                    inBounds: true);
            }

            settings = StepTowardsCorridor(settings, corridor, lastReduction.Value, profile);
        }

        return VideoSettingsAutoSampleResolution.Success(
            settings,
            mode: "fast",
            path: "estimate",
            corridor,
            windows: Array.Empty<VideoSettingsSampleWindow>(),
            iterationCount: profile.AutoSampling.MaxIterations,
            lastReductionPercent: lastReduction,
            inBounds: lastReduction.HasValue && corridor.Contains(lastReduction.Value));
    }

    private static VideoSettingsAutoSampleResolution RunHybrid(
        VideoSettingsProfile profile,
        VideoSettingsDefaults baseSettings,
        VideoSettingsRange corridor,
        TimeSpan duration,
        long sourceBitrate,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider)
    {
        var fastResult = RunFast(profile, baseSettings, corridor, sourceBitrate);
        if (fastResult.InBounds || accurateReductionProvider is null)
        {
            return fastResult with { Mode = "hybrid", Path = fastResult.InBounds ? "estimate" : "estimate_only" };
        }

        var accurateResult = RunAccurate(profile, fastResult.Settings, corridor, duration, accurateReductionProvider);
        return accurateResult with { Mode = "hybrid", Path = "estimate+sample" };
    }

    private static VideoSettingsAutoSampleResolution RunAccurate(
        VideoSettingsProfile profile,
        VideoSettingsDefaults baseSettings,
        VideoSettingsRange corridor,
        TimeSpan duration,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider)
    {
        if (accurateReductionProvider is null)
        {
            return VideoSettingsAutoSampleResolution.Skip(baseSettings, "accurate_provider_missing");
        }

        var windows = profile.GetSampleWindows(duration);
        var settings = baseSettings;
        decimal? lastReduction = null;

        for (var iteration = 1; iteration <= profile.AutoSampling.MaxIterations; iteration++)
        {
            lastReduction = accurateReductionProvider(settings, windows);
            if (lastReduction is null)
            {
                return VideoSettingsAutoSampleResolution.Skip(baseSettings, "sample_failed");
            }

            var inBounds = corridor.Contains(lastReduction.Value);
            if (inBounds)
            {
                return VideoSettingsAutoSampleResolution.Success(
                    settings,
                    mode: "accurate",
                    path: "sample",
                    corridor,
                    windows,
                    iterationCount: iteration,
                    lastReductionPercent: lastReduction,
                    inBounds: true);
            }

            settings = StepTowardsCorridor(settings, corridor, lastReduction.Value, profile);
        }

        return VideoSettingsAutoSampleResolution.Success(
            settings,
            mode: "accurate",
            path: "sample",
            corridor,
            windows,
            iterationCount: profile.AutoSampling.MaxIterations,
            lastReductionPercent: lastReduction,
            inBounds: lastReduction.HasValue && corridor.Contains(lastReduction.Value));
    }

    private static bool HasManualRateOverrides(VideoSettingsRequest? overrides)
    {
        return overrides?.Cq.HasValue == true ||
               overrides?.Maxrate.HasValue == true ||
               overrides?.Bufsize.HasValue == true;
    }

    private static VideoSettingsSourceBitrateResolution ResolveSourceBitrate(long? sourceBitrate, TimeSpan duration, bool hasAudio)
    {
        if (sourceBitrate.HasValue && sourceBitrate.Value > 0)
        {
            return new VideoSettingsSourceBitrateResolution(sourceBitrate.Value, "metadata");
        }

        return duration > TimeSpan.Zero
            ? new VideoSettingsSourceBitrateResolution(null, "missing_source_bitrate")
            : new VideoSettingsSourceBitrateResolution(null, "invalid_duration");
    }

    private static decimal? EstimateReductionPercent(long sourceBitrate, VideoSettingsDefaults settings)
    {
        if (sourceBitrate <= 0)
        {
            return null;
        }

        var targetBitrate = settings.Maxrate * 1_000_000m;
        if (targetBitrate <= 0m)
        {
            return null;
        }

        var reduction = (1m - (targetBitrate / sourceBitrate)) * 100m;
        return decimal.Round(reduction, 2, MidpointRounding.AwayFromZero);
    }

    private static VideoSettingsDefaults StepTowardsCorridor(
        VideoSettingsDefaults settings,
        VideoSettingsRange corridor,
        decimal reductionPercent,
        VideoSettingsProfile profile)
    {
        if (IsBelowCorridor(corridor, reductionPercent))
        {
            return Step(settings, profile, makeSmaller: true);
        }

        if (IsAboveCorridor(corridor, reductionPercent))
        {
            return Step(settings, profile, makeSmaller: false);
        }

        return settings;
    }

    private static VideoSettingsDefaults Step(VideoSettingsDefaults settings, VideoSettingsProfile profile, bool makeSmaller)
    {
        var cqDelta = makeSmaller ? 1 : -1;
        var nextCq = Clamp(settings.Cq + cqDelta, settings.CqMin, settings.CqMax);

        var maxrateDelta = makeSmaller
            ? -profile.RateModel.CqStepToMaxrateStep
            : profile.RateModel.CqStepToMaxrateStep;
        var nextMaxrate = Clamp(settings.Maxrate + maxrateDelta, settings.MaxrateMin, settings.MaxrateMax);
        var nextBufsize = nextMaxrate * profile.RateModel.BufsizeMultiplier;

        return settings with
        {
            Cq = nextCq,
            Maxrate = nextMaxrate,
            Bufsize = nextBufsize
        };
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static bool IsBelowCorridor(VideoSettingsRange corridor, decimal value)
    {
        return corridor.MinInclusive.HasValue && value < corridor.MinInclusive.Value ||
               corridor.MinExclusive.HasValue && value <= corridor.MinExclusive.Value;
    }

    private static bool IsAboveCorridor(VideoSettingsRange corridor, decimal value)
    {
        return corridor.MaxInclusive.HasValue && value > corridor.MaxInclusive.Value ||
               corridor.MaxExclusive.HasValue && value >= corridor.MaxExclusive.Value;
    }
}

/*
Это узкий результат autosample.
Он остается локальным рядом с алгоритмом, потому что нужен только resolver и тестам.
*/
/// <summary>
/// Describes the autosampling result and diagnostics for video settings.
/// </summary>
internal sealed record VideoSettingsAutoSampleResolution(
    VideoSettingsDefaults Settings,
    string Mode,
    string Path,
    string Reason,
    VideoSettingsRange? Corridor,
    IReadOnlyList<VideoSettingsSampleWindow> Windows,
    int IterationCount,
    decimal? LastReductionPercent,
    bool InBounds)
{
    public static VideoSettingsAutoSampleResolution Skip(VideoSettingsDefaults settings, string reason)
    {
        return new VideoSettingsAutoSampleResolution(
            Settings: settings,
            Mode: "none",
            Path: "skip",
            Reason: reason,
            Corridor: null,
            Windows: Array.Empty<VideoSettingsSampleWindow>(),
            IterationCount: 0,
            LastReductionPercent: null,
            InBounds: false);
    }

    public static VideoSettingsAutoSampleResolution Success(
        VideoSettingsDefaults settings,
        string mode,
        string path,
        VideoSettingsRange corridor,
        IReadOnlyList<VideoSettingsSampleWindow> windows,
        int iterationCount,
        decimal? lastReductionPercent,
        bool inBounds)
    {
        return new VideoSettingsAutoSampleResolution(
            Settings: settings,
            Mode: mode,
            Path: path,
            Reason: "resolved",
            Corridor: corridor,
            Windows: windows,
            IterationCount: iterationCount,
            LastReductionPercent: lastReductionPercent,
            InBounds: inBounds);
    }
}

/*
Это локальный результат определения source bitrate для autosample.
Он хранит и само значение, и причину, почему оно было получено или не было получено.
*/
/// <summary>
/// Describes the source bitrate resolved for autosample together with the resolution reason.
/// </summary>
internal sealed record VideoSettingsSourceBitrateResolution(long? Bitrate, string Reason);
