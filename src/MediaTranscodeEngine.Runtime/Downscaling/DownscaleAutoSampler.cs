namespace MediaTranscodeEngine.Runtime.Downscaling;

/// <summary>
/// Resolves downscale autosample settings from profile data and measured reduction values.
/// </summary>
internal sealed class DownscaleAutoSampler
{
    private readonly DownscaleProfiles _profiles;

    public DownscaleAutoSampler(DownscaleProfiles profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public DownscaleDefaults Resolve(
        DownscaleRequest request,
        DownscaleDefaults baseSettings,
        int? sourceHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<DownscaleDefaults, IReadOnlyList<DownscaleSampleWindow>, decimal?>? accurateReductionProvider = null)
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

    internal DownscaleAutoSampleResolution ResolveWithDiagnostics(
        DownscaleRequest request,
        DownscaleDefaults baseSettings,
        int? sourceHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<DownscaleDefaults, IReadOnlyList<DownscaleSampleWindow>, decimal?>? accurateReductionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(baseSettings);

        if (request.NoAutoSample)
        {
            return DownscaleAutoSampleResolution.Skip(baseSettings, "disabled_by_request");
        }

        if (request.Cq.HasValue || request.Maxrate.HasValue || request.Bufsize.HasValue)
        {
            return DownscaleAutoSampleResolution.Skip(baseSettings, "manual_override");
        }

        if (!request.TargetHeight.HasValue)
        {
            return DownscaleAutoSampleResolution.Skip(baseSettings, "no_target_height");
        }

        var profile = _profiles.GetRequiredProfile(request.TargetHeight.Value);
        if (string.IsNullOrWhiteSpace(request.AutoSampleMode) && !profile.AutoSampling.EnabledByDefault)
        {
            return DownscaleAutoSampleResolution.Skip(baseSettings, "disabled_by_profile");
        }

        if (duration <= TimeSpan.Zero)
        {
            return DownscaleAutoSampleResolution.Skip(baseSettings, "missing_duration");
        }

        var range = profile.ResolveRange(sourceHeight, request.ContentProfile, request.QualityProfile);
        if (range is null)
        {
            return DownscaleAutoSampleResolution.Skip(baseSettings, "missing_range");
        }

        var mode = NormalizeMode(request.AutoSampleMode) ?? profile.AutoSampling.ModeDefault;
        if (mode.Equals("fast", StringComparison.OrdinalIgnoreCase))
        {
            return DownscaleAutoSampleResolution.FromResult(
                mode,
                range,
                [],
                RunFast(profile, baseSettings, range, sourceBitrate, hasAudio));
        }

        var windows = profile.GetSampleWindows(duration);
        if (mode.Equals("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            var result = RunHybrid(profile, baseSettings, range, windows, sourceBitrate, hasAudio, accurateReductionProvider);
            return DownscaleAutoSampleResolution.FromResult(
                mode,
                range,
                result.Path == "hybrid-accurate" ? windows : [],
                result);
        }

        return DownscaleAutoSampleResolution.FromResult(
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

    private static DownscaleAutoSampleResult RunFast(
        DownscaleProfile profile,
        DownscaleDefaults baseSettings,
        DownscaleRange range,
        long? sourceBitrate,
        bool hasAudio)
    {
        if (!sourceBitrate.HasValue || sourceBitrate.Value <= 0)
        {
            return DownscaleAutoSampleResult.FromSettings(baseSettings, "fast", "missing_source_bitrate");
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

    private static DownscaleAutoSampleResult RunAccurate(
        DownscaleProfile profile,
        DownscaleDefaults baseSettings,
        DownscaleRange range,
        IReadOnlyList<DownscaleSampleWindow> windows,
        Func<DownscaleDefaults, IReadOnlyList<DownscaleSampleWindow>, decimal?>? accurateReductionProvider,
        int maxIterations)
    {
        if (accurateReductionProvider is null || windows.Count == 0)
        {
            return DownscaleAutoSampleResult.FromSettings(baseSettings, "accurate", "missing_accurate_measurement");
        }

        return RunLoop(
            profile,
            baseSettings,
            range,
            maxIterations,
            "accurate",
            settings => accurateReductionProvider(settings, windows));
    }

    private static DownscaleAutoSampleResult RunHybrid(
        DownscaleProfile profile,
        DownscaleDefaults baseSettings,
        DownscaleRange range,
        IReadOnlyList<DownscaleSampleWindow> windows,
        long? sourceBitrate,
        bool hasAudio,
        Func<DownscaleDefaults, IReadOnlyList<DownscaleSampleWindow>, decimal?>? accurateReductionProvider)
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

    private static DownscaleAutoSampleResult RunLoop(
        DownscaleProfile profile,
        DownscaleDefaults startSettings,
        DownscaleRange range,
        int maxIterations,
        string path,
        Func<DownscaleDefaults, decimal?> reductionProvider)
    {
        var current = startSettings;
        decimal? lastReduction = null;
        var iterations = 0;

        for (var i = 0; i < maxIterations; i++)
        {
            var reduction = reductionProvider(current);
            if (!reduction.HasValue)
            {
                return new DownscaleAutoSampleResult(current, lastReduction, InBounds: false, iterations, "missing_reduction", path);
            }

            iterations++;
            lastReduction = reduction.Value;
            if (range.Contains(reduction.Value))
            {
                return new DownscaleAutoSampleResult(current, lastReduction, InBounds: true, iterations, "in_range", path);
            }

            var previous = current;
            current = Adjust(current, range, reduction.Value, profile.RateModel);
            if (previous.Cq == current.Cq && previous.Maxrate == current.Maxrate)
            {
                return new DownscaleAutoSampleResult(current, lastReduction, InBounds: false, iterations, "no_movement", path);
            }
        }

        return new DownscaleAutoSampleResult(current, lastReduction, InBounds: false, iterations, "max_iterations", path);
    }

    private static DownscaleDefaults Adjust(
        DownscaleDefaults current,
        DownscaleRange range,
        decimal reduction,
        DownscaleRateModel rateModel)
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

    private static bool IsBelowRange(DownscaleRange range, decimal value)
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

internal sealed record DownscaleAutoSampleResult(
    DownscaleDefaults Settings,
    decimal? LastReduction,
    bool InBounds,
    int Iterations,
    string Reason,
    string Path)
{
    public static DownscaleAutoSampleResult FromSettings(DownscaleDefaults settings, string path, string reason)
    {
        return new DownscaleAutoSampleResult(settings, LastReduction: null, InBounds: false, Iterations: 0, Reason: reason, Path: path);
    }
}

internal sealed record DownscaleAutoSampleResolution(
    DownscaleDefaults Settings,
    string Mode,
    string Path,
    string Reason,
    DownscaleRange? Range,
    IReadOnlyList<DownscaleSampleWindow> Windows,
    decimal? LastReduction,
    bool InBounds,
    int Iterations)
{
    public static DownscaleAutoSampleResolution Skip(DownscaleDefaults settings, string reason)
    {
        return new DownscaleAutoSampleResolution(
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

    public static DownscaleAutoSampleResolution FromResult(
        string mode,
        DownscaleRange range,
        IReadOnlyList<DownscaleSampleWindow> windows,
        DownscaleAutoSampleResult result)
    {
        return new DownscaleAutoSampleResolution(
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
