using FluentAssertions;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Engine.Behaviors;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Policy;
using NSubstitute;

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class UnifiedTranscodeEngineTests
{
    [Fact]
    public void Process_WhenResolvedCodecIsCopy_BuildsRemuxCommand()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe());
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\a.mp4",
            TargetContainer: RequestContracts.Unified.MkvContainer,
            ComputeMode: RequestContracts.Unified.GpuComputeMode);

        var actual = sut.Process(request);

        actual.Should().Contain("-c:v copy");
        actual.Should().Contain("\"C:\\video\\a.mkv\"");
    }

    [Fact]
    public void Process_WhenResolvedCodecIsH264AndComputeGpu_BuildsNvencCommand()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe());
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\a.mkv",
            ComputeMode: RequestContracts.Unified.GpuComputeMode,
            PreferH264: true);

        var actual = sut.Process(request);

        actual.Should().Contain("-c:v h264_nvenc");
    }

    [Fact]
    public void ProcessWithProbeResult_WhenResolvedCodecIsCopy_BuildsRemuxCommandAndSkipsProbeReader()
    {
        var (sut, probeReader) = CreateSut();
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\a.mp4",
            TargetContainer: RequestContracts.Unified.MkvContainer,
            ComputeMode: RequestContracts.Unified.GpuComputeMode);
        var probe = CreateProbe();

        var actual = sut.ProcessWithProbeResult(request, probe);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().Contain("-c:v copy");
    }

    [Fact]
    public void ProcessWithProbeJson_WhenResolvedCodecIsCopy_BuildsRemuxCommandAndSkipsProbeReader()
    {
        var (sut, probeReader) = CreateSut();
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\a.mp4",
            TargetContainer: RequestContracts.Unified.MkvContainer,
            ComputeMode: RequestContracts.Unified.GpuComputeMode);
        var probeJson = CreateProbeJson();

        var actual = sut.ProcessWithProbeJson(request, probeJson);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().Contain("-c:v copy");
    }

    [Fact]
    public void ProcessWithProbeResult_WhenResolvedCodecIsH264AndComputeGpu_BuildsNvencCommand()
    {
        var (sut, probeReader) = CreateSut();
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\a.mkv",
            ComputeMode: RequestContracts.Unified.GpuComputeMode,
            PreferH264: true);
        var probe = CreateProbe();

        var actual = sut.ProcessWithProbeResult(request, probe);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().Contain("-c:v h264_nvenc");
    }

    [Fact]
    public void ProcessWithProbeJson_WhenResolvedCodecIsH264AndComputeGpu_BuildsNvencCommand()
    {
        var (sut, probeReader) = CreateSut();
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\a.mkv",
            ComputeMode: RequestContracts.Unified.GpuComputeMode,
            PreferH264: true);
        var probeJson = CreateProbeJson();

        var actual = sut.ProcessWithProbeJson(request, probeJson);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().Contain("-c:v h264_nvenc");
    }

    [Fact]
    public void ProcessWithProbeResult_WhenResolvedCodecIsH264AndComputeCpu_ReturnsNotImplemented()
    {
        var (sut, probeReader) = CreateSut();
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\a.mp4",
            ComputeMode: RequestContracts.Unified.CpuComputeMode);
        var probe = CreateProbe();

        var actual = sut.ProcessWithProbeResult(request, probe);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().Be("REM h264 cpu not implemented: C:\\video\\a.mp4");
    }

    [Fact]
    public void ProcessWithProbeJson_WhenResolvedCodecIsH264AndComputeCpu_ReturnsNotImplemented()
    {
        var (sut, probeReader) = CreateSut();
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\a.mp4",
            ComputeMode: RequestContracts.Unified.CpuComputeMode);
        var probeJson = CreateProbeJson();

        var actual = sut.ProcessWithProbeJson(request, probeJson);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().Be("REM h264 cpu not implemented: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenResolvedCodecIsH264AndComputeCpu_ReturnsNotImplemented()
    {
        var (sut, _) = CreateSut();
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\a.mp4",
            ComputeMode: RequestContracts.Unified.CpuComputeMode);

        var actual = sut.Process(request);

        actual.Should().Be("REM h264 cpu not implemented: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenProbeFails_ReturnsRemProbeFailed()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns((ProbeResult?)null);
        var request = UnifiedTranscodeRequest.Create(InputPath: "C:\\video\\a.mp4");

        var actual = sut.Process(request);

        actual.Should().Be("REM ffprobe failed: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenNoVideoStream_ReturnsRemNoVideoStream()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(new ProbeResult(
            Format: null,
            Streams: [new ProbeStream("audio", "aac")]));
        var request = UnifiedTranscodeRequest.Create(InputPath: "C:\\video\\a.mp4");

        var actual = sut.Process(request);

        actual.Should().Be("REM Нет видеопотока: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenKeepSourceTrue_AppendsExpectedSuffixes()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(height: 1080));
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\a.mp4",
            KeepSource: true,
            PreferH264: true,
            Downscale: 576);

        var actual = sut.Process(request);

        actual.Should().Contain("\"C:\\video\\a_576p_h264.mkv\"");
    }

    private static (UnifiedTranscodeEngine Sut, IProbeReader ProbeReader) CreateSut()
    {
        var probeReader = Substitute.For<IProbeReader>();
        var transcodeEngine = new TranscodeEngine(
            probeReader: probeReader,
            profileRepository: new StaticProfileRepository(),
            policy: new TranscodePolicy(),
            commandBuilder: new FfmpegCommandBuilder());
        var h264Engine = new H264TranscodeEngine(
            probeReader: probeReader,
            remuxEligibilityPolicy: new H264RemuxEligibilityPolicy(),
            timestampPolicy: new H264TimestampPolicy(),
            audioPolicy: new H264AudioPolicy(),
            rateControlPolicy: new H264RateControlPolicy(),
            containerPolicySelector: new ContainerPolicySelector(
            [
                new MkvContainerPolicy(),
                new Mp4ContainerPolicy()
            ]),
            commandBuilder: new H264CommandBuilder());
        var behaviors = new ITranscodeBehavior[]
        {
            new CopyTranscodeBehavior(transcodeEngine),
            new H264GpuTranscodeBehavior(h264Engine),
            new H264CpuNotImplementedBehavior()
        };
        var sut = new UnifiedTranscodeEngine(
            codecResolver: new TargetVideoCodecResolver(),
            behaviorSelector: new TranscodeBehaviorSelector(behaviors));

        return (sut, probeReader);
    }

    private static ProbeResult CreateProbe(
        string videoCodec = "h264",
        int height = 1080,
        string audioCodec = "aac",
        string formatName = "mov,mp4,m4a,3gp,3g2,mj2")
    {
        return new ProbeResult(
            Format: new ProbeFormat(DurationSeconds: 600, BitrateBps: 6_000_000, FormatName: formatName),
            Streams:
            [
                new ProbeStream("video", videoCodec, Width: 1920, Height: height, RFrameRate: "30000/1001", AvgFrameRate: "30000/1001"),
                new ProbeStream("audio", audioCodec)
            ]);
    }

    private static string CreateProbeJson()
    {
        return """
            {
              "format": { "format_name": "mov,mp4,m4a,3gp,3g2,mj2", "duration": "10.0" },
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "r_frame_rate": "30000/1001", "avg_frame_rate": "30000/1001", "width": 1920, "height": 1080 },
                { "codec_type": "audio", "codec_name": "aac" }
              ]
            }
            """;
    }
}
