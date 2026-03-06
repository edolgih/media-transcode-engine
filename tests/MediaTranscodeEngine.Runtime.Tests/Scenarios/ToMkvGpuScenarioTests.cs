using FluentAssertions;
using MediaTranscodeEngine.Runtime.Downscaling;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tests.Scenarios;

public sealed class ToMkvGpuScenarioTests
{
    [Theory]
    [InlineData("h264")]
    [InlineData("mpeg4")]
    public void BuildPlan_WhenVideoCodecIsCopyCompatibleAndNoOverrides_CopiesVideo(string videoCodec)
    {
        var sut = CreateSut();
        var video = CreateVideo(videoCodec: videoCodec, audioCodecs: ["aac"], container: "mkv");

        var actual = sut.BuildPlan(video);

        actual.CopyVideo.Should().BeTrue();
        actual.TargetVideoCodec.Should().BeNull();
        actual.PreferredBackend.Should().BeNull();
        actual.TargetContainer.Should().Be("mkv");
        actual.OutputPath.Should().Be(video.FilePath);
    }

    [Fact]
    public void BuildPlan_WhenSourceContainerIsNotMkv_ReturnsMkvOutputPath()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4");

        var actual = sut.BuildPlan(video);

        actual.TargetContainer.Should().Be("mkv");
        actual.OutputPath.Should().Be(@"C:\video\input.mkv");
    }

    [Fact]
    public void BuildPlan_WhenKeepSourceAndMkvInputRequiresEncode_ReturnsDistinctOutputPath()
    {
        var sut = CreateSut(keepSource: true);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input.mkv");

        var actual = sut.BuildPlan(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        actual.OutputPath.Should().Be(@"C:\video\input_out.mkv");
    }

    [Theory]
    [InlineData(@"C:\video\input.wmv")]
    [InlineData(@"C:\video\input.asf")]
    public void BuildPlan_WhenInputIsAsfFamily_ForcesEncodeAndTimestampFix(string filePath)
    {
        var sut = CreateSut();
        var video = CreateVideo(container: Path.GetExtension(filePath).TrimStart('.'), videoCodec: "h264", audioCodecs: ["aac"], filePath: filePath);

        var actual = sut.BuildPlan(video);

        actual.CopyVideo.Should().BeFalse();
        actual.CopyAudio.Should().BeFalse();
        actual.FixTimestamps.Should().BeTrue();
        actual.TargetVideoCodec.Should().Be("h264");
    }

    [Fact]
    public void BuildPlan_WhenVideoCodecIsNotCopyCompatible_UsesH264GpuEncodePlan()
    {
        var sut = CreateSut();
        var video = CreateVideo(videoCodec: "av1", audioCodecs: ["aac"]);

        var actual = sut.BuildPlan(video);

        actual.CopyVideo.Should().BeFalse();
        actual.TargetVideoCodec.Should().Be("h264");
        actual.PreferredBackend.Should().Be("gpu");
        actual.CopyAudio.Should().BeFalse();
        actual.FixTimestamps.Should().BeTrue();
    }

    [Fact]
    public void BuildPlan_WhenOverlayBackgroundIsRequested_ForcesVideoEncode()
    {
        var sut = CreateSut(overlayBackground: true);
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["aac"]);

        var actual = sut.BuildPlan(video);

        actual.CopyVideo.Should().BeFalse();
        actual.ApplyOverlayBackground.Should().BeTrue();
        actual.TargetVideoCodec.Should().Be("h264");
    }

    [Fact]
    public void BuildPlan_WhenAudioContainsNonAac_ForcesAudioEncodeWhileKeepingVideoCopy()
    {
        var sut = CreateSut();
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["aac", "ac3"]);

        var actual = sut.BuildPlan(video);

        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeFalse();
        actual.SynchronizeAudio.Should().BeFalse();
    }

    [Fact]
    public void BuildPlan_WhenSynchronizeAudioIsRequested_ForcesAudioEncode()
    {
        var sut = CreateSut(synchronizeAudio: true);
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["aac"]);

        var actual = sut.BuildPlan(video);

        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeFalse();
        actual.SynchronizeAudio.Should().BeTrue();
    }

    [Fact]
    public void BuildPlan_WhenVideoIsEncoded_AlwaysEncodesAudio()
    {
        var sut = CreateSut();
        var video = CreateVideo(videoCodec: "hevc", audioCodecs: ["aac"]);

        var actual = sut.BuildPlan(video);

        actual.CopyVideo.Should().BeFalse();
        actual.CopyAudio.Should().BeFalse();
    }

    [Fact]
    public void BuildPlan_WhenDownscale576RequestedForLargerSource_AppliesDownscaleAndForcesEncode()
    {
        var sut = CreateSut(downscaleTarget: 576);
        var video = CreateVideo(height: 1080, videoCodec: "h264", audioCodecs: ["aac"]);

        var actual = sut.BuildPlan(video);

        actual.TargetHeight.Should().Be(576);
        actual.CopyVideo.Should().BeFalse();
        actual.TargetVideoCodec.Should().Be("h264");
    }

    [Fact]
    public void BuildPlan_WhenDownscale576RequestedForSmallerSource_DoesNotApplyDownscale()
    {
        var sut = CreateSut(downscaleTarget: 576);
        var video = CreateVideo(height: 480, videoCodec: "h264", audioCodecs: ["aac"]);

        var actual = sut.BuildPlan(video);

        actual.TargetHeight.Should().BeNull();
        actual.CopyVideo.Should().BeTrue();
    }

    [Fact]
    public void BuildPlan_WhenDownscale720Requested_ThrowsNotSupportedException()
    {
        var sut = CreateSut(downscaleTarget: 720);
        var video = CreateVideo();

        var action = () => sut.BuildPlan(video);

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*720*");
    }

    private static ToMkvGpuScenario CreateSut(
        bool overlayBackground = false,
        int? downscaleTarget = null,
        bool synchronizeAudio = false,
        bool keepSource = false)
    {
        return new ToMkvGpuScenario(new ToMkvGpuRequest(
            overlayBackground: overlayBackground,
            synchronizeAudio: synchronizeAudio,
            keepSource: keepSource,
            downscale: downscaleTarget.HasValue ? new DownscaleRequest(targetHeight: downscaleTarget) : null));
    }

    private static SourceVideo CreateVideo(
        string container = "mkv",
        string videoCodec = "h264",
        IReadOnlyList<string>? audioCodecs = null,
        int width = 1920,
        int height = 1080,
        double framesPerSecond = 29.97,
        string filePath = @"C:\video\input.mkv")
    {
        return new SourceVideo(
            filePath: filePath,
            container: container,
            videoCodec: videoCodec,
            audioCodecs: audioCodecs ?? ["aac"],
            width: width,
            height: height,
            framesPerSecond: framesPerSecond,
            duration: TimeSpan.FromMinutes(10));
    }
}
