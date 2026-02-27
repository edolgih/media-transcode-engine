using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;

namespace MediaTranscodeEngine.Core.Tests.Infrastructure;

public class StaticProfileRepositoryTests
{
    [Fact]
    public void Get576Config_WhenCalled_ReturnsExpectedContentProfilesAndDefaults()
    {
        var sut = new StaticProfileRepository();

        var actual = sut.Get576Config();

        actual.ContentProfiles.ContainsKey("anime").Should().BeTrue();
        actual.ContentProfiles.ContainsKey("mult").Should().BeTrue();
        actual.ContentProfiles.ContainsKey("film").Should().BeTrue();
        actual.ContentProfiles["film"].AlgoDefault.Should().Be("bilinear");
        actual.ContentProfiles["film"].Defaults["default"].Cq.Should().Be(26);
        actual.ContentProfiles["film"].Defaults["default"].Maxrate.Should().Be(3.4);
        actual.ContentProfiles["film"].Defaults["default"].Bufsize.Should().Be(6.9);
    }

    [Fact]
    public void Get576Config_WhenCalled_ReturnsExpectedBucketsWithoutDefaultBucket()
    {
        var sut = new StaticProfileRepository();

        var actual = sut.Get576Config();

        actual.SourceBuckets.Should().NotBeNull();
        actual.SourceBuckets!.Count.Should().Be(2);
        actual.SourceBuckets.Any(static b => b.IsDefault).Should().BeFalse();
        actual.SourceBuckets[0].Name.Should().Be("hd_720");
        actual.SourceBuckets[0].Match!.MinHeightInclusive.Should().Be(650);
        actual.SourceBuckets[0].Match!.MaxHeightInclusive.Should().Be(899);
        actual.SourceBuckets[1].Name.Should().Be("fhd_1080");
        actual.SourceBuckets[1].Match!.MinHeightInclusive.Should().Be(1000);
        actual.SourceBuckets[1].Match!.MaxHeightInclusive.Should().Be(1300);
    }
}
