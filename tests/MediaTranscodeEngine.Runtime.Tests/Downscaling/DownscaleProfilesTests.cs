using FluentAssertions;
using MediaTranscodeEngine.Runtime.Downscaling;

namespace MediaTranscodeEngine.Runtime.Tests.Downscaling;

public sealed class DownscaleProfilesTests
{
    [Fact]
    public void Default_When576ProfileIsRequested_ReturnsConfiguredSourceBuckets()
    {
        var sut = DownscaleProfiles.Default;

        var actual = sut.GetRequiredProfile(576);

        actual.SourceBuckets.Should().HaveCount(2);
        actual.SourceBuckets[0].Name.Should().Be("hd_720");
        actual.SourceBuckets[0].MinHeight.Should().Be(650);
        actual.SourceBuckets[0].MaxHeight.Should().Be(899);
        actual.SourceBuckets[1].Name.Should().Be("fhd_1080");
        actual.SourceBuckets[1].MinHeight.Should().Be(1000);
        actual.SourceBuckets[1].MaxHeight.Should().Be(1300);
    }

    [Fact]
    public void ResolveDefaults_WhenContentAndQualityAreMissing_UsesFilmDefaultEntry()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveDefaults(contentProfile: null, qualityProfile: null);

        actual.ContentProfile.Should().Be("film");
        actual.QualityProfile.Should().Be("default");
        actual.Cq.Should().Be(26);
        actual.Maxrate.Should().Be(3.4m);
        actual.Bufsize.Should().Be(6.9m);
        actual.Algorithm.Should().Be("bilinear");
    }

    [Fact]
    public void ResolveDefaults_WhenOnlyContentProfileIsProvided_UsesDefaultQualityForThatContent()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveDefaults(contentProfile: "anime", qualityProfile: null);

        actual.ContentProfile.Should().Be("anime");
        actual.QualityProfile.Should().Be("default");
        actual.Cq.Should().Be(23);
        actual.Maxrate.Should().Be(2.4m);
        actual.Bufsize.Should().Be(4.8m);
    }

    [Fact]
    public void ResolveDefaults_WhenOnlyQualityProfileIsProvided_UsesFilmAsDefaultContent()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveDefaults(contentProfile: null, qualityProfile: "high");

        actual.ContentProfile.Should().Be("film");
        actual.QualityProfile.Should().Be("high");
        actual.Cq.Should().Be(24);
        actual.Maxrate.Should().Be(3.7m);
        actual.Bufsize.Should().Be(7.4m);
    }

    [Fact]
    public void ResolveDefaults_WhenHd720BucketHasBoundsOverride_AppliesOnlyThoseBounds()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var defaultActual = sut.ResolveDefaults(sourceHeight: 720, contentProfile: "mult", qualityProfile: "default");
        var highActual = sut.ResolveDefaults(sourceHeight: 720, contentProfile: "mult", qualityProfile: "high");

        defaultActual.ContentProfile.Should().Be("mult");
        defaultActual.QualityProfile.Should().Be("default");
        defaultActual.Cq.Should().Be(26);
        defaultActual.Maxrate.Should().Be(2.4m);
        defaultActual.Bufsize.Should().Be(4.8m);
        defaultActual.CqMin.Should().Be(19);
        defaultActual.CqMax.Should().Be(29);
        defaultActual.MaxrateMin.Should().Be(2.0m);
        defaultActual.MaxrateMax.Should().Be(3.6m);

        highActual.ContentProfile.Should().Be("mult");
        highActual.QualityProfile.Should().Be("high");
        highActual.Cq.Should().Be(24);
        highActual.Maxrate.Should().Be(2.7m);
        highActual.Bufsize.Should().Be(5.3m);
        highActual.CqMin.Should().Be(15);
        highActual.CqMax.Should().Be(26);
        highActual.MaxrateMin.Should().Be(2.4m);
        highActual.MaxrateMax.Should().Be(4.4m);
    }

    [Fact]
    public void ResolveDefaults_WhenFhd1080BucketUsesBaseBounds_KeepsBaseOrSpecificOverride()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var defaultActual = sut.ResolveDefaults(sourceHeight: 1080, contentProfile: "mult", qualityProfile: "default");
        var lowActual = sut.ResolveDefaults(sourceHeight: 1080, contentProfile: "mult", qualityProfile: "low");

        defaultActual.CqMin.Should().Be(23);
        defaultActual.CqMax.Should().Be(29);
        defaultActual.MaxrateMin.Should().Be(2.0m);
        defaultActual.MaxrateMax.Should().Be(2.8m);

        lowActual.CqMin.Should().Be(26);
        lowActual.CqMax.Should().Be(33);
        lowActual.MaxrateMin.Should().Be(1.4m);
        lowActual.MaxrateMax.Should().Be(2.0m);
    }

    [Theory]
    [InlineData(650, "hd_720")]
    [InlineData(899, "hd_720")]
    [InlineData(900, null)]
    [InlineData(999, null)]
    [InlineData(1000, "fhd_1080")]
    [InlineData(1300, "fhd_1080")]
    [InlineData(1301, null)]
    public void ResolveSourceBucket_WhenHeightIsOnBoundary_MatchesConfiguredBuckets(int sourceHeight, string? expectedBucket)
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveSourceBucket(sourceHeight);

        actual.Should().Be(expectedBucket);
    }

    [Fact]
    public void ResolveSourceBucketIssue_WhenBucketIsMissing_ReturnsHint()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveSourceBucketIssue(900);

        actual.Should().Be("576 source bucket missing: height 900; add SourceBuckets");
    }

    [Fact]
    public void Default_When576ProfileIsRequested_ReturnsConfiguredRateModel()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        sut.RateModel.CqStepToMaxrateStep.Should().Be(0.4m);
        sut.RateModel.BufsizeMultiplier.Should().Be(2.0m);
    }

    [Fact]
    public void Default_When576ProfileIsRequested_ReturnsConfiguredAutoSampling()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        sut.AutoSampling.EnabledByDefault.Should().BeTrue();
        sut.AutoSampling.ModeDefault.Should().Be("accurate");
        sut.AutoSampling.MaxIterations.Should().Be(8);
        sut.AutoSampling.HybridAccurateIterations.Should().Be(2);
        sut.AutoSampling.AudioBitrateEstimateMbps.Should().Be(0.192m);
        sut.AutoSampling.SampleWindowDuration.Should().Be(TimeSpan.FromSeconds(15));
        sut.AutoSampling.LongWindowAnchors.Should().Equal(0.20, 0.50, 0.80);
        sut.AutoSampling.MediumWindowAnchors.Should().Equal(0.35, 0.65);
        sut.AutoSampling.ShortWindowAnchors.Should().Equal(0.50);
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsLong_ReturnsThree15SecondWindowsUsingConfiguredAnchors()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.FromMinutes(8));

        actual.Should().Equal(
            new DownscaleSampleWindow(StartSeconds: 88, DurationSeconds: 15),
            new DownscaleSampleWindow(StartSeconds: 232, DurationSeconds: 15),
            new DownscaleSampleWindow(StartSeconds: 376, DurationSeconds: 15));
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsMedium_ReturnsTwo15SecondWindowsUsingConfiguredAnchors()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.FromMinutes(3));

        actual.Should().Equal(
            new DownscaleSampleWindow(StartSeconds: 55, DurationSeconds: 15),
            new DownscaleSampleWindow(StartSeconds: 109, DurationSeconds: 15));
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsShort_ReturnsOne15SecondWindow()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.FromMinutes(2));

        actual.Should().Equal(new DownscaleSampleWindow(StartSeconds: 52, DurationSeconds: 15));
    }

    [Fact]
    public void GetSampleWindows_WhenCustomAnchorsAndSharedDurationAreConfigured_UsesThoseValues()
    {
        var sut = new DownscaleAutoSampling(
            EnabledByDefault: true,
            ModeDefault: "accurate",
            MaxIterations: 8,
            HybridAccurateIterations: 2,
            AudioBitrateEstimateMbps: 0.192m,
            LongMinDuration: TimeSpan.FromMinutes(8),
            LongWindowCount: 2,
            LongWindowAnchors: [0.20, 0.80],
            MediumMinDuration: TimeSpan.FromMinutes(3),
            MediumWindowCount: 2,
            MediumWindowAnchors: [0.25, 0.75],
            ShortWindowCount: 1,
            SampleWindowDuration: TimeSpan.FromSeconds(30),
            ShortWindowAnchors: [0.50]);

        var actual = sut.GetSampleWindows(TimeSpan.FromMinutes(10));

        actual.Should().Equal(
            new DownscaleSampleWindow(StartSeconds: 105, DurationSeconds: 30),
            new DownscaleSampleWindow(StartSeconds: 465, DurationSeconds: 30));
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsMissing_ReturnsEmptySet()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.Zero);

        actual.Should().BeEmpty();
    }

    [Fact]
    public void ResolveRange_WhenContentProfileAnimeAndQualityDefault_ReturnsConfiguredBucketRange()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveRange(sourceHeight: 1080, contentProfile: "anime", qualityProfile: "default");

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(45.0m);
        actual.MaxInclusive.Should().Be(60.0m);
    }

    [Fact]
    public void ResolveRange_WhenContentProfileMultAndQualityDefault_ReturnsConfiguredBucketRange()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveRange(sourceHeight: 1080, contentProfile: "mult", qualityProfile: "default");

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(42.0m);
        actual.MaxInclusive.Should().Be(57.0m);
    }

    [Fact]
    public void ResolveRange_WhenContentProfileFilmAndQualityDefault_ReturnsConfiguredBucketRange()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveRange(sourceHeight: 1080, contentProfile: "film", qualityProfile: "default");

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(35.0m);
        actual.MaxInclusive.Should().Be(50.0m);
    }

    [Fact]
    public void ResolveRange_WhenContentSpecificRangeIsMissing_FallsBackToGlobalQualityRange()
    {
        var sut = CreateProfileWithFallbacks(globalContentRanges: []);

        var actual = sut.ResolveRange(sourceHeight: null, contentProfile: "anime", qualityProfile: "default");

        actual.Should().NotBeNull();
        actual!.MinExclusive.Should().Be(40.0m);
        actual.MaxInclusive.Should().Be(50.0m);
    }

    [Fact]
    public void ResolveRange_WhenBucketDoesNotMatch_FallsBackToGlobalRange()
    {
        var sut = CreateProfileWithFallbacks();

        var actual = sut.ResolveRange(sourceHeight: 900, contentProfile: "anime", qualityProfile: "default");

        actual.Should().NotBeNull();
        actual!.MinExclusive.Should().Be(40.0m);
        actual.MaxInclusive.Should().Be(50.0m);
    }

    [Fact]
    public void ResolveRange_WhenSourceHeightIsMissing_UsesDefaultBucketRange()
    {
        var sut = CreateProfileWithFallbacks(
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "fhd",
                    MinHeight: 1000,
                    MaxHeight: 1300,
                    QualityRanges:
                    [
                        new DownscaleQualityRange("default", MinExclusive: 80.0m, MaxInclusive: 90.0m)
                    ]),
                new SourceHeightBucket(
                    "fallback",
                    MinHeight: 1,
                    MaxHeight: 1,
                    QualityRanges:
                    [
                        new DownscaleQualityRange("default", MinExclusive: 33.0m, MaxInclusive: 44.0m)
                    ],
                    IsDefault: true)
            ]);

        var actual = sut.ResolveRange(sourceHeight: null, contentProfile: "anime", qualityProfile: "default");

        actual.Should().NotBeNull();
        actual!.MinExclusive.Should().Be(33.0m);
        actual.MaxInclusive.Should().Be(44.0m);
    }

    [Fact]
    public void ResolveSourceBucket_WhenHeightDoesNotMatchConfiguredBuckets_UsesDefaultBucket()
    {
        var sut = CreateProfileWithFallbacks(
            sourceBuckets:
            [
                new SourceHeightBucket("fhd", MinHeight: 1000, MaxHeight: 1300, Ranges: [new DownscaleRange("anime", "default", MinInclusive: 80.0m, MaxInclusive: 90.0m)]),
                new SourceHeightBucket("fallback", MinHeight: 1, MaxHeight: 1, QualityRanges: [new DownscaleQualityRange("default", MinExclusive: 33.0m, MaxInclusive: 44.0m)], IsDefault: true)
            ]);

        var actual = sut.ResolveSourceBucket(900);

        actual.Should().Be("fallback");
    }

    [Fact]
    public void Contains_WhenValueEqualsLowerInclusive_ReturnsTrue()
    {
        var sut = new DownscaleRange("film", "default", MinInclusive: 35.0m, MaxInclusive: 50.0m);

        sut.Contains(35.0m).Should().BeTrue();
    }

    [Fact]
    public void Contains_WhenValueEqualsLowerExclusive_ReturnsFalse()
    {
        var sut = new DownscaleRange("film", "default", MinExclusive: 35.0m, MaxInclusive: 50.0m);

        sut.Contains(35.0m).Should().BeFalse();
    }

    [Fact]
    public void Contains_WhenValueEqualsUpperInclusive_ReturnsTrue()
    {
        var sut = new DownscaleRange("film", "default", MinInclusive: 35.0m, MaxInclusive: 50.0m);

        sut.Contains(50.0m).Should().BeTrue();
    }

    [Fact]
    public void Contains_WhenValueEqualsUpperExclusive_ReturnsFalse()
    {
        var sut = new DownscaleRange("film", "default", MinInclusive: 35.0m, MaxExclusive: 50.0m);

        sut.Contains(50.0m).Should().BeFalse();
    }

    private static DownscaleProfile CreateProfileWithFallbacks(
        IReadOnlyList<SourceHeightBucket>? sourceBuckets = null,
        IReadOnlyList<DownscaleRange>? globalContentRanges = null)
    {
        return new DownscaleProfile(
            targetHeight: 576,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new DownscaleRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
            autoSampling: new DownscaleAutoSampling(
                EnabledByDefault: true,
                ModeDefault: "accurate",
                MaxIterations: 8,
                HybridAccurateIterations: 2,
                AudioBitrateEstimateMbps: 0.192m,
                LongMinDuration: TimeSpan.FromMinutes(8),
                LongWindowCount: 3,
                LongWindowAnchors: [0.20, 0.50, 0.80],
                MediumMinDuration: TimeSpan.FromMinutes(3),
                MediumWindowCount: 2,
                MediumWindowAnchors: [0.35, 0.65],
                ShortWindowCount: 1,
                SampleWindowDuration: TimeSpan.FromSeconds(15),
                ShortWindowAnchors: [0.50]),
            sourceBuckets: sourceBuckets ??
                           [
                               new SourceHeightBucket(
                                   "fhd",
                                   MinHeight: 1000,
                                   MaxHeight: 1300,
                                   Ranges:
                                   [
                                       new DownscaleRange("anime", "default", MinInclusive: 80.0m, MaxInclusive: 90.0m)
                                   ])
                           ],
            defaults:
            [
                new DownscaleDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: "bilinear", CqMin: 20, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 3.0m),
                new DownscaleDefaults("film", "default", Cq: 26, Maxrate: 3.4m, Bufsize: 6.9m, Algorithm: "bilinear", CqMin: 18, CqMax: 35, MaxrateMin: 1.6m, MaxrateMax: 8.0m)
            ],
            globalContentRanges: globalContentRanges ??
                                 [
                                     new DownscaleRange("anime", "default", MinExclusive: 40.0m, MaxInclusive: 50.0m)
                                 ],
            globalQualityRanges:
            [
                new DownscaleQualityRange("default", MinExclusive: 40.0m, MaxInclusive: 50.0m)
            ]);
    }
}
