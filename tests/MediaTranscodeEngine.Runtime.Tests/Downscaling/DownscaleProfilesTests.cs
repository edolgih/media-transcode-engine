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
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsLong_ReturnsThree120SecondWindows()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.FromMinutes(8));

        actual.Should().Equal(
            new DownscaleSampleWindow(StartSeconds: 60, DurationSeconds: 120),
            new DownscaleSampleWindow(StartSeconds: 180, DurationSeconds: 120),
            new DownscaleSampleWindow(StartSeconds: 300, DurationSeconds: 120));
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsMedium_ReturnsTwo120SecondWindows()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.FromMinutes(3));

        actual.Should().Equal(
            new DownscaleSampleWindow(StartSeconds: 45, DurationSeconds: 120),
            new DownscaleSampleWindow(StartSeconds: 30, DurationSeconds: 120));
    }

    [Fact]
    public void GetSampleWindows_WhenDurationIsShort_ReturnsOne90SecondWindow()
    {
        var sut = DownscaleProfiles.Default.GetRequiredProfile(576);

        var actual = sut.GetSampleWindows(TimeSpan.FromMinutes(2));

        actual.Should().Equal(new DownscaleSampleWindow(StartSeconds: 15, DurationSeconds: 90));
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
}
