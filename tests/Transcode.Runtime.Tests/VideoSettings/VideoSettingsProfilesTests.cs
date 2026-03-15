using FluentAssertions;
using Transcode.Core.VideoSettings;
using Transcode.Core.VideoSettings.Profiles;

namespace Transcode.Runtime.Tests.VideoSettings;

/*
Это тесты runtime-каталога video settings профилей.
Они проверяют bucket mapping и доступность типизированных profile data.
*/
/// <summary>
/// Verifies profile catalog mapping and typed profile data exposed by Runtime.
/// </summary>
public sealed class VideoSettingsProfilesTests
{
    [Theory]
    [InlineData(400, 424)]
    [InlineData(500, 480)]
    [InlineData(577, 576)]
    [InlineData(650, 720)]
    [InlineData(900, 1080)]
    public void ResolveOutputProfile_WhenHeightIsMapped_ReturnsExpectedProfile(int outputHeight, int expectedTargetHeight)
    {
        var sut = VideoSettingsProfiles.Default;

        var actual = sut.ResolveOutputProfile(outputHeight);

        actual.TargetHeight.Should().Be(expectedTargetHeight);
    }

    [Fact]
    public void GetSupportedDownscaleTargetHeights_WhenDefaultsAreRequested_ReturnsConfiguredTargets()
    {
        var sut = VideoSettingsProfiles.Default;

        var actual = sut.GetSupportedDownscaleTargetHeights();

        actual.Should().Equal(720, 576, 480, 424);
    }

    [Fact]
    public void Default_When720ProfileIsRequested_ResolvesFilmDefault()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(720);

        var actual = sut.ResolveDefaults(CreateSelection(sut));

