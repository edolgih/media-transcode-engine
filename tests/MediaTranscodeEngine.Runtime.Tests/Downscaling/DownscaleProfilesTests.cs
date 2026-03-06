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
}
