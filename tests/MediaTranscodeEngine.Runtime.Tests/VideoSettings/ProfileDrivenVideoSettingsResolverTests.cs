using FluentAssertions;
using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.Tests.VideoSettings;

public sealed class ProfileDrivenVideoSettingsResolverTests
{
    [Fact]
    public void ResolveForEncode_WhenRequestIsNull_UsesResolvedOutputProfileAndFastMode()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var actual = sut.ResolveForEncode(
            request: null,
            outputHeight: 650,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: 4_000_000,
            hasAudio: true);

        // Assert
        actual.Profile.TargetHeight.Should().Be(720);
        actual.EffectiveRequest.TargetHeight.Should().Be(720);
        actual.EffectiveRequest.AutoSampleMode.Should().Be("fast");
    }

    [Fact]
    public void ResolveForEncode_WhenRequestContainsTargetHeight_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateRequest(targetHeight: 576);

        // Act
        var action = () => sut.ResolveForEncode(
            request,
            outputHeight: 650,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: 4_000_000,
            hasAudio: true);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolveForDownscale_WhenAutoSampleModeIsMissing_UsesTargetProfileAndHybridMode()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateRequest(targetHeight: 576, contentProfile: "anime", qualityProfile: "high");

        // Act
        var actual = sut.ResolveForDownscale(
            request,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: 4_000_000,
            hasAudio: true);

        // Assert
        actual.Profile.TargetHeight.Should().Be(576);
        actual.EffectiveRequest.TargetHeight.Should().Be(576);
        actual.EffectiveRequest.AutoSampleMode.Should().Be("hybrid");
        actual.BaseSettings.ContentProfile.Should().Be("anime");
        actual.BaseSettings.QualityProfile.Should().Be("high");
    }

    [Fact]
    public void ResolveForDownscale_WhenTargetHeightIsMissing_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateRequest();

        // Act
        var action = () => sut.ResolveForDownscale(
            request,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: 4_000_000,
            hasAudio: true);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    private static ProfileDrivenVideoSettingsResolver CreateSut(VideoSettingsProfiles? profiles = null)
    {
        return new ProfileDrivenVideoSettingsResolver(profiles ?? VideoSettingsProfiles.Default);
    }

    private static VideoSettingsRequest CreateRequest(
        int? targetHeight = null,
        string? contentProfile = null,
        string? qualityProfile = null,
        string? autoSampleMode = null,
        string? algorithm = null,
        int? cq = null,
        decimal? maxrate = null,
        decimal? bufsize = null)
    {
        return new VideoSettingsRequest(
            targetHeight: targetHeight,
            contentProfile: contentProfile,
            qualityProfile: qualityProfile,
            autoSampleMode: autoSampleMode,
            algorithm: algorithm,
            cq: cq,
            maxrate: maxrate,
            bufsize: bufsize);
    }
}
