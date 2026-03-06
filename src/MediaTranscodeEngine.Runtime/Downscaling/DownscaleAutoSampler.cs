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
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(baseSettings);

        if (request.NoAutoSample || request.Cq.HasValue || request.Maxrate.HasValue || request.Bufsize.HasValue)
        {
            return baseSettings;
        }

        if (!request.TargetHeight.HasValue)
        {
            return baseSettings;
        }

        var profile = _profiles.GetRequiredProfile(request.TargetHeight.Value);
        if (string.IsNullOrWhiteSpace(request.AutoSampleMode) && !profile.AutoSampling.EnabledByDefault)
        {
            return baseSettings;
        }

        if (duration <= TimeSpan.Zero)
        {
            return baseSettings;
        }

        var range = profile.ResolveRange(sourceHeight, request.ContentProfile, request.QualityProfile);
        if (range is null)
        {
            return baseSettings;
        }

        var mode = NormalizeMode(request.AutoSampleMode) ?? profile.AutoSampling.ModeDefault;
        if (mode.Equals("fast", StringComparison.OrdinalIgnoreCase))
        {
            return RunFast(profile, baseSettings, range, sourceBitrate, hasAudio).Settings;
        }

        if (mode.Equals("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return RunHybrid(profile, baseSettings, range, duration, sourceBitrate, hasAudio, accurateReductionProvider).Settings;
        }

        return RunAccurate(profile, baseSettings, range, duration, accurateReductionProvider, profile.AutoSampling.MaxIterations).Settings;
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
            return DownscaleAutoSampleResult.FromSettings(baseSettings);
        }

        return RunLoop(
            profile,
            baseSettings,
            range,
            profile.AutoSampling.MaxIterations,
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
        TimeSpan duration,
        Func<DownscaleDefaults, IReadOnlyList<DownscaleSampleWindow>, decimal?>? accurateReductionProvider,
        int maxIterations)
    {
        var windows = profile.GetSampleWindows(duration);
        if (accurateReductionProvider is null || windows.Count == 0)
        {
            return DownscaleAutoSampleResult.FromSettings(baseSettings);
        }

        return RunLoop(
            profile,
            baseSettings,
            range,
            maxIterations,
            settings => accurateReductionProvider(settings, windows));
    }

    private static DownscaleAutoSampleResult RunHybrid(
        DownscaleProfile profile,
        DownscaleDefaults baseSettings,
        DownscaleRange range,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<DownscaleDefaults, IReadOnlyList<DownscaleSampleWindow>, decimal?>? accurateReductionProvider)
    {
        var fastResult = RunFast(profile, baseSettings, range, sourceBitrate, hasAudio);
        if (fastResult.InBounds)
        {
            return fastResult;
        }

        var windows = profile.GetSampleWindows(duration);
        if (accurateReductionProvider is null || windows.Count == 0)
        {
            return fastResult;
        }

        var iterations = Math.Min(profile.AutoSampling.MaxIterations, Math.Max(profile.AutoSampling.HybridAccurateIterations, 1));
        return RunLoop(
            profile,
            fastResult.Settings,
            range,
            iterations,
            settings => accurateReductionProvider(settings, windows));
    }

    private static DownscaleAutoSampleResult RunLoop(
        DownscaleProfile profile,
        DownscaleDefaults startSettings,
        DownscaleRange range,
        int maxIterations,
        Func<DownscaleDefaults, decimal?> reductionProvider)
    {
        var current = startSettings;
        decimal? lastReduction = null;
        var inBounds = false;

        for (var i = 0; i < maxIterations; i++)
        {
            var reduction = reductionProvider(current);
            if (!reduction.HasValue)
            {
                break;
            }

            lastReduction = reduction.Value;
            if (range.Contains(reduction.Value))
            {
                inBounds = true;
                break;
            }

            var previous = current;
            current = Adjust(current, range, reduction.Value, profile.RateModel);
            if (previous.Cq == current.Cq && previous.Maxrate == current.Maxrate)
            {
                break;
            }
        }

        return new DownscaleAutoSampleResult(current, lastReduction, inBounds);
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
    bool InBounds)
{
    public static DownscaleAutoSampleResult FromSettings(DownscaleDefaults settings)
    {
        return new DownscaleAutoSampleResult(settings, LastReduction: null, InBounds: false);
    }
}
