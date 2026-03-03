using FluentAssertions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Commanding;

public class H264CommandBuilderTests
{
    [Fact]
    public void BuildCommand_WhenContainerMp4_AddsFaststart()
    {
        var sut = CreateSut();
        var input = CreateRemuxInput(outputMkv: false);

        var actual = sut.BuildRemux(input);

        actual.Should().Contain("-c copy");
        actual.Should().Contain("-movflags +faststart");
        actual.Should().Contain("move /Y");
    }

    [Fact]
    public void BuildCommand_WhenContainerMkv_DoesNotAddFaststart()
    {
        var sut = CreateSut();
        var input = CreateRemuxInput(outputMkv: true);

        var actual = sut.BuildRemux(input);

        actual.Should().Contain("-c copy");
        actual.Should().NotContain("-movflags +faststart");
    }

    [Fact]
    public void BuildRemux_WhenReplaceInputDisabled_WritesToFinalOutputAndKeepsSource()
    {
        var sut = CreateSut();
        var input = CreateRemuxInput(outputMkv: true) with { ReplaceInput = false };

        var actual = sut.BuildRemux(input);

        actual.Should().Contain("\"C:\\video\\a.mkv\"");
        actual.Should().NotContain("&& del ");
        actual.Should().NotContain("move /Y");
    }

    [Fact]
    public void BuildEncode_WhenDownscaleAndUseAqEnabled_IncludesScaleCudaAndAqFlags()
    {
        var sut = CreateSut();
        var input = CreateEncodeInput(
            applyDownscale: true,
            useAq: true);

        var actual = sut.BuildEncode(input);

        actual.Should().Contain("-hwaccel cuda -hwaccel_output_format cuda");
        actual.Should().Contain("scale_cuda=-2:576:interp_algo=lanczos:format=nv12");
        actual.Should().Contain("-spatial_aq 1");
        actual.Should().Contain("-temporal_aq 1");
        actual.Should().Contain("-aq-strength 5");
    }

    [Fact]
    public void BuildEncode_WhenDenoiseAndFixTimestampsEnabled_IncludesExpectedFlags()
    {
        var sut = CreateSut();
        var input = CreateEncodeInput(
            denoise: true,
            fixTimestamps: true,
            applyDownscale: false);

        var actual = sut.BuildEncode(input);

        actual.Should().Contain("-fflags +genpts+igndts");
        actual.Should().Contain("-vf \"hqdn3d=1.2:1.2:6:6\"");
        actual.Should().Contain("-c:v h264_nvenc");
    }

    [Fact]
    public void BuildEncode_WhenReplaceInputDisabled_WritesToFinalOutputAndKeepsSource()
    {
        var sut = CreateSut();
        var input = CreateEncodeInput() with { ReplaceInput = false };

        var actual = sut.BuildEncode(input);

        actual.Should().Contain("\"C:\\video\\a.mp4\"");
        actual.Should().NotContain("&& del ");
        actual.Should().NotContain("move /Y");
    }

    private static H264CommandBuilder CreateSut()
    {
        return new H264CommandBuilder();
    }

    private static H264RemuxCommandInput CreateRemuxInput(bool outputMkv)
    {
        IContainerPolicy containerPolicy = outputMkv
            ? new MkvContainerPolicy()
            : new Mp4ContainerPolicy();

        return new H264RemuxCommandInput(
            InputPath: "C:\\video\\a.mp4",
            OutputPath: outputMkv ? "C:\\video\\a.mkv" : "C:\\video\\a.mp4",
            TempOutputPath: outputMkv ? "C:\\video\\a (h264).mkv" : "C:\\video\\a (h264).mp4",
            ContainerPolicy: containerPolicy);
    }

    private static H264EncodeCommandInput CreateEncodeInput(
        bool outputMkv = false,
        bool applyDownscale = false,
        bool useAq = false,
        bool denoise = false,
        bool fixTimestamps = false)
    {
        IContainerPolicy containerPolicy = outputMkv
            ? new MkvContainerPolicy()
            : new Mp4ContainerPolicy();
        var outputPath = outputMkv ? "C:\\video\\a.mkv" : "C:\\video\\a.mp4";
        var tempOutputPath = outputMkv ? "C:\\video\\a (h264).mkv" : "C:\\video\\a (h264).mp4";

        return new H264EncodeCommandInput(
            InputPath: "C:\\video\\a.mkv",
            OutputPath: outputPath,
            TempOutputPath: tempOutputPath,
            NvencPreset: "p5",
            Cq: 19,
            FpsToken: "25/1",
            Gop: 50,
            ContainerPolicy: containerPolicy,
            ApplyDownscale: applyDownscale,
            DownscaleTarget: 576,
            DownscaleAlgo: "lanczos",
            UseAq: useAq,
            AqStrength: 5,
            Denoise: denoise,
            FixTimestamps: fixTimestamps,
            CopyAudio: false);
    }
}
