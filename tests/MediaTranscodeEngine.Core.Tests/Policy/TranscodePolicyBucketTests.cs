using FluentAssertions;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Policy;

public class TranscodePolicyBucketTests
{
    [Fact]
    public void ResolveSourceBucket_WhenHeightMatchesConfiguredBucket_ReturnsMatchedBucket()
    {
        var sut = CreateSut();
        var config = CreateConfig();

        var actual = sut.ResolveSourceBucket(config, sourceHeight: 1080);

        actual.Should().NotBeNull();
        actual!.Name.Should().Be("fhd_1080");
    }

    [Fact]
    public void ResolveSourceBucket_WhenHeightDoesNotMatchConfiguredBuckets_ReturnsDefaultBucket()
    {
        var sut = CreateSut();
        var config = CreateConfig();

        var actual = sut.ResolveSourceBucket(config, sourceHeight: 900);

        actual.Should().NotBeNull();
        actual!.Name.Should().Be("default_bucket");
    }

    [Fact]
    public void ResolveSourceBucket_WhenNoDefaultAndNoMatches_ReturnsNull()
    {
        var sut = CreateSut();
        var config = CreateConfigWithoutDefaultBucket();

        var actual = sut.ResolveSourceBucket(config, sourceHeight: 901);

        actual.Should().BeNull();
    }

    [Fact]
    public void ResolveQualityRange_WhenBucketHasContentSpecificRange_ReturnsBucketRange()
    {
        var sut = CreateSut();
        var config = CreateConfig();

        var actual = sut.ResolveQualityRange(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            sourceHeight: 1080);

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(45.0);
        actual.MaxInclusive.Should().Be(60.0);
    }

    [Fact]
    public void ResolveQualityRange_WhenNoBucketMatch_FallsBackToGlobalContentRange()
    {
        var sut = CreateSut();
        var config = CreateConfigWithoutDefaultBucket();

        var actual = sut.ResolveQualityRange(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            sourceHeight: 901);

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(40.0);
        actual.MaxInclusive.Should().Be(50.0);
    }

    [Fact]
    public void ResolveQualityRange_WhenContentRangeMissing_FallsBackToGlobalQualityRange()
    {
        var sut = CreateSut();
        var config = CreateConfigWithoutGlobalContentRanges();

        var actual = sut.ResolveQualityRange(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            sourceHeight: 901);

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(35.0);
        actual.MaxInclusive.Should().Be(52.0);
    }

    private static TranscodePolicy CreateSut()
    {
        return new TranscodePolicy();
    }

    private static TranscodePolicyConfig CreateConfig()
    {
        return new TranscodePolicyConfig(
            ContentProfiles: CreateContentProfiles(),
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            QualityRanges: CreateGlobalQualityRanges(),
            ContentQualityRanges: CreateGlobalContentQualityRanges(),
            SourceBuckets: CreateSourceBuckets(includeDefault: true));
    }

    private static TranscodePolicyConfig CreateConfigWithoutDefaultBucket()
    {
        return new TranscodePolicyConfig(
            ContentProfiles: CreateContentProfiles(),
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            QualityRanges: CreateGlobalQualityRanges(),
            ContentQualityRanges: CreateGlobalContentQualityRanges(),
            SourceBuckets: CreateSourceBuckets(includeDefault: false));
    }

    private static TranscodePolicyConfig CreateConfigWithoutGlobalContentRanges()
    {
        return new TranscodePolicyConfig(
            ContentProfiles: CreateContentProfiles(),
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            QualityRanges: CreateGlobalQualityRanges(),
            ContentQualityRanges: new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase),
            SourceBuckets: CreateSourceBuckets(includeDefault: false));
    }

    private static IReadOnlyDictionary<string, ContentProfileSettings> CreateContentProfiles()
    {
        var defaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8)
        };

        var limits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileLimits(CqMin: 20, CqMax: 26, MaxrateMin: 2.0, MaxrateMax: 3.0)
        };

        return new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new ContentProfileSettings(
                AlgoDefault: "bilinear",
                Defaults: defaults,
                Limits: limits)
        };
    }

    private static IReadOnlyDictionary<string, ReductionRange> CreateGlobalQualityRanges()
    {
        return new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ReductionRange(MinInclusive: 35.0, MaxInclusive: 52.0)
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReductionRange>> CreateGlobalContentQualityRanges()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 40.0, MaxInclusive: 50.0)
            }
        };
    }

    private static IReadOnlyList<SourceBucketSettings> CreateSourceBuckets(bool includeDefault)
    {
        var buckets = new List<SourceBucketSettings>
        {
            new(
                Name: "hd_720",
                Match: new SourceBucketMatch(MinHeightInclusive: 650, MaxHeightInclusive: 899),
                ContentQualityRanges: new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["default"] = new ReductionRange(MinInclusive: 32.0, MaxInclusive: 46.0)
                    }
                }),
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                ContentQualityRanges: new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["default"] = new ReductionRange(MinInclusive: 45.0, MaxInclusive: 60.0)
                    }
                })
        };

        if (includeDefault)
        {
            buckets.Add(new SourceBucketSettings(
                Name: "default_bucket",
                IsDefault: true));
        }

        return buckets;
    }
}
