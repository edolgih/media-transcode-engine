namespace MediaTranscodeEngine.Core.Policy;

public sealed record RateModelSettings(
    double CqStepToMaxrateStep,
    double BufsizeMultiplier);

public sealed record ProfileDefaults(
    int Cq,
    double Maxrate,
    double Bufsize);

public sealed record ProfileLimits(
    int CqMin,
    int CqMax,
    double MaxrateMin,
    double MaxrateMax);

public sealed record ContentProfileSettings(
    string AlgoDefault,
    IReadOnlyDictionary<string, ProfileDefaults> Defaults,
    IReadOnlyDictionary<string, ProfileLimits> Limits);

public sealed record ReductionRange(
    double? MinInclusive = null,
    double? MinExclusive = null,
    double? MaxInclusive = null,
    double? MaxExclusive = null);

public sealed record SourceBucketMatch(
    double? MinHeightInclusive = null,
    double? MinHeightExclusive = null,
    double? MaxHeightInclusive = null,
    double? MaxHeightExclusive = null);

public sealed record SourceBucketSettings(
    string Name,
    SourceBucketMatch? Match = null,
    bool IsDefault = false,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReductionRange>>? ContentQualityRanges = null,
    IReadOnlyDictionary<string, ReductionRange>? QualityRanges = null);

public sealed record AutoSamplingSettings(
    bool EnabledByDefault = true,
    int MaxIterations = 8,
    string ModeDefault = "accurate",
    int HybridAccurateIterations = 2);

public sealed record TranscodePolicyConfig(
    IReadOnlyDictionary<string, ContentProfileSettings> ContentProfiles,
    RateModelSettings RateModel,
    IReadOnlyDictionary<string, ReductionRange>? QualityRanges = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReductionRange>>? ContentQualityRanges = null,
    IReadOnlyList<SourceBucketSettings>? SourceBuckets = null,
    AutoSamplingSettings? AutoSampling = null);

public sealed record TranscodePolicyInput(
    string ContentProfile,
    string QualityProfile,
    int? Cq = null,
    double? Maxrate = null,
    double? Bufsize = null,
    string? DownscaleAlgo = null);

public sealed record TranscodePolicyResult(
    int Cq,
    double Maxrate,
    double Bufsize,
    string DownscaleAlgo);
