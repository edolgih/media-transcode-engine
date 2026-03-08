using FluentAssertions;
using MediaTranscodeEngine.Runtime.Downscaling;
using MediaTranscodeEngine.Runtime.Plans;
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
        actual.VideoCompatibilityProfile.Should().Be(VideoCompatibilityProfile.H264High);
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
        actual.FixTimestamps.Should().BeTrue();
        actual.SynchronizeAudio.Should().BeTrue();
    }

    [Fact]
    public void BuildPlan_WhenMaxFpsIsLowerThanSourceFrameRate_ForcesVideoEncodeWithTargetFrameRate()
    {
        var sut = CreateSut(maxFramesPerSecond: 50);
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["aac"], framesPerSecond: 59.94);

        var actual = sut.BuildPlan(video);

        actual.CopyVideo.Should().BeFalse();
        actual.CopyAudio.Should().BeFalse();
        actual.TargetFramesPerSecond.Should().Be(50);
        actual.FixTimestamps.Should().BeTrue();
    }

    [Fact]
    public void BuildPlan_WhenMaxFpsIsNotLowerThanSourceFrameRate_DoesNotForceVideoEncode()
    {
        var sut = CreateSut(maxFramesPerSecond: 30);
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["aac"], framesPerSecond: 23.976);

        var actual = sut.BuildPlan(video);

        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeTrue();
        actual.TargetFramesPerSecond.Should().BeNull();
    }

    [Fact]
    public void Ctor_WhenMaxFpsIsNotSupported_ThrowsArgumentOutOfRangeException()
    {
        Action action = static () => _ = new ToMkvGpuRequest(maxFramesPerSecond: 55);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*50, 40, 30, 24*");
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
        actual.Downscale.Should().NotBeNull();
        actual.Downscale!.TargetHeight.Should().Be(576);
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
    public void BuildPlan_WhenDownscale576RequestedForZeroHeight_ThrowsBucketHint()
    {
        var sut = CreateSut(downscaleTarget: 576);
        var video = CreateVideo(height: 0, videoCodec: "h264", audioCodecs: ["aac"]);

        var action = () => sut.BuildPlan(video);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*576 source bucket missing*")
            .WithMessage("*height 0*");
    }

    [Fact]
    public void BuildPlan_WhenEncodeOverridesAreRequestedWithoutResize_PreservesOverridesForToolRendering()
    {
        var sut = new ToMkvGpuScenario(new ToMkvGpuRequest(
            downscale: new DownscaleRequest(cq: 23),
            nvencPreset: "p5"));
        var video = CreateVideo(container: "mp4", videoCodec: "av1", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");

        var actual = sut.BuildPlan(video);

        actual.CopyVideo.Should().BeFalse();
        actual.Downscale.Should().NotBeNull();
        actual.Downscale!.Cq.Should().Be(23);
        actual.EncoderPreset.Should().Be("p5");
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

    [Fact]
    public void BuildPlan_WhenDownscale576SourceBucketIsMissing_ThrowsInformativeError()
    {
        var profile = new DownscaleProfile(
            targetHeight: 576,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new DownscaleRateModel(0.4m, 2.0m),
            autoSampling: CreateAutoSampling(),
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "fhd_1080",
                    MinHeight: 1000,
                    MaxHeight: 1300,
                    Ranges: CreateCompleteRanges())
            ],
            defaults: CreateDefaults());
        var sut = new ToMkvGpuScenario(
            new ToMkvGpuRequest(downscale: new DownscaleRequest(targetHeight: 576)),
            DownscaleProfiles.Create(profile));
        var video = CreateVideo(container: "mp4", height: 900, videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");

        var action = () => sut.BuildPlan(video);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*576 source bucket missing*")
            .WithMessage("*add SourceBuckets*");
    }

    [Fact]
    public void BuildPlan_WhenDownscale576BucketRangesAreIncomplete_ThrowsInformativeError()
    {
        var profile = new DownscaleProfile(
            targetHeight: 576,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new DownscaleRateModel(0.4m, 2.0m),
            autoSampling: CreateAutoSampling(),
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "fhd_1080",
                    MinHeight: 1000,
                    MaxHeight: 1300,
                    Ranges: CreateCompleteRanges().Where(static range => !(range.ContentProfile == "mult" && range.QualityProfile == "low")).ToArray())
            ],
            defaults: CreateDefaults());
        var sut = new ToMkvGpuScenario(
            new ToMkvGpuRequest(downscale: new DownscaleRequest(targetHeight: 576)),
            DownscaleProfiles.Create(profile));
        var video = CreateVideo(container: "mp4", height: 1080, videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");

        var action = () => sut.BuildPlan(video);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*576 source bucket invalid*")
            .WithMessage("*mult/low*");
    }

    private static ToMkvGpuScenario CreateSut(
        bool overlayBackground = false,
        int? downscaleTarget = null,
        bool synchronizeAudio = false,
        bool keepSource = false,
        int? maxFramesPerSecond = null)
    {
        return new ToMkvGpuScenario(new ToMkvGpuRequest(
            overlayBackground: overlayBackground,
            synchronizeAudio: synchronizeAudio,
            keepSource: keepSource,
            downscale: downscaleTarget.HasValue ? new DownscaleRequest(targetHeight: downscaleTarget) : null,
            maxFramesPerSecond: maxFramesPerSecond));
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

    private static DownscaleDefaults[] CreateDefaults()
    {
        return
        [
            new DownscaleDefaults("anime", "high", 22, 3.3m, 6.5m, "bilinear", 19, 24, 2.4m, 4.2m),
            new DownscaleDefaults("anime", "default", 23, 2.4m, 4.8m, "bilinear", 20, 26, 2.0m, 3.0m),
            new DownscaleDefaults("anime", "low", 29, 2.1m, 4.1m, "bilinear", 24, 35, 1.0m, 3.2m),
            new DownscaleDefaults("mult", "high", 24, 2.7m, 5.3m, "bilinear", 21, 26, 2.4m, 3.2m),
            new DownscaleDefaults("mult", "default", 26, 2.4m, 4.8m, "bilinear", 23, 29, 2.0m, 2.8m),
            new DownscaleDefaults("mult", "low", 29, 1.7m, 3.5m, "bilinear", 26, 31, 1.6m, 2.0m),
            new DownscaleDefaults("film", "high", 24, 3.7m, 7.4m, "bilinear", 16, 33, 2.0m, 8.0m),
            new DownscaleDefaults("film", "default", 26, 3.4m, 6.9m, "bilinear", 18, 35, 1.6m, 8.0m),
            new DownscaleDefaults("film", "low", 30, 2.2m, 4.5m, "bilinear", 20, 38, 1.2m, 4.0m)
        ];
    }

    private static DownscaleRange[] CreateCompleteRanges()
    {
        return
        [
            new DownscaleRange("anime", "high", MinInclusive: 30.0m, MaxInclusive: 45.0m),
            new DownscaleRange("anime", "default", MinInclusive: 45.0m, MaxInclusive: 60.0m),
            new DownscaleRange("anime", "low", MinInclusive: 60.0m, MaxInclusive: 80.0m),
            new DownscaleRange("mult", "high", MinInclusive: 28.0m, MaxInclusive: 42.0m),
            new DownscaleRange("mult", "default", MinInclusive: 42.0m, MaxInclusive: 57.0m),
            new DownscaleRange("mult", "low", MinInclusive: 57.0m, MaxInclusive: 77.0m),
            new DownscaleRange("film", "high", MinInclusive: 20.0m, MaxInclusive: 35.0m),
            new DownscaleRange("film", "default", MinInclusive: 35.0m, MaxInclusive: 50.0m),
            new DownscaleRange("film", "low", MinInclusive: 50.0m, MaxInclusive: 70.0m)
        ];
    }

    private static DownscaleAutoSampling CreateAutoSampling()
    {
        return new DownscaleAutoSampling(
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
            ShortWindowAnchors: [0.50]);
    }
}
