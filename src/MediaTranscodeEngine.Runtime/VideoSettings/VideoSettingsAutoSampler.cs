using MediaTranscodeEngine.Runtime.VideoSettings.Profiles;

namespace MediaTranscodeEngine.Runtime.VideoSettings;

/*
Этот компонент решает autosample-путь для video settings.
Он берёт профиль, source facts и measured reduction и выбирает итоговые настройки.
*/
/// <summary>
/// Resolves video-settings autosample settings from profile data and measured reduction values.
/// </summary>
internal sealed class VideoSettingsAutoSampler
{
    private readonly VideoSettingsProfiles _profiles;

    public VideoSettingsAutoSampler(VideoSettingsProfiles profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public VideoSettingsDefaults Resolve(
        VideoSettingsRequest request,
        VideoSettingsDefaults baseSettings,
        int? sourceHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider = null)
    {
        return ResolveWithDiagnostics(
            request,
            baseSettings,
            sourceHeight,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider).Settings;
    }

    internal VideoSettingsAutoSampleResolution ResolveWithDiagnostics(
        VideoSettingsRequest request,
        VideoSettingsDefaults baseSettings,
        int? sourceHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(baseSettings);

        if (request.Cq.HasValue || request.Maxrate.HasValue || request.Bufsize.HasValue)
        {
            return VideoSettingsAutoSampleResolution.Skip(baseSettings, "manual_override");
        }

        if (!request.TargetHeight.HasValue)
        {
            return VideoSettingsAutoSampleResolution.Skip(baseSettings, "no_target_height");
        }

        var profile = _profiles.GetRequiredProfile(request.TargetHeight.Value);
        if (string.IsNullOrWhiteSpace(request.AutoSampleMode) && !profile.AutoSampling.EnabledByDefault)
        {
            return VideoSettingsAutoSampleResolution.Skip(baseSettings, "disabled_by_profile");
        }

        if (duration <= TimeSpan.Zero)
        {
            return VideoSettingsAutoSampleResolution.Skip(baseSettings, "missing_duration");
        }

        var range = profile.ResolveRange(sourceHeight, request.ContentProfile, request.QualityProfile);
        if (range is null)
        {
            return VideoSettingsAutoSampleResolution.Skip(baseSettings, "missing_range");
        }

        var mode = NormalizeMode(request.AutoSampleMode) ?? profile.AutoSampling.ModeDefault;
        if (mode.Equals("fast", StringComparison.OrdinalIgnoreCase))
        {
            return VideoSettingsAutoSampleResolution.FromResult(
                mode,
                range,
                [],
                RunFast(profile, baseSettings, range, sourceBitrate, hasAudio));
        }

        var windows = profile.GetSampleWindows(duration);
        if (mode.Equals("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            var result = RunHybrid(profile, baseSettings, range, windows, sourceBitrate, hasAudio, accurateReductionProvider);
            return VideoSettingsAutoSampleResolution.FromResult(
                mode,
                range,
                result.Path == "hybrid-accurate" ? windows : [],
                result);
        }

        return VideoSettingsAutoSampleResolution.FromResult(
            mode,
            range,
            windows,
            RunAccurate(profile, baseSettings, range, windows, accurateReductionProvider, profile.AutoSampling.MaxIterations));
    }

    private static string? NormalizeMode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private static VideoSettingsAutoSampleResult RunFast(
        VideoSettingsProfile profile,
        VideoSettingsDefaults baseSettings,
        VideoSettingsRange range,
        long? sourceBitrate,
        bool hasAudio)
    {
        if (!sourceBitrate.HasValue || sourceBitrate.Value <= 0)
        {
            return VideoSettingsAutoSampleResult.FromSettings(baseSettings, "fast", "missing_source_bitrate");
        }

        return RunLoop(
            profile,
            baseSettings,
            range,
            profile.AutoSampling.MaxIterations,
            "fast",
            settings => EstimateReductionFromBitrate(
                sourceBitrateBps: sourceBitrate.Value,
                maxrateMbps: settings.Maxrate,
                hasAudio: hasAudio,
                audioBitrateEstimateMbps: profile.AutoSampling.AudioBitrateEstimateMbps));
    }

    private static VideoSettingsAutoSampleResult RunAccurate(
        VideoSettingsProfile profile,
        VideoSettingsDefaults baseSettings,
        VideoSettingsRange range,
        IReadOnlyList<VideoSettingsSampleWindow> windows,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider,
        int maxIterations)
    {
        if (accurateReductionProvider is null || windows.Count == 0)
        {
            return VideoSettingsAutoSampleResult.FromSettings(baseSettings, "accurate", "missing_accurate_measurement");
        }

        return RunLoop(
            profile,
            baseSettings,
            range,
            maxIterations,
            "accurate",
            settings => accurateReductionProvider(settings, windows));
    }

    private static VideoSettingsAutoSampleResult RunHybrid(
        VideoSettingsProfile profile,
        VideoSettingsDefaults baseSettings,
        VideoSettingsRange range,
        IReadOnlyList<VideoSettingsSampleWindow> windows,
        long? sourceBitrate,
        bool hasAudio,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider)
    {
        var fastResult = RunFast(profile, baseSettings, range, sourceBitrate, hasAudio);
        if (fastResult.InBounds)
        {
            return fastResult with { Path = "hybrid-fast" };
        }

        if (accurateReductionProvider is null || windows.Count == 0)
        {
            return fastResult with { Path = "hybrid-fast", Reason = "missing_accurate_measurement" };
        }

        var iterations = Math.Min(profile.AutoSampling.MaxIterations, Math.Max(profile.AutoSampling.HybridAccurateIterations, 1));
        return RunLoop(
            profile,
            fastResult.Settings,
            range,
            iterations,
            "hybrid-accurate",
            settings => accurateReductionProvider(settings, windows));
    }

    private static VideoSettingsAutoSampleResult RunLoop(
        VideoSettingsProfile profile,
        VideoSettingsDefaults startSettings,
        VideoSettingsRange range,
        int maxIterations,
        string path,
        Func<VideoSettingsDefaults, decimal?> reductionProvider)
    {
        var current = startSettings;
        decimal? lastReduction = null;
        var iterations = 0;

        for (var i = 0; i < maxIterations; i++)
        {
            var reduction = reductionProvider(current);
            if (!reduction.HasValue)
            {
                return new VideoSettingsAutoSampleResult(current, lastReduction, InBounds: false, iterations, "missing_reduction", path);
            }

            iterations++;
            lastReduction = reduction.Value;
            if (range.Contains(reduction.Value))
            {
                return new VideoSettingsAutoSampleResult(current, lastReduction, InBounds: true, iterations, "in_range", path);
            }

            var previous = current;
            current = Adjust(current, range, reduction.Value, profile.RateModel);
            if (previous.Cq == current.Cq && previous.Maxrate == current.Maxrate)
            {
                return new VideoSettingsAutoSampleResult(current, lastReduction, InBounds: false, iterations, "no_movement", path);
            }
        }

        return new VideoSettingsAutoSampleResult(current, lastReduction, InBounds: false, iterations, "max_iterations", path);
    }

    private static VideoSettingsDefaults Adjust(
        VideoSettingsDefaults current,
        VideoSettingsRange range,
        decimal reduction,
        VideoSettingsRateModel rateModel)
    {
        var cq = current.Cq;
        var maxrate = current.Maxrate;

        if (IsBelowRange(range, reduction))
        {
            if (cq < current.CqMax)
            {
                cq++;
            }

            maxrate = Math.Max(maxrate - rateModel.CqStepToMaxrateStep, current.MaxrateMin);
        }
        else
        {
            if (cq > current.CqMin)
            {
                cq--;
            }

            maxrate = Math.Min(maxrate + rateModel.CqStepToMaxrateStep, current.MaxrateMax);
        }

        return current with
        {
            Cq = cq,
            Maxrate = maxrate,
            Bufsize = maxrate * rateModel.BufsizeMultiplier
        };
    }

    private static bool IsBelowRange(VideoSettingsRange range, decimal value)
    {
        if (range.MinInclusive.HasValue)
        {
            return value < range.MinInclusive.Value;
        }

        if (range.MinExclusive.HasValue)
        {
            return value <= range.MinExclusive.Value;
        }

        return false;
    }

    private static decimal? EstimateReductionFromBitrate(
        long sourceBitrateBps,
        decimal maxrateMbps,
        bool hasAudio,
        decimal audioBitrateEstimateMbps)
    {
        var sourceMbps = sourceBitrateBps / 1_000_000m;
        if (sourceMbps <= 0m)
        {
            return null;
        }

        var targetMbps = maxrateMbps;
        if (hasAudio)
        {
            targetMbps += audioBitrateEstimateMbps;
        }

        if (targetMbps < 0m)
        {
            targetMbps = 0m;
        }

        var reduction = (1m - (targetMbps / sourceMbps)) * 100m;
        reduction = Math.Max(-100m, Math.Min(100m, reduction));
        return Math.Round(reduction, 2, MidpointRounding.AwayFromZero);
    }
}

/*
Это внутренний результат одной autosample-итерации.
Он хранит вычисленные настройки и признаки попадания в corridor.
*/
/// <summary>
/// Represents one intermediate autosample result before the final diagnostics payload is assembled.
/// </summary>
internal sealed record VideoSettingsAutoSampleResult(
    VideoSettingsDefaults Settings,
    decimal? LastReduction,
    bool InBounds,
    int Iterations,
    string Reason,
    string Path)
{
    public static VideoSettingsAutoSampleResult FromSettings(VideoSettingsDefaults settings, string path, string reason)
    {
        return new VideoSettingsAutoSampleResult(settings, LastReduction: null, InBounds: false, Iterations: 0, Reason: reason, Path: path);
    }
}

/*
Это итоговое разрешение autosample.
Оно хранит финальные настройки и диагностику выбранного пути для логирования.
*/
/// <summary>
/// Represents the final autosample resolution together with diagnostics about the chosen path.
/// </summary>
internal sealed record VideoSettingsAutoSampleResolution(
    VideoSettingsDefaults Settings,
    string Mode,
    string Path,
    string Reason,
    VideoSettingsRange? Range,
    IReadOnlyList<VideoSettingsSampleWindow> Windows,
    decimal? LastReduction,
    bool InBounds,
    int Iterations)
{
    public static VideoSettingsAutoSampleResolution Skip(VideoSettingsDefaults settings, string reason)
    {
        return new VideoSettingsAutoSampleResolution(
            settings,
            Mode: "none",
            Path: "skip",
            Reason: reason,
            Range: null,
            Windows: [],
            LastReduction: null,
            InBounds: false,
            Iterations: 0);
    }

    public static VideoSettingsAutoSampleResolution FromResult(
        string mode,
        VideoSettingsRange range,
        IReadOnlyList<VideoSettingsSampleWindow> windows,
        VideoSettingsAutoSampleResult result)
    {
        return new VideoSettingsAutoSampleResolution(
            Settings: result.Settings,
            Mode: mode,
            Path: result.Path,
            Reason: result.Reason,
            Range: range,
            Windows: windows,
            LastReduction: result.LastReduction,
            InBounds: result.InBounds,
            Iterations: result.Iterations);
    }
}
