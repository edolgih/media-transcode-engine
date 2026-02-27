using FluentAssertions;
using MediaTranscodeEngine.Core.Commanding;

namespace MediaTranscodeEngine.Core.Tests.Commanding;

public class FfmpegCommandBuilderTests
{
    [Fact]
    public void Build_WhenContainerChangeWithCopyStreams_UsesCopyCodecsAndGenptsSanitize()
    {
        var sut = CreateSut();
        var input = CreateInput(
            needVideoEncode: false,
            needAudioEncode: false,
            needContainer: true);

        var actual = sut.Build(input);

        actual.Should().Contain("-fflags +genpts -avoid_negative_ts make_zero");
        actual.Should().Contain("-map 0:v:0 -c:v copy");
        actual.Should().Contain("-map 0:a? -c:a copy");
        actual.Should().Contain("-sn");
        actual.Should().Contain("-max_muxing_queue_size 4096");
    }

    [Fact]
    public void Build_WhenVideoEncodeWithoutOverlay_UsesNvencAndSourceVideoMap()
    {
        var sut = CreateSut();
        var input = CreateInput(
            needVideoEncode: true,
            needAudioEncode: true,
            overlayBg: false,
            applyDownscale: false);

        var actual = sut.Build(input);

        actual.Should().Contain("-c:v h264_nvenc");
        actual.Should().Contain("-map 0:v:0");
        actual.Should().Contain("-fps_mode:v cfr");
        actual.Should().NotContain("-filter_complex");
        actual.Should().NotContain("-map \"[v]\"");
    }

    [Fact]
    public void Build_WhenDownscale576WithoutOverlay_UsesScaleCudaWithProfileRates()
    {
        var sut = CreateSut();
        var input = CreateInput(
            needVideoEncode: true,
            needAudioEncode: true,
            applyDownscale: true,
            downscaleTarget: 576,
            downscaleAlgo: "bilinear",
            maxrate: 2.4,
            bufsize: 4.8);

        var actual = sut.Build(input);

        actual.Should().Contain("-hwaccel cuda -hwaccel_output_format cuda");
        actual.Should().Contain("-vf \"scale_cuda=-2:576:interp_algo=bilinear:format=nv12\"");
        actual.Should().Contain("-maxrate 2.4M");
        actual.Should().Contain("-bufsize 4.8M");
    }

    [Fact]
    public void Build_WhenOverlayEnabled_UsesFilterComplexAndOverlayMap()
    {
        var sut = CreateSut();
        var input = CreateInput(
            needVideoEncode: true,
            overlayBg: true,
            applyDownscale: true,
            sourceWidth: 1920,
            sourceHeight: 1080,
            downscaleTarget: 576);

        var actual = sut.Build(input);

        actual.Should().Contain("-filter_complex");
        actual.Should().Contain("overlay_cuda");
        actual.Should().Contain("-map \"[v]\"");
        actual.Should().NotContain("-map 0:v:0 -c:v copy");
    }

    [Fact]
    public void Build_WhenNeedVideoEncodeAndSourceFpsAbove30_UsesLevel42()
    {
        var sut = CreateSut();
        var input = CreateInput(
            needVideoEncode: true,
            sourceFps: 59.94,
            sourceWidth: 1920,
            sourceHeight: 800);

        var actual = sut.Build(input);

        actual.Should().Contain("-level:v 4.2");
    }

    [Fact]
    public void Build_WhenNeedVideoEncodeAndSourceFpsAbove30AndDownscale576_UsesLevel41()
    {
        var sut = CreateSut();
        var input = CreateInput(
            needVideoEncode: true,
            applyDownscale: true,
            downscaleTarget: 576,
            sourceFps: 59.94,
            sourceWidth: 1920,
            sourceHeight: 800);

        var actual = sut.Build(input);

        actual.Should().Contain("-level:v 4.1");
        actual.Should().NotContain("-level:v 4.2");
    }

    [Theory]
    [InlineData(true, true, false, false, true, true)]
    [InlineData(false, true, false, false, false, false)]
    [InlineData(false, true, false, true, true, false)]
    public void Build_WhenCalled_UsesExpectedSanitizeFlags(
        bool needVideoEncode,
        bool needAudioEncode,
        bool needContainer,
        bool forceSyncAudio,
        bool expectedGenpts,
        bool expectedIgndts)
    {
        var sut = CreateSut();
        var input = CreateInput(
            needVideoEncode: needVideoEncode,
            needAudioEncode: needAudioEncode,
            needContainer: needContainer,
            forceSyncAudio: forceSyncAudio);

        var actual = sut.Build(input);

        actual.Should().Contain("-avoid_negative_ts make_zero");
        actual.Contains("+genpts").Should().Be(expectedGenpts);
        actual.Contains("+igndts").Should().Be(expectedIgndts);
    }

    private static FfmpegCommandBuilder CreateSut()
    {
        return new FfmpegCommandBuilder();
    }

    private static FfmpegCommandInput CreateInput(
        bool needVideoEncode = true,
        bool needAudioEncode = true,
        bool needContainer = false,
        bool forceSyncAudio = false,
        bool applyDownscale = false,
        int downscaleTarget = 576,
        bool overlayBg = false,
        int? sourceWidth = 1920,
        int? sourceHeight = 1080,
        int cq = 23,
        double maxrate = 2.4,
        double bufsize = 4.8,
        string downscaleAlgo = "bilinear",
        double? sourceFps = null)
    {
        return new FfmpegCommandInput(
            InputPath: "C:\\input\\video.mp4",
            OutputPath: "C:\\output\\video.mkv",
            PostOperation: "&& del \"C:\\input\\video.mp4\"",
            NeedVideoEncode: needVideoEncode,
            NeedAudioEncode: needAudioEncode,
            NeedContainer: needContainer,
            ForceSyncAudio: forceSyncAudio,
            ApplyDownscale: applyDownscale,
            DownscaleTarget: downscaleTarget,
            OverlayBg: overlayBg,
            SourceWidth: sourceWidth,
            SourceHeight: sourceHeight,
            Cq: cq,
            Maxrate: maxrate,
            Bufsize: bufsize,
            DownscaleAlgo: downscaleAlgo,
            SourceFps: sourceFps,
            NvencPreset: "p6");
    }
}
