using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;

namespace MediaTranscodeEngine.Core.Tests.Infrastructure;

public class StaticProfileRepositoryTests
{
    [Fact]
    public void Get576Config_WhenCalled_ReturnsConfigWithExpectedProfiles()
    {
        var sut = new StaticProfileRepository();

        var actual = sut.Get576Config();

        actual.ContentProfiles.ContainsKey("anime").Should().BeTrue();
        actual.ContentProfiles.ContainsKey("film").Should().BeTrue();
        actual.ContentProfiles["anime"].Defaults.ContainsKey("high").Should().BeTrue();
        actual.ContentProfiles["film"].Defaults.ContainsKey("default").Should().BeTrue();
    }

    [Fact]
    public void Get576Config_WhenCalled_ReturnsSourceBucketsWithDefaultBucket()
    {
        var sut = new StaticProfileRepository();

        var actual = sut.Get576Config();

        actual.SourceBuckets.Should().NotBeNull();
        actual.SourceBuckets!.Count.Should().BeGreaterThan(0);
        actual.SourceBuckets.Any(static b => b.IsDefault).Should().BeTrue();
    }
}
