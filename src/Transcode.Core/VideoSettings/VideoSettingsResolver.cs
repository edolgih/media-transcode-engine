using Transcode.Core.VideoSettings.Profiles;

namespace Transcode.Core.VideoSettings;

/*
Это общий resolver profile-driven video settings.
Он раздельно обслуживает ordinary encode и explicit downscale, но использует один и тот же каталог профилей, bounds и autosample-логику.
*/
/// <summary>
/// Resolves effective profile-driven video settings for encode and explicit downscale paths.
/// </summary>
internal sealed class VideoSettingsResolver
{
    private readonly VideoSettingsProfiles _profiles;
    private readonly VideoSettingsAutoSampler _autoSampler;

    /// <summary>
    /// Initializes a resolver backed by the supplied profile catalog.
    /// </summary>
    public VideoSettingsResolver(VideoSettingsProfiles profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _autoSampler = new VideoSettingsAutoSampler(_profiles);
    }

    public ProfileDrivenVideoSettingsResolution ResolveForEncode(
        VideoSettingsRequest? request,
        int outputHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        string? defaultAutoSampleMode = "fast",
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);

        var profile = _profiles.ResolveOutputProfile(outputHeight);
        var effectiveSelection = BuildEffectiveVideoSettingsSelection(profile, request, defaultAutoSampleMode);
        return ResolveCore(
            profile,
            effectiveSelection,
            request,
            algorithmOverride: null,
            sourceHeightForRanges: null,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider);
    }

    public ProfileDrivenVideoSettingsResolution ResolveForDownscale(
        DownscaleRequest request,
        VideoSettingsRequest? videoSettings,
        int sourceHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        string? defaultAutoSampleMode = "hybrid",
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);

        var profile = _profiles.GetRequiredProfile(request.TargetHeight);
        var effectiveSelection = BuildEffectiveVideoSettingsSelection(profile, videoSettings, defaultAutoSampleMode);
        return ResolveCore(
            profile,
            effectiveSelection,
            videoSettings,
            algorithmOverride: request.Algorithm,
            sourceHeightForRanges: sourceHeight,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider);
    }

    private ProfileDrivenVideoSettingsResolution ResolveCore(
        VideoSettingsProfile profile,
        EffectiveVideoSettingsSelection effectiveSelection,
        VideoSettingsRequest? request,
        string? algorithmOverride,
        int? sourceHeightForRanges,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider)
    {
        var baseSettings = profile.ResolveDefaults(sourceHeightForRanges, effectiveSelection);

        var autoSampleResolution = _autoSampler.ResolveWithDiagnostics(
            profile,
            effectiveSelection,
            request,
            baseSettings,
            sourceHeightForRanges,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider);

        var settings = ApplyOverrides(autoSampleResolution.Settings, request, profile, algorithmOverride);
        return new ProfileDrivenVideoSettingsResolution(profile, effectiveSelection, baseSettings, autoSampleResolution, settings);
    }

    private static EffectiveVideoSettingsSelection BuildEffectiveVideoSettingsSelection(
        VideoSettingsProfile profile,
        VideoSettingsRequest? request,
        string? defaultAutoSampleMode)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new EffectiveVideoSettingsSelection(
            ContentProfile: request?.ContentProfile ?? profile.DefaultContentProfile,
            QualityProfile: request?.QualityProfile ?? profile.DefaultQualityProfile,
            AutoSampleMode: request?.AutoSampleMode ?? defaultAutoSampleMode ?? profile.AutoSampling.ModeDefault);
    }

    private static VideoSettingsDefaults ApplyOverrides(
        VideoSettingsDefaults defaults,
        VideoSettingsRequest? request,
        VideoSettingsProfile profile,
        string? algorithmOverride)
    {
        var cq = request?.Cq ?? defaults.Cq;
        var maxrate = request?.Maxrate;
        var hasManualCq = request?.Cq.HasValue == true;
        var hasManualMaxrate = request?.Maxrate.HasValue == true;

        if (!maxrate.HasValue && hasManualCq)
        {
            var delta = defaults.Cq - cq;
            var resolved = defaults.Maxrate + (delta * profile.RateModel.CqStepToMaxrateStep);
            maxrate = Clamp(resolved, defaults.MaxrateMin, defaults.MaxrateMax);
        }

        maxrate ??= defaults.Maxrate;

        var bufsize = request?.Bufsize;
        if (!bufsize.HasValue && (hasManualMaxrate || hasManualCq))
        {
            bufsize = maxrate.Value * profile.RateModel.BufsizeMultiplier;
        }

        bufsize ??= defaults.Bufsize;

        return new VideoSettingsDefaults(
            ContentProfile: defaults.ContentProfile,
            QualityProfile: defaults.QualityProfile,
            Cq: cq,
            Maxrate: maxrate.Value,
            Bufsize: bufsize.Value,
            Algorithm: algorithmOverride ?? defaults.Algorithm,
            CqMin: defaults.CqMin,
            CqMax: defaults.CqMax,
            MaxrateMin: defaults.MaxrateMin,
            MaxrateMax: defaults.MaxrateMax);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}

/*
Это диагностический результат разрешения video settings.
Он нужен тестам и логированию, но не вводит отдельную доменную модель.
*/
/// <summary>
/// Describes the full resolution result for profile-driven video settings.
/// </summary>
internal sealed record ProfileDrivenVideoSettingsResolution(
    VideoSettingsProfile Profile,
    EffectiveVideoSettingsSelection EffectiveSelection,
    VideoSettingsDefaults BaseSettings,
    VideoSettingsAutoSampleResolution AutoSample,
    VideoSettingsDefaults Settings);