        actual.ContentProfile.Should().Be("film");
        actual.QualityProfile.Should().Be("default");
        actual.Cq.Should().Be(23);
        actual.Maxrate.Should().Be(4.5m);
        actual.Bufsize.Should().Be(9.0m);
    }

    [Fact]
    public void Default_When1080ProfileIsRequested_ResolvesFilmDefault()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(1080);

        var actual = sut.ResolveDefaults(CreateSelection(sut));

        actual.ContentProfile.Should().Be("film");
        actual.QualityProfile.Should().Be("default");
        actual.Cq.Should().Be(21);
        actual.Maxrate.Should().Be(5.2m);
        actual.Bufsize.Should().Be(10.4m);
    }

    [Fact]
    public void ResolveDefaults_When720ProfileUsesAnimeHigh_ReturnsConfiguredMaxrate()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(720);

        var actual = sut.ResolveDefaults(CreateSelection(sut, contentProfile: "anime", qualityProfile: "high"));

        actual.Maxrate.Should().Be(3.6m);
        actual.Bufsize.Should().Be(7.2m);
    }

    [Fact]
    public void Default_When720ProfileIsRequested_ReturnsConfiguredSourceBuckets()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(720);

        sut.SourceBuckets.Should().HaveCount(2);
        sut.SourceBuckets.Select(static bucket => bucket.Name).Should().Equal("fhd_1080", "uhd_2160");
    }

    [Fact]
    public void Default_When1080ProfileIsRequested_ReturnsConfiguredSourceBuckets()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(1080);

        sut.SourceBuckets.Should().HaveCount(2);
        sut.SourceBuckets.Select(static bucket => bucket.Name).Should().Equal("qhd_1440", "uhd_2160");
    }

    [Fact]
    public void ResolveRange_When720ProfileUsesFhd1080Bucket_ReturnsConfiguredBucketRange()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(720);

        var actual = sut.ResolveRange(sourceHeight: 1080, CreateSelection(sut, contentProfile: "film", qualityProfile: "default"));

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(30.0m);
        actual.MaxInclusive.Should().Be(45.0m);
    }

    [Fact]
    public void ResolveDefaults_When720ProfileUsesFhd1080BucketBoundsOverride_AppliesOnlyOverride()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(720);

        var actual = sut.ResolveDefaults(sourceHeight: 1080, CreateSelection(sut, contentProfile: "mult", qualityProfile: "default"));

        actual.Cq.Should().Be(25);
        actual.CqMin.Should().Be(21);
        actual.CqMax.Should().Be(29);
        actual.MaxrateMin.Should().Be(2.0m);
        actual.MaxrateMax.Should().Be(3.6m);
    }

    [Fact]
    public void ResolveRange_When1080ProfileUsesUhd2160Bucket_ReturnsConfiguredBucketRange()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(1080);

        var actual = sut.ResolveRange(sourceHeight: 2160, CreateSelection(sut, contentProfile: "film", qualityProfile: "default"));

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(33.0m);
        actual.MaxInclusive.Should().Be(48.0m);
    }

    [Fact]
    public void Default_When576ProfileIsRequested_ReturnsConfiguredSourceBuckets()
    {
        var sut = VideoSettingsProfiles.Default;

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
    public void Default_When480ProfileIsRequested_ReturnsConfiguredSourceBuckets()
    {
        var sut = VideoSettingsProfiles.Default;

        var actual = sut.GetRequiredProfile(480);

        actual.SourceBuckets.Should().HaveCount(3);
        actual.SourceBuckets[0].Name.Should().Be("sd_576");
        actual.SourceBuckets[0].MinHeight.Should().Be(481);
        actual.SourceBuckets[0].MaxHeight.Should().Be(649);
        actual.SourceBuckets[1].Name.Should().Be("hd_720");
        actual.SourceBuckets[1].MinHeight.Should().Be(650);
        actual.SourceBuckets[1].MaxHeight.Should().Be(899);
        actual.SourceBuckets[2].Name.Should().Be("fhd_1080");
        actual.SourceBuckets[2].MinHeight.Should().Be(900);
        actual.SourceBuckets[2].MaxHeight.Should().Be(1300);
    }

    [Fact]
    public void Default_When424ProfileIsRequested_ReturnsConfiguredSourceBuckets()
    {
        var sut = VideoSettingsProfiles.Default;

        var actual = sut.GetRequiredProfile(424);

        actual.SourceBuckets.Should().HaveCount(3);
        actual.SourceBuckets[0].Name.Should().Be("sd_480");
        actual.SourceBuckets[0].MinHeight.Should().Be(425);
        actual.SourceBuckets[0].MaxHeight.Should().Be(649);
        actual.SourceBuckets[1].Name.Should().Be("hd_720");
        actual.SourceBuckets[1].MinHeight.Should().Be(650);
        actual.SourceBuckets[1].MaxHeight.Should().Be(899);
        actual.SourceBuckets[2].Name.Should().Be("fhd_1080");
        actual.SourceBuckets[2].MinHeight.Should().Be(900);
        actual.SourceBuckets[2].MaxHeight.Should().Be(1300);
    }

    [Fact]
    public void ResolveDefaults_WhenContentAndQualityAreMissing_UsesFilmDefaultEntry()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveDefaults(CreateSelection(sut));

        actual.ContentProfile.Should().Be("film");
        actual.QualityProfile.Should().Be("default");
        actual.Cq.Should().Be(26);
        actual.Maxrate.Should().Be(3.4m);
        actual.Bufsize.Should().Be(6.9m);
        actual.Algorithm.Should().Be("bilinear");
    }

    [Fact]
    public void ResolveDefaults_When424ProfileUsesMissingContentAndQuality_UsesFilmDefaultEntry()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(424);

        var actual = sut.ResolveDefaults(CreateSelection(sut));

        actual.ContentProfile.Should().Be("film");
        actual.QualityProfile.Should().Be("default");
        actual.Cq.Should().Be(28);
        actual.Maxrate.Should().Be(2.1m);
        actual.Bufsize.Should().Be(4.2m);
        actual.Algorithm.Should().Be("bilinear");
    }

    [Fact]
    public void ResolveDefaults_WhenOnlyContentProfileIsProvided_UsesDefaultQualityForThatContent()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveDefaults(CreateSelection(sut, contentProfile: "anime"));

        actual.ContentProfile.Should().Be("anime");
        actual.QualityProfile.Should().Be("default");
        actual.Cq.Should().Be(23);
        actual.Maxrate.Should().Be(2.4m);
        actual.Bufsize.Should().Be(4.8m);
    }

    [Fact]
    public void ResolveDefaults_WhenOnlyQualityProfileIsProvided_UsesFilmAsDefaultContent()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveDefaults(CreateSelection(sut, qualityProfile: "high"));

        actual.ContentProfile.Should().Be("film");
        actual.QualityProfile.Should().Be("high");
        actual.Cq.Should().Be(24);
        actual.Maxrate.Should().Be(3.7m);
        actual.Bufsize.Should().Be(7.4m);
    }

    [Fact]
    public void ResolveDefaults_WhenHd720BucketHasBoundsOverride_AppliesOnlyThoseBounds()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var defaultActual = sut.ResolveDefaults(sourceHeight: 720, CreateSelection(sut, contentProfile: "mult", qualityProfile: "default"));
        var highActual = sut.ResolveDefaults(sourceHeight: 720, CreateSelection(sut, contentProfile: "mult", qualityProfile: "high"));

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
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var defaultActual = sut.ResolveDefaults(sourceHeight: 1080, CreateSelection(sut, contentProfile: "mult", qualityProfile: "default"));
        var lowActual = sut.ResolveDefaults(sourceHeight: 1080, CreateSelection(sut, contentProfile: "mult", qualityProfile: "low"));

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
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveSourceBucket(sourceHeight);

        actual.Should().Be(expectedBucket);
    }

    [Fact]
    public void ResolveSourceBucketIssue_WhenBucketIsMissing_ReturnsHint()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveSourceBucketIssue(900);

        actual.Should().Be("576 source bucket missing: height 900; add SourceBuckets");
    }

    [Fact]
    public void Default_When576ProfileIsRequested_ReturnsConfiguredRateModel()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        sut.RateModel.CqStepToMaxrateStep.Should().Be(0.4m);
        sut.RateModel.BufsizeMultiplier.Should().Be(2.0m);
    }

    [Fact]
    public void Default_When576ProfileIsRequested_ReturnsConfiguredAutoSampling()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        sut.AutoSampling.ModeDefault.Should().Be("accurate");
        sut.AutoSampling.MaxIterations.Should().Be(8);
        sut.AutoSampling.SampleWindowDuration.Should().Be(TimeSpan.FromSeconds(30));
        sut.AutoSampling.LongWindowAnchors.Should().Equal(0.20, 0.50, 0.80);
        sut.AutoSampling.MediumWindowAnchors.Should().Equal(0.35, 0.65);
        sut.AutoSampling.ShortWindowAnchors.Should().Equal(0.50);
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsLong_ReturnsThree30SecondWindowsUsingConfiguredAnchors()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.FromMinutes(8));

        actual.Should().Equal(
            new VideoSettingsSampleWindow(StartSeconds: 81, DurationSeconds: 30),
            new VideoSettingsSampleWindow(StartSeconds: 225, DurationSeconds: 30),
            new VideoSettingsSampleWindow(StartSeconds: 369, DurationSeconds: 30));
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsMedium_ReturnsTwo30SecondWindowsUsingConfiguredAnchors()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.FromMinutes(3));

        actual.Should().Equal(
            new VideoSettingsSampleWindow(StartSeconds: 48, DurationSeconds: 30),
            new VideoSettingsSampleWindow(StartSeconds: 102, DurationSeconds: 30));
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsShort_ReturnsOne30SecondWindow()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.FromMinutes(2));

        actual.Should().Equal(new VideoSettingsSampleWindow(StartSeconds: 45, DurationSeconds: 30));
    }

    [Fact]
    public void GetSampleWindows_WhenCustomAnchorsAndSharedDurationAreConfigured_UsesThoseValues()
    {
        var sut = new VideoSettingsAutoSampling(
            ModeDefault: "accurate",
            MaxIterations: 8,
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
            new VideoSettingsSampleWindow(StartSeconds: 105, DurationSeconds: 30),
            new VideoSettingsSampleWindow(StartSeconds: 465, DurationSeconds: 30));
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsMissing_ReturnsEmptySet()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.Zero);

        actual.Should().BeEmpty();
    }

    [Fact]
    public void ResolveRange_WhenContentProfileAnimeAndQualityDefault_ReturnsConfiguredBucketRange()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveRange(sourceHeight: 1080, CreateSelection(sut, contentProfile: "anime", qualityProfile: "default"));

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(45.0m);
        actual.MaxInclusive.Should().Be(60.0m);
    }

    [Fact]
    public void ResolveRange_WhenContentProfileMultAndQualityDefault_ReturnsConfiguredBucketRange()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveRange(sourceHeight: 1080, CreateSelection(sut, contentProfile: "mult", qualityProfile: "default"));

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(42.0m);
        actual.MaxInclusive.Should().Be(57.0m);
    }

    [Fact]
    public void ResolveRange_WhenContentProfileFilmAndQualityDefault_ReturnsConfiguredBucketRange()
    {
        var sut = VideoSettingsProfiles.Default.GetRequiredProfile(576);

        var actual = sut.ResolveRange(sourceHeight: 1080, CreateSelection(sut, contentProfile: "film", qualityProfile: "default"));

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(35.0m);
        actual.MaxInclusive.Should().Be(50.0m);
    }

    [Fact]
    public void ResolveRange_WhenContentSpecificRangeIsMissing_FallsBackToGlobalQualityRange()
    {
        var sut = CreateProfileWithFallbacks(globalContentRanges: []);

        var actual = sut.ResolveRange(sourceHeight: null, CreateSelection(sut, contentProfile: "anime", qualityProfile: "default"));

        actual.Should().NotBeNull();
        actual!.MinExclusive.Should().Be(40.0m);
        actual.MaxInclusive.Should().Be(50.0m);
    }

    [Fact]
    public void ResolveRange_WhenBucketDoesNotMatch_FallsBackToGlobalRange()
    {
        var sut = CreateProfileWithFallbacks();

        var actual = sut.ResolveRange(sourceHeight: 900, CreateSelection(sut, contentProfile: "anime", qualityProfile: "default"));

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
                        new VideoSettingsQualityRange("default", MinExclusive: 80.0m, MaxInclusive: 90.0m)
                    ]),
                new SourceHeightBucket(
                    "fallback",
                    MinHeight: 1,
                    MaxHeight: 1,
                    QualityRanges:
                    [
                        new VideoSettingsQualityRange("default", MinExclusive: 33.0m, MaxInclusive: 44.0m)
                    ],
                    IsDefault: true)
            ]);

        var actual = sut.ResolveRange(sourceHeight: null, CreateSelection(sut, contentProfile: "anime", qualityProfile: "default"));

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
                new SourceHeightBucket("fhd", MinHeight: 1000, MaxHeight: 1300, Ranges: [new VideoSettingsRange("anime", "default", MinInclusive: 80.0m, MaxInclusive: 90.0m)]),
                new SourceHeightBucket("fallback", MinHeight: 1, MaxHeight: 1, QualityRanges: [new VideoSettingsQualityRange("default", MinExclusive: 33.0m, MaxInclusive: 44.0m)], IsDefault: true)
            ]);

        var actual = sut.ResolveSourceBucket(900);

        actual.Should().Be("fallback");
    }

    [Fact]
    public void Contains_WhenValueEqualsLowerInclusive_ReturnsTrue()
    {
        var sut = new VideoSettingsRange("film", "default", MinInclusive: 35.0m, MaxInclusive: 50.0m);

        sut.Contains(35.0m).Should().BeTrue();
    }

    [Fact]
    public void Contains_WhenValueEqualsLowerExclusive_ReturnsFalse()
    {
        var sut = new VideoSettingsRange("film", "default", MinExclusive: 35.0m, MaxInclusive: 50.0m);

        sut.Contains(35.0m).Should().BeFalse();
    }

    [Fact]
    public void Contains_WhenValueEqualsUpperInclusive_ReturnsTrue()
    {
        var sut = new VideoSettingsRange("film", "default", MinInclusive: 35.0m, MaxInclusive: 50.0m);

        sut.Contains(50.0m).Should().BeTrue();
    }

    [Fact]
    public void Contains_WhenValueEqualsUpperExclusive_ReturnsFalse()
    {
        var sut = new VideoSettingsRange("film", "default", MinInclusive: 35.0m, MaxExclusive: 50.0m);

        sut.Contains(50.0m).Should().BeFalse();
    }

    private static VideoSettingsProfile CreateProfileWithFallbacks(
        IReadOnlyList<SourceHeightBucket>? sourceBuckets = null,
        IReadOnlyList<VideoSettingsRange>? globalContentRanges = null)
    {
        return new VideoSettingsProfile(
            targetHeight: 576,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new VideoSettingsRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
            autoSampling: new VideoSettingsAutoSampling(
                ModeDefault: "accurate",
                MaxIterations: 8,
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
                                       new VideoSettingsRange("anime", "default", MinInclusive: 80.0m, MaxInclusive: 90.0m)
                                   ])
                           ],
            defaults:
            [
                new VideoSettingsDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: "bilinear", CqMin: 20, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 3.0m),
                new VideoSettingsDefaults("film", "default", Cq: 26, Maxrate: 3.4m, Bufsize: 6.9m, Algorithm: "bilinear", CqMin: 18, CqMax: 35, MaxrateMin: 1.6m, MaxrateMax: 8.0m)
            ],
            globalContentRanges: globalContentRanges ??
                                 [
                                     new VideoSettingsRange("anime", "default", MinExclusive: 40.0m, MaxInclusive: 50.0m)
                                 ],
            globalQualityRanges:
             [
                 new VideoSettingsQualityRange("default", MinExclusive: 40.0m, MaxInclusive: 50.0m)
             ]);
    }

    private static EffectiveVideoSettingsSelection CreateSelection(
        VideoSettingsProfile profile,
        string? contentProfile = null,
        string? qualityProfile = null,
        string autoSampleMode = "accurate")
    {
        return new EffectiveVideoSettingsSelection(
            ContentProfile: contentProfile ?? profile.DefaultContentProfile,
            QualityProfile: qualityProfile ?? profile.DefaultQualityProfile,
            AutoSampleMode: autoSampleMode);
    }
}
