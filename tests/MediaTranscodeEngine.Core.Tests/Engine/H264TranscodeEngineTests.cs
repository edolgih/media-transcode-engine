using FluentAssertions;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Policy;
using NSubstitute;

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class H264TranscodeEngineTests
{
    [Fact]
    public void Process_WhenProbeMissing_ReturnsFfprobeFailedRem()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns((ProbeResult?)null);
        var request = CreateRequest();

        var actual = sut.Process(request);

        actual.Should().Be("REM ffprobe failed: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenNoVideoStream_ReturnsNoVideoRem()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(new ProbeResult(
            Format: null,
            Streams: [new ProbeStream("audio", "aac")]));
        var request = CreateRequest();

        var actual = sut.Process(request);

        actual.Should().Be("REM Нет видеопотока: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenInputEligibleForRemux_ReturnsCopyCommandWithFaststart()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe());
        var request = CreateRequest();

        var actual = sut.Process(request);

        actual.Should().Contain("-c copy");
        actual.Should().Contain("-movflags +faststart");
        actual.Should().Contain("\"C:\\video\\a (h264).mp4\"");
    }

    [Fact]
    public void Process_WhenInputEligibleForRemuxAndOutputMkv_ReturnsCopyCommandWithoutFaststart()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe());
        var request = CreateRequest(outputMkv: true);

        var actual = sut.Process(request);

        actual.Should().Contain("-c copy");
        actual.Should().NotContain("-movflags +faststart");
        actual.Should().Contain("\"C:\\video\\a (h264).mkv\"");
    }

    [Fact]
    public void Process_WhenVfrSuspected_FallsBackToEncodeCommand()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(
            rFrameRate: "24000/1001",
            avgFrameRate: "30000/1001"));
        var request = CreateRequest();

        var actual = sut.Process(request);

        actual.Should().Contain("-c:v h264_nvenc");
        actual.Should().NotContain("-c copy");
    }

    [Fact]
    public void Process_WhenDenoiseEnabled_FallsBackToEncodeCommand()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe());
        var request = CreateRequest(denoise: true);

        var actual = sut.Process(request);

        actual.Should().Contain("-c:v h264_nvenc");
        actual.Should().Contain("-vf \"hqdn3d=1.2:1.2:6:6\"");
    }

    [Fact]
    public void Process_WhenDownscaleRequestedAndSourceFpsAbove30AndKeepFpsFalse_UsesCappedFps()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(
            height: 1080,
            rFrameRate: "60/1",
            avgFrameRate: "60/1"));
        var request = CreateRequest(downscale: 576, keepFps: false);

        var actual = sut.Process(request);

        actual.Should().Contain("-r 30000/1001");
        actual.Should().Contain("scale_cuda=-2:576");
    }

    [Fact]
    public void Process_WhenDownscaleRequestedAndKeepFpsTrue_UsesSourceFps()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(
            height: 1080,
            rFrameRate: "60/1",
            avgFrameRate: "60/1"));
        var request = CreateRequest(downscale: 576, keepFps: true);

        var actual = sut.Process(request);

        actual.Should().Contain("-r 60/1");
        actual.Should().Contain("scale_cuda=-2:576");
    }

    [Fact]
    public void Process_WhenAsfFormatAutoFixEnabled_DisablesAudioCopyAndAddsFflags()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(
            formatName: "asf",
            audioCodec: "aac"));
        var request = CreateRequest(inputPath: "C:\\video\\a.mp4");

        var actual = sut.Process(request);

        actual.Should().Contain("-fflags +genpts+igndts");
        actual.Should().Contain("-c:a aac -b:a 160k");
        actual.Should().NotContain("-c:a copy");
    }

    [Fact]
    public void ProcessWithProbeJson_WhenProbeJsonValid_BuildsCommandAndSkipsProbeReader()
    {
        var (sut, probeReader) = CreateSut();
        var request = CreateRequest();
        var probeJson = """
            {
              "format": { "format_name": "mov,mp4,m4a,3gp,3g2,mj2", "duration": "10.0" },
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "r_frame_rate": "25/1", "avg_frame_rate": "25/1", "width": 1920, "height": 1080 },
                { "codec_type": "audio", "codec_name": "aac" }
              ]
            }
            """;

        var actual = sut.ProcessWithProbeJson(request, probeJson);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().Contain("-c copy");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("{invalid")]
    public void ProcessWithProbeJson_WhenProbeJsonInvalid_ReturnsFfprobeFailedRem(string? probeJson)
    {
        var (sut, probeReader) = CreateSut();
        var request = CreateRequest();

        var actual = sut.ProcessWithProbeJson(request, probeJson);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().Be("REM ffprobe failed: C:\\video\\a.mp4");
    }

    private static (H264TranscodeEngine Sut, IProbeReader ProbeReader) CreateSut()
    {
        var probeReader = Substitute.For<IProbeReader>();
        var sut = new H264TranscodeEngine(
            probeReader,
            new H264RemuxEligibilityPolicy(),
            new H264CommandBuilder());
        return (sut, probeReader);
    }

    private static H264TranscodeRequest CreateRequest(
        string inputPath = "C:\\video\\a.mp4",
        int? downscale = null,
        bool keepFps = false,
        bool denoise = false,
        bool outputMkv = false)
    {
        return new H264TranscodeRequest(
            InputPath: inputPath,
            Downscale: downscale,
            KeepFps: keepFps,
            Denoise: denoise,
            OutputMkv: outputMkv);
    }

    private static ProbeResult CreateProbe(
        string formatName = "mov,mp4,m4a,3gp,3g2,mj2",
        string videoCodec = "h264",
        string? audioCodec = "aac",
        int height = 1080,
        string rFrameRate = "25/1",
        string avgFrameRate = "25/1")
    {
        var streams = new List<ProbeStream>
        {
            new(
                CodecType: "video",
                CodecName: videoCodec,
                Width: 1920,
                Height: height,
                RFrameRate: rFrameRate,
                AvgFrameRate: avgFrameRate)
        };

        if (!string.IsNullOrWhiteSpace(audioCodec))
        {
            streams.Add(new ProbeStream(
                CodecType: "audio",
                CodecName: audioCodec));
        }

        return new ProbeResult(
            Format: new ProbeFormat(
                DurationSeconds: 10.0,
                BitrateBps: 4_000_000,
                FormatName: formatName),
            Streams: streams);
    }
}
