using FluentAssertions;
using Transcode.Core.VideoSettings;

namespace Transcode.Runtime.Tests.VideoSettings;

/*
Это тесты resolver-а profile-driven video settings.
Они покрывают выбор профиля, вычисление default-настроек и сочетание их с explicit overrides.
*/
/// <summary>
/// Verifies profile resolution and override application in the video-settings resolver.
/// </summary>
public sealed class VideoSettingsResolverTests
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
        actual.EffectiveSelection.AutoSampleMode.Should().Be("fast");
    }

    [Fact]
    public void ResolveForEncode_WhenRequestContainsProfiles_UsesProvidedProfiles()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateRequest(contentProfile: "anime", qualityProfile: "high");

        // Act
        var actual = sut.ResolveForEncode(
            request: request,
            outputHeight: 650,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: 4_000_000,
            hasAudio: true);

        // Assert
        actual.BaseSettings.ContentProfile.Should().Be("anime");
        actual.BaseSettings.QualityProfile.Should().Be("high");
    }

    [Fact]
    public void ResolveForDownscale_WhenAutoSampleModeIsMissing_UsesTargetProfileAndHybridMode()
    {
        // Arrange
        var sut = CreateSut();
        var downscale = new DownscaleRequest(576);
        var request = CreateRequest(contentProfile: "anime", qualityProfile: "high");

        // Act
        var actual = sut.ResolveForDownscale(
            request: downscale,
            videoSettings: request,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: 4_000_000,
            hasAudio: true);

        // Assert
        actual.Profile.TargetHeight.Should().Be(576);
        actual.EffectiveSelection.AutoSampleMode.Should().Be("hybrid");
        actual.BaseSettings.ContentProfile.Should().Be("anime");
        actual.BaseSettings.QualityProfile.Should().Be("high");
    }

    [Fact]
    public void ResolveForDownscale_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var action = () => sut.ResolveForDownscale(
            request: null!,
            videoSettings: null,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: 4_000_000,
            hasAudio: true);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    private static VideoSettingsResolver CreateSut(VideoSettingsProfiles? profiles = null)
    {
        return new VideoSettingsResolver(profiles ?? VideoSettingsProfiles.Default);
    }

    private static VideoSettingsRequest CreateRequest(
        string? contentProfile = null,
        string? qualityProfile = null,
        string? autoSampleMode = null,
        int? cq = null,
        decimal? maxrate = null,
        decimal? bufsize = null)
    {
        return new VideoSettingsRequest(
            contentProfile: contentProfile,
            qualityProfile: qualityProfile,
            autoSampleMode: autoSampleMode,
            cq: cq,
            maxrate: maxrate,
            bufsize: bufsize);
    }
}
