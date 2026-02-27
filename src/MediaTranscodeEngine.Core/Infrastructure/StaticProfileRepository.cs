using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Infrastructure;

public sealed class StaticProfileRepository : IProfileRepository
{
    private readonly TranscodePolicyConfig _config;

    public StaticProfileRepository(TranscodePolicyConfig? config = null)
    {
        _config = config ?? CreateDefaultConfig();
    }

    public TranscodePolicyConfig Get576Config() => _config;

    private static TranscodePolicyConfig CreateDefaultConfig()
    {
        var animeDefaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileDefaults(Cq: 22, Maxrate: 2.8, Bufsize: 5.6),
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8),
            ["low"] = new ProfileDefaults(Cq: 24, Maxrate: 2.1, Bufsize: 4.2)
        };

        var filmDefaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileDefaults(Cq: 20, Maxrate: 2.8, Bufsize: 5.6),
            ["default"] = new ProfileDefaults(Cq: 21, Maxrate: 2.2, Bufsize: 4.4),
            ["low"] = new ProfileDefaults(Cq: 22, Maxrate: 2.0, Bufsize: 4.0)
        };

        var commonLimits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileLimits(CqMin: 18, CqMax: 25, MaxrateMin: 2.2, MaxrateMax: 3.6),
            ["default"] = new ProfileLimits(CqMin: 19, CqMax: 26, MaxrateMin: 1.8, MaxrateMax: 3.4),
            ["low"] = new ProfileLimits(CqMin: 20, CqMax: 28, MaxrateMin: 1.5, MaxrateMax: 3.0)
        };

        var contentProfiles = new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new ContentProfileSettings(
                AlgoDefault: "bilinear",
                Defaults: animeDefaults,
                Limits: commonLimits),
            ["film"] = new ContentProfileSettings(
                AlgoDefault: "bicubic",
                Defaults: filmDefaults,
                Limits: commonLimits)
        };

        var bucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 40, MaxInclusive: 55),
                ["default"] = new ReductionRange(MinInclusive: 45, MaxInclusive: 60),
                ["low"] = new ReductionRange(MinInclusive: 50, MaxInclusive: 66)
            },
            ["film"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 38, MaxInclusive: 53),
                ["default"] = new ReductionRange(MinInclusive: 43, MaxInclusive: 58),
                ["low"] = new ReductionRange(MinInclusive: 48, MaxInclusive: 64)
            }
        };

        var sourceBuckets = new List<SourceBucketSettings>
        {
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                ContentQualityRanges: bucketRanges),
            new(
                Name: "hd_720",
                Match: new SourceBucketMatch(MinHeightInclusive: 650, MaxHeightInclusive: 999),
                ContentQualityRanges: bucketRanges),
            new(
                Name: "default_bucket",
                IsDefault: true,
                ContentQualityRanges: bucketRanges)
        };

        return new TranscodePolicyConfig(
            ContentProfiles: contentProfiles,
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            SourceBuckets: sourceBuckets,
            AutoSampling: new AutoSamplingSettings(
                EnabledByDefault: true,
                MaxIterations: 8,
                ModeDefault: "accurate",
                HybridAccurateIterations: 2));
    }
}
