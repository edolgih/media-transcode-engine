using MediaTranscodeEngine.Runtime.VideoSettings.Profiles;

namespace MediaTranscodeEngine.Runtime.VideoSettings;

/*
Этот helper выбирает итоговые video settings из общего profile-based механизма.
Он умеет брать bucket по output height, применять profile defaults, autosample и manual overrides.
*/
/// <summary>
/// Resolves profile-driven video settings for ordinary encode and downscale encode paths.
/// </summary>
internal sealed class ProfileDrivenVideoSettingsResolver
{
    private readonly VideoSettingsProfiles _profiles;
    private readonly VideoSettingsAutoSampler _autoSampler;

    public ProfileDrivenVideoSettingsResolver(VideoSettingsProfiles profiles)
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

        if (request?.TargetHeight.HasValue == true)
        {
            throw new ArgumentException("Encode settings request must not specify a target height.", nameof(request));
        }

        var profile = _profiles.ResolveOutputProfile(outputHeight);
        var effectiveRequest = BuildEncodeRequest(request, profile.TargetHeight, defaultAutoSampleMode);
        return ResolveCore(
            profile,
            effectiveRequest,
            sourceHeightForRanges: null,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider);
    }

    public ProfileDrivenVideoSettingsResolution ResolveForDownscale(
        VideoSettingsRequest request,
        int sourceHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        string? defaultAutoSampleMode = "hybrid",
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);

        if (!request.TargetHeight.HasValue)
        {
            throw new ArgumentException("Video settings request for downscale must specify a target height.", nameof(request));
        }

        var profile = _profiles.GetRequiredProfile(request.TargetHeight.Value);
        var effectiveRequest = BuildDownscaleRequest(request, defaultAutoSampleMode);
        return ResolveCore(
            profile,
            effectiveRequest,
            sourceHeight,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider);
    }

    private ProfileDrivenVideoSettingsResolution ResolveCore(
        VideoSettingsProfile profile,
        VideoSettingsRequest effectiveRequest,
        int? sourceHeightForRanges,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider)
    {
        var baseSettings = profile.ResolveDefaults(sourceHeightForRanges, effectiveRequest.ContentProfile, effectiveRequest.QualityProfile);
        var autoSampleResolution = _autoSampler.ResolveWithDiagnostics(
            effectiveRequest,
            baseSettings,
            sourceHeightForRanges,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider);
        var settings = ApplyOverrides(autoSampleResolution.Settings, effectiveRequest, profile);

        return new ProfileDrivenVideoSettingsResolution(profile, effectiveRequest, baseSettings, autoSampleResolution, settings);
    }

    private static VideoSettingsRequest BuildEncodeRequest(
        VideoSettingsRequest? request,
        int targetHeight,
        string? defaultAutoSampleMode)
    {
        return new VideoSettingsRequest(
            targetHeight: targetHeight,
            contentProfile: request?.ContentProfile,
            qualityProfile: request?.QualityProfile,
            autoSampleMode: request?.AutoSampleMode ?? defaultAutoSampleMode,
            algorithm: request?.Algorithm,
            cq: request?.Cq,
            maxrate: request?.Maxrate,
            bufsize: request?.Bufsize);
    }

    private static VideoSettingsRequest BuildDownscaleRequest(VideoSettingsRequest request, string? defaultAutoSampleMode)
    {
        if (defaultAutoSampleMode is null || !string.IsNullOrWhiteSpace(request.AutoSampleMode))
        {
            return request;
        }

        return new VideoSettingsRequest(
            targetHeight: request.TargetHeight,
            contentProfile: request.ContentProfile,
            qualityProfile: request.QualityProfile,
            autoSampleMode: defaultAutoSampleMode,
            algorithm: request.Algorithm,
            cq: request.Cq,
            maxrate: request.Maxrate,
            bufsize: request.Bufsize);
    }

    private static VideoSettingsDefaults ApplyOverrides(VideoSettingsDefaults defaults, VideoSettingsRequest request, VideoSettingsProfile profile)
    {
        var cq = request.Cq ?? defaults.Cq;
        var maxrate = request.Maxrate;

        if (!maxrate.HasValue && request.Cq.HasValue)
        {
            var delta = defaults.Cq - cq;
            var resolved = defaults.Maxrate + (delta * profile.RateModel.CqStepToMaxrateStep);
            maxrate = Clamp(resolved, defaults.MaxrateMin, defaults.MaxrateMax);
        }

        maxrate ??= defaults.Maxrate;

        var bufsize = request.Bufsize;
        if (!bufsize.HasValue && (request.Maxrate.HasValue || request.Cq.HasValue))
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
            Algorithm: request.Algorithm ?? defaults.Algorithm,
            CqMin: defaults.CqMin,
            CqMax: defaults.CqMax,
            MaxrateMin: defaults.MaxrateMin,
            MaxrateMax: defaults.MaxrateMax);
    }

    private static decimal Clamp(decimal value, decimal minInclusive, decimal maxInclusive)
    {
        return value < minInclusive
            ? minInclusive
            : value > maxInclusive
                ? maxInclusive
                : value;
    }
}

/*
Это результат profile-driven выбора video settings.
Он хранит выбранный профиль, базовые defaults, autosample-диагностику и итоговые настройки.
*/
/// <summary>
/// Represents one resolved profile-driven settings selection.
/// </summary>
internal sealed record ProfileDrivenVideoSettingsResolution(
    VideoSettingsProfile Profile,
    VideoSettingsRequest EffectiveRequest,
    VideoSettingsDefaults BaseSettings,
    VideoSettingsAutoSampleResolution AutoSample,
    VideoSettingsDefaults Settings);
