using FluentAssertions;
using MediaTranscodeEngine.Core.Commanding;

namespace MediaTranscodeEngine.Core.Tests.Commanding;

public class H264CommandBuilderTests
{
    [Fact]
    public void BuildRemux_WhenOutputIsMp4_IncludesFaststartAndCopy()
    {
        var sut = CreateSut();
        var input = CreateRemuxInput(outputMkv: false);

        var actual = sut.BuildRemux(input);

        actual.Should().Contain("-c copy");
        actual.Should().Contain("-movflags +faststart");
        actual.Should().Contain("move /Y");
    }

    [Fact]
    public void BuildRemux_WhenOutputIsMkv_DoesNotIncludeFaststart()
    {
        var sut = CreateSut();
        var input = CreateRemuxInput(outputMkv: true);

        var actual = sut.BuildRemux(input);

        actual.Should().Contain("-c copy");
        actual.Should().NotContain("-movflags +faststart");
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

    private static H264CommandBuilder CreateSut()
    {
        return new H264CommandBuilder();
    }

    private static H264RemuxCommandInput CreateRemuxInput(bool outputMkv)
    {
        return new H264RemuxCommandInput(
            InputPath: "C:\\video\\a.mp4",
            OutputPath: outputMkv ? "C:\\video\\a.mkv" : "C:\\video\\a.mp4",
            TempOutputPath: outputMkv ? "C:\\video\\a (h264).mkv" : "C:\\video\\a (h264).mp4",
            OutputMkv: outputMkv);
    }

    private static H264EncodeCommandInput CreateEncodeInput(
        bool applyDownscale = false,
        bool useAq = false,
        bool denoise = false,
        bool fixTimestamps = false)
    {
        return new H264EncodeCommandInput(
            InputPath: "C:\\video\\a.mkv",
            OutputPath: "C:\\video\\a.mp4",
            TempOutputPath: "C:\\video\\a (h264).mp4",
            NvencPreset: "p5",
            Cq: 19,
            FpsToken: "25/1",
            Gop: 50,
            OutputMkv: false,
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
