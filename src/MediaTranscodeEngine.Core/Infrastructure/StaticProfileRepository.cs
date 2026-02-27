using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Infrastructure;

public sealed class StaticProfileRepository : IProfileRepository
{
    private readonly TranscodePolicyConfig _config;

    public StaticProfileRepository()
        : this(CreateDefaultConfig())
    {
    }

    public StaticProfileRepository(TranscodePolicyConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public TranscodePolicyConfig Get576Config() => _config;

    private static TranscodePolicyConfig CreateDefaultConfig()
    {
        var animeDefaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileDefaults(Cq: 22, Maxrate: 3.3, Bufsize: 6.5),
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8),
            ["low"] = new ProfileDefaults(Cq: 29, Maxrate: 2.1, Bufsize: 4.1)
        };

        var animeLimits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileLimits(CqMin: 19, CqMax: 24, MaxrateMin: 2.4, MaxrateMax: 4.2),
            ["default"] = new ProfileLimits(CqMin: 20, CqMax: 26, MaxrateMin: 2.0, MaxrateMax: 3.0),
            ["low"] = new ProfileLimits(CqMin: 24, CqMax: 35, MaxrateMin: 1.0, MaxrateMax: 3.2)
        };

        var multDefaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileDefaults(Cq: 24, Maxrate: 2.7, Bufsize: 5.3),
            ["default"] = new ProfileDefaults(Cq: 26, Maxrate: 2.4, Bufsize: 4.8),
            ["low"] = new ProfileDefaults(Cq: 29, Maxrate: 1.7, Bufsize: 3.5)
        };

        var multLimits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileLimits(CqMin: 21, CqMax: 26, MaxrateMin: 2.4, MaxrateMax: 3.2),
            ["default"] = new ProfileLimits(CqMin: 23, CqMax: 29, MaxrateMin: 2.0, MaxrateMax: 2.8),
            ["low"] = new ProfileLimits(CqMin: 26, CqMax: 31, MaxrateMin: 1.6, MaxrateMax: 2.0)
        };

        var filmDefaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileDefaults(Cq: 24, Maxrate: 3.7, Bufsize: 7.4),
            ["default"] = new ProfileDefaults(Cq: 26, Maxrate: 3.4, Bufsize: 6.9),
            ["low"] = new ProfileDefaults(Cq: 30, Maxrate: 2.2, Bufsize: 4.5)
        };

        var filmLimits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileLimits(CqMin: 16, CqMax: 33, MaxrateMin: 2.0, MaxrateMax: 8.0),
            ["default"] = new ProfileLimits(CqMin: 18, CqMax: 35, MaxrateMin: 1.6, MaxrateMax: 8.0),
            ["low"] = new ProfileLimits(CqMin: 20, CqMax: 38, MaxrateMin: 1.2, MaxrateMax: 4.0)
        };

        var contentProfiles = new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new ContentProfileSettings(
                AlgoDefault: "bilinear",
                Defaults: animeDefaults,
                Limits: animeLimits),
            ["mult"] = new ContentProfileSettings(
                AlgoDefault: "bilinear",
                Defaults: multDefaults,
                Limits: multLimits),
            ["film"] = new ContentProfileSettings(
                AlgoDefault: "bilinear",
                Defaults: filmDefaults,
                Limits: filmLimits)
        };

        var qualityRanges = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ReductionRange(MinInclusive: 25.0, MaxInclusive: 40.0),
            ["default"] = new ReductionRange(MinExclusive: 40.0, MaxInclusive: 50.0),
            ["low"] = new ReductionRange(MinExclusive: 50.0)
        };

        var contentQualityRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 25.0, MaxInclusive: 40.0),
                ["default"] = new ReductionRange(MinExclusive: 40.0, MaxInclusive: 50.0),
                ["low"] = new ReductionRange(MinExclusive: 50.0, MaxInclusive: 80.0)
            },
            ["mult"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 30.0, MaxInclusive: 45.0),
                ["default"] = new ReductionRange(MinExclusive: 45.0, MaxInclusive: 58.0),
                ["low"] = new ReductionRange(MinExclusive: 58.0, MaxInclusive: 85.0)
            },
            ["film"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 20.0, MaxInclusive: 38.0),
                ["default"] = new ReductionRange(MinExclusive: 38.0, MaxInclusive: 52.0),
                ["low"] = new ReductionRange(MinExclusive: 52.0, MaxInclusive: 78.0)
            }
        };

        var hd720BucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 18.0, MaxInclusive: 32.0),
                ["default"] = new ReductionRange(MinInclusive: 32.0, MaxInclusive: 46.0),
                ["low"] = new ReductionRange(MinInclusive: 46.0, MaxInclusive: 65.0)
            },
            ["mult"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 15.0, MaxInclusive: 28.0),
                ["default"] = new ReductionRange(MinInclusive: 28.0, MaxInclusive: 42.0),
                ["low"] = new ReductionRange(MinInclusive: 42.0, MaxInclusive: 60.0)
            },
            ["film"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 10.0, MaxInclusive: 25.0),
                ["default"] = new ReductionRange(MinInclusive: 25.0, MaxInclusive: 40.0),
                ["low"] = new ReductionRange(MinInclusive: 40.0, MaxInclusive: 55.0)
            }
        };

        var fhd1080BucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 30.0, MaxInclusive: 45.0),
                ["default"] = new ReductionRange(MinInclusive: 45.0, MaxInclusive: 60.0),
                ["low"] = new ReductionRange(MinInclusive: 60.0, MaxInclusive: 80.0)
            },
            ["mult"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 28.0, MaxInclusive: 42.0),
                ["default"] = new ReductionRange(MinInclusive: 42.0, MaxInclusive: 57.0),
                ["low"] = new ReductionRange(MinInclusive: 57.0, MaxInclusive: 77.0)
            },
            ["film"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ReductionRange(MinInclusive: 20.0, MaxInclusive: 35.0),
                ["default"] = new ReductionRange(MinInclusive: 35.0, MaxInclusive: 50.0),
                ["low"] = new ReductionRange(MinInclusive: 50.0, MaxInclusive: 70.0)
            }
        };

        var sourceBuckets = new List<SourceBucketSettings>
        {
            new(
                Name: "hd_720",
                Match: new SourceBucketMatch(MinHeightInclusive: 650, MaxHeightInclusive: 899),
                ContentQualityRanges: hd720BucketRanges),
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                ContentQualityRanges: fhd1080BucketRanges)
        };

        return new TranscodePolicyConfig(
            ContentProfiles: contentProfiles,
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            QualityRanges: qualityRanges,
            ContentQualityRanges: contentQualityRanges,
            SourceBuckets: sourceBuckets,
            AutoSampling: new AutoSamplingSettings(
                EnabledByDefault: true,
                MaxIterations: 8,
                ModeDefault: "accurate",
                HybridAccurateIterations: 2));
    }
}
