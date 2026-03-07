using FluentAssertions;
using MediaTranscodeEngine.Runtime.Downscaling;
using MediaTranscodeEngine.Runtime.Plans;

namespace MediaTranscodeEngine.Runtime.Tests.Plans;

public sealed class TranscodePlanTests
{
    [Fact]
    public void Ctor_WhenCopyVideoPlanRequestsTargetHeight_ThrowsArgumentException()
    {
        Action action = () => CreateCopyVideoPlan(targetHeight: 576);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Video copy plan cannot request target height*");
    }

    [Fact]
    public void Ctor_WhenCopyVideoPlanRequestsTargetFrameRate_ThrowsArgumentException()
    {
        Action action = () => CreateCopyVideoPlan(targetFramesPerSecond: 60);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Video copy plan cannot request target frame rate*");
    }

    [Fact]
    public void Ctor_WhenCopyVideoPlanRequestsInterpolation_ThrowsArgumentException()
    {
        Action action = () => CreateCopyVideoPlan(useFrameInterpolation: true);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Video copy plan cannot request frame interpolation*");
    }

    [Fact]
    public void Ctor_WhenInterpolationHasNoTargetFrameRate_ThrowsArgumentException()
    {
        Action action = () => new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: "h264",
            preferredBackend: "nvenc",
            targetHeight: null,
            targetFramesPerSecond: null,
            useFrameInterpolation: true,
            downscale: null,
            copyVideo: false,
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Frame interpolation requires a target frame rate*");
    }

    [Fact]
    public void Ctor_WhenDownscaleTargetDoesNotMatchTargetHeight_ThrowsArgumentException()
    {
        Action action = () => new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: "h264",
            preferredBackend: "nvenc",
            targetHeight: 720,
            targetFramesPerSecond: null,
            useFrameInterpolation: false,
            downscale: new DownscaleRequest(targetHeight: 576),
            copyVideo: false,
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Downscale target must match target height*");
    }

    [Fact]
    public void Ctor_WhenDownscaleTargetIsProvidedWithoutPlanTargetHeight_ThrowsArgumentException()
    {
        Action action = () => new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: "h264",
            preferredBackend: "nvenc",
            targetHeight: null,
            targetFramesPerSecond: null,
            useFrameInterpolation: false,
            downscale: new DownscaleRequest(targetHeight: 576),
            copyVideo: false,
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Downscale target requires target height in the transcode plan*");
    }

    [Fact]
    public void Ctor_WhenOptionalTokensAndPathAreProvided_NormalizesThem()
    {
        var actual = new TranscodePlan(
            targetContainer: " MKV ",
            targetVideoCodec: " H264 ",
            preferredBackend: " Nvenc ",
            targetHeight: 576,
            targetFramesPerSecond: 23.976,
            useFrameInterpolation: false,
            downscale: new DownscaleRequest(targetHeight: 576),
            copyVideo: false,
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true,
            encoderPreset: " P5 ",
            outputPath: @".\output.mkv");

        actual.TargetContainer.Should().Be("mkv");
        actual.TargetVideoCodec.Should().Be("h264");
        actual.PreferredBackend.Should().Be("nvenc");
        actual.EncoderPreset.Should().Be("p5");
        actual.OutputPath.Should().Be(Path.GetFullPath(@".\output.mkv"));
    }

    [Fact]
    public void Ctor_WhenDownscaleHasNoValue_DropsIt()
    {
        var actual = new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: "h264",
            preferredBackend: "nvenc",
            targetHeight: null,
            targetFramesPerSecond: null,
            useFrameInterpolation: false,
            downscale: new DownscaleRequest(),
            copyVideo: false,
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true);

        actual.Downscale.Should().BeNull();
    }

    private static TranscodePlan CreateCopyVideoPlan(
        int? targetHeight = null,
        double? targetFramesPerSecond = null,
        bool useFrameInterpolation = false)
    {
        return new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: null,
            preferredBackend: null,
            targetHeight: targetHeight,
            targetFramesPerSecond: targetFramesPerSecond,
            useFrameInterpolation: useFrameInterpolation,
            downscale: null,
            copyVideo: true,
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true);
    }
}
