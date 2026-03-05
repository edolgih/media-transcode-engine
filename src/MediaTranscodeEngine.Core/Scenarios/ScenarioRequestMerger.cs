using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Scenarios;

public sealed class ScenarioRequestMerger
{
    private readonly IScenarioPresetRepository _repository;

    public ScenarioRequestMerger(IScenarioPresetRepository repository)
    {
        _repository = repository;
    }

    public RawTranscodeRequest Merge(
        RawTranscodeRequest request,
        IReadOnlySet<string>? explicitTemplateFields = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scenarioName = request.Scenario;
        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            return request;
        }

        var preset = _repository.Get(scenarioName);
        if (preset is null)
        {
            throw new ArgumentException($"Unknown scenario: {scenarioName}", nameof(request));
        }

        var explicitFields = explicitTemplateFields ?? EmptyExplicitFieldSet;

        return request with
        {
            TargetContainer = ResolveString(
                explicitValue: request.TargetContainer,
                defaultValue: RequestContracts.General.DefaultContainer,
                presetValue: preset.TargetContainer,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.TargetContainer))),
            EncoderBackend = ResolveString(
                explicitValue: request.EncoderBackend,
                defaultValue: RequestContracts.General.DefaultEncoderBackend,
                presetValue: preset.EncoderBackend,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.EncoderBackend))),
            VideoPreset = ResolveString(
                explicitValue: request.VideoPreset,
                defaultValue: RequestContracts.General.DefaultVideoPreset,
                presetValue: preset.VideoPreset,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.VideoPreset))),
            TargetVideoCodec = ResolveString(
                explicitValue: request.TargetVideoCodec,
                defaultValue: RequestContracts.General.DefaultTargetVideoCodec,
                presetValue: preset.TargetVideoCodec,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.TargetVideoCodec))),
            PreferH264 = ResolveBool(
                explicitValue: request.PreferH264,
                defaultValue: false,
                presetValue: preset.PreferH264,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.PreferH264))),
            OverlayBg = ResolveBool(
                explicitValue: request.OverlayBg,
                defaultValue: false,
                presetValue: preset.OverlayBg,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.OverlayBg))),
            Downscale = ResolveNullableInt(
                explicitValue: request.Downscale,
                presetValue: preset.Downscale,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.Downscale))),
            DownscaleAlgo = ResolveNullableString(
                explicitValue: request.DownscaleAlgo,
                presetValue: preset.DownscaleAlgo,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.DownscaleAlgo))),
            ContentProfile = ResolveString(
                explicitValue: request.ContentProfile,
                defaultValue: RequestContracts.Transcode.DefaultContentProfile,
                presetValue: preset.ContentProfile,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.ContentProfile))),
            QualityProfile = ResolveString(
                explicitValue: request.QualityProfile,
                defaultValue: RequestContracts.Transcode.DefaultQualityProfile,
                presetValue: preset.QualityProfile,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.QualityProfile))),
            NoAutoSample = ResolveBool(
                explicitValue: request.NoAutoSample,
                defaultValue: false,
                presetValue: preset.NoAutoSample,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.NoAutoSample))),
            AutoSampleMode = ResolveString(
                explicitValue: request.AutoSampleMode,
                defaultValue: RequestContracts.Transcode.DefaultAutoSampleMode,
                presetValue: preset.AutoSampleMode,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.AutoSampleMode))),
            SyncAudio = ResolveBool(
                explicitValue: request.SyncAudio,
                defaultValue: false,
                presetValue: preset.SyncAudio,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.SyncAudio))),
            Cq = ResolveNullableInt(
                explicitValue: request.Cq,
                presetValue: preset.Cq,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.Cq))),
            Maxrate = ResolveNullableDouble(
                explicitValue: request.Maxrate,
                presetValue: preset.Maxrate,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.Maxrate))),
            Bufsize = ResolveNullableDouble(
                explicitValue: request.Bufsize,
                presetValue: preset.Bufsize,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.Bufsize))),
            ForceVideoEncode = ResolveBool(
                explicitValue: request.ForceVideoEncode,
                defaultValue: false,
                presetValue: preset.ForceVideoEncode,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.ForceVideoEncode))),
            KeepFps = ResolveBool(
                explicitValue: request.KeepFps,
                defaultValue: false,
                presetValue: preset.KeepFps,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.KeepFps))),
            UseAq = ResolveBool(
                explicitValue: request.UseAq,
                defaultValue: false,
                presetValue: preset.UseAq,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.UseAq))),
            AqStrength = ResolveInt(
                explicitValue: request.AqStrength,
                defaultValue: RequestContracts.General.DefaultAqStrength,
                presetValue: preset.AqStrength,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.AqStrength))),
            Denoise = ResolveBool(
                explicitValue: request.Denoise,
                defaultValue: false,
                presetValue: preset.Denoise,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.Denoise))),
            FixTimestamps = ResolveBool(
                explicitValue: request.FixTimestamps,
                defaultValue: false,
                presetValue: preset.FixTimestamps,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.FixTimestamps))),
            KeepSource = ResolveBool(
                explicitValue: request.KeepSource,
                defaultValue: false,
                presetValue: preset.KeepSource,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.KeepSource)))
        };
    }

    private static readonly IReadOnlySet<string> EmptyExplicitFieldSet = new HashSet<string>(StringComparer.Ordinal);

    private static bool IsExplicit(
        IReadOnlySet<string> explicitFields,
        string fieldName)
    {
        return explicitFields.Contains(fieldName);
    }

    private static string ResolveString(
        string explicitValue,
        string defaultValue,
        string? presetValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return string.Equals(explicitValue, defaultValue, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(presetValue)
            ? presetValue
            : explicitValue;
    }

    private static string? ResolveNullableString(
        string? explicitValue,
        string? presetValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return string.IsNullOrWhiteSpace(explicitValue) && !string.IsNullOrWhiteSpace(presetValue)
            ? presetValue
            : explicitValue;
    }

    private static bool ResolveBool(
        bool explicitValue,
        bool defaultValue,
        bool? presetValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return explicitValue == defaultValue && presetValue.HasValue
            ? presetValue.Value
            : explicitValue;
    }

    private static int ResolveInt(
        int explicitValue,
        int defaultValue,
        int? presetValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return explicitValue == defaultValue && presetValue.HasValue
            ? presetValue.Value
            : explicitValue;
    }

    private static int? ResolveNullableInt(
        int? explicitValue,
        int? presetValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return explicitValue ?? presetValue;
    }

    private static double? ResolveNullableDouble(
        double? explicitValue,
        double? presetValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return explicitValue ?? presetValue;
    }
}
