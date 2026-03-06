using FluentAssertions;
using MediaTranscodeEngine.Cli.Processing;
using MediaTranscodeEngine.Runtime.Downscaling;
using MediaTranscodeEngine.Runtime.Inspection;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Tools.Ffmpeg;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Cli.Tests.Processing;

public sealed class PrimaryTranscodeProcessorTests
{
    [Fact]
    public void Process_WhenNonInfoEncodeIsNeeded_ReturnsSingleLegacyCommandLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mkv", container: "mkv", videoCodec: "av1")),
            new FfmpegTool("ffmpeg"),
            new ToMkvGpuInfoFormatter());

        var actual = sut.Process(new CliTranscodeRequest(
            InputPath: @"C:\video\a.mkv",
            ScenarioName: "tomkvgpu",
            Info: false,
            ToMkvGpu: new ToMkvGpuRequest()));

        actual.Should().StartWith("ffmpeg ");
        actual.Should().Contain(" && del \"C:\\video\\a.mkv\" && ren \"C:\\video\\a_temp.mkv\" \"a.mkv\"");
        actual.Should().NotContain(Environment.NewLine);
    }

    [Fact]
    public void Process_WhenProbeReturnsNoVideoStream_ReturnsLegacyRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("Video probe did not return a video stream.")),
            new StubTool(),
            new ToMkvGpuInfoFormatter());

        var actual = sut.Process(new CliTranscodeRequest(
            InputPath: @"C:\video\a.mp4",
            ScenarioName: "tomkvgpu",
            Info: false,
            ToMkvGpu: new ToMkvGpuRequest()));

        actual.Should().Be("REM Нет видеопотока: a.mp4");
    }

    [Fact]
    public void Process_WhenProbeFails_ReturnsLegacyFfprobeRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("ffprobe returned invalid JSON output.")),
            new StubTool(),
            new ToMkvGpuInfoFormatter());

        var actual = sut.Process(new CliTranscodeRequest(
            InputPath: @"C:\video\a.mp4",
            ScenarioName: "tomkvgpu",
            Info: false,
            ToMkvGpu: new ToMkvGpuRequest()));

        actual.Should().Be("REM ffprobe failed: a.mp4");
    }

    [Fact]
    public void Process_WhenOverlayHasUnknownDimensions_ReturnsLegacyUnknownDimensionsRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("Video probe did not return a valid video width.")),
            new StubTool(),
            new ToMkvGpuInfoFormatter());

        var actual = sut.Process(new CliTranscodeRequest(
            InputPath: @"C:\video\a.mp4",
            ScenarioName: "tomkvgpu",
            Info: false,
            ToMkvGpu: new ToMkvGpuRequest(overlayBackground: true)));

        actual.Should().Be("REM Unknown dimensions: a.mp4");
    }

    [Fact]
    public void Process_WhenOverlayHasNonPositiveDimensions_Uses1920x1080FallbackCommand()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(new VideoProbeSnapshot(
                container: "mp4",
                streams:
                [
                    new VideoProbeStream(streamType: "video", codec: "av1", width: 0, height: 0, framesPerSecond: 25),
                    new VideoProbeStream(streamType: "audio", codec: "aac")
                ],
                duration: TimeSpan.FromMinutes(10))),
            new FfmpegTool("ffmpeg"),
            new ToMkvGpuInfoFormatter());

        var actual = sut.Process(new CliTranscodeRequest(
            InputPath: @"C:\video\a.mp4",
            ScenarioName: "tomkvgpu",
            Info: false,
            ToMkvGpu: new ToMkvGpuRequest(overlayBackground: true)));

        actual.Should().Contain("scale=1920:-1,crop=1920:1080");
        actual.Should().Contain("-map \"[v]\"");
        actual.Should().NotContain("REM Unknown dimensions");
    }

    [Fact]
    public void Process_WhenDownscale720IsRequested_ReturnsLegacyDownscaleRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mp4", container: "mp4", videoCodec: "h264")),
            new StubTool(),
            new ToMkvGpuInfoFormatter());

        var actual = sut.Process(new CliTranscodeRequest(
            InputPath: @"C:\video\a.mp4",
            ScenarioName: "tomkvgpu",
            Info: false,
            ToMkvGpu: new ToMkvGpuRequest(downscale: new DownscaleRequest(targetHeight: 720))));

        actual.Should().Be("REM Downscale 720 not implemented: a.mp4");
    }

    [Fact]
    public void Process_WhenDownscale576SourceBucketIsMissing_ReturnsLegacyBucketRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("576 source bucket missing: height 900; add SourceBuckets")),
            new StubTool(),
            new ToMkvGpuInfoFormatter());

        var actual = sut.Process(new CliTranscodeRequest(
            InputPath: @"C:\video\a.mp4",
            ScenarioName: "tomkvgpu",
            Info: false,
            ToMkvGpu: new ToMkvGpuRequest(downscale: new DownscaleRequest(targetHeight: 576))));

        actual.Should().Be("REM 576 source bucket missing: height 900; add SourceBuckets");
    }

    [Fact]
    public void Process_WhenDownscale576SourceBucketMatrixIsIncomplete_ReturnsLegacyBucketRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("576 source bucket invalid: missing corridor 'mult/low'")),
            new StubTool(),
            new ToMkvGpuInfoFormatter());

        var actual = sut.Process(new CliTranscodeRequest(
            InputPath: @"C:\video\a.mp4",
            ScenarioName: "tomkvgpu",
            Info: false,
            ToMkvGpu: new ToMkvGpuRequest(downscale: new DownscaleRequest(targetHeight: 576))));

        actual.Should().Be("REM 576 source bucket invalid: missing corridor 'mult/low'");
    }

    [Fact]
    public void Process_WhenInfoModeProbeFails_ReturnsInfoMarker()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("ffprobe returned invalid JSON output.")),
            new StubTool(),
            new ToMkvGpuInfoFormatter());

        var actual = sut.Process(new CliTranscodeRequest(
            InputPath: @"C:\video\a.mp4",
            ScenarioName: "tomkvgpu",
            Info: true,
            ToMkvGpu: new ToMkvGpuRequest()));

        actual.Should().Be("a.mp4: [ffprobe failed]");
    }

    private static SourceVideo CreateVideo(
        string filePath,
        string container,
        string videoCodec,
        IReadOnlyList<string>? audioCodecs = null)
    {
        return new SourceVideo(
            filePath: filePath,
            container: container,
            videoCodec: videoCodec,
            audioCodecs: audioCodecs ?? ["aac"],
            width: 1920,
            height: 1080,
            framesPerSecond: 25,
            duration: TimeSpan.FromMinutes(10));
    }

    private static VideoInspector CreateInspector(SourceVideo video)
    {
        return new VideoInspector(new StubVideoProbe(video));
    }

    private static VideoInspector CreateInspector(VideoProbeSnapshot snapshot)
    {
        return new VideoInspector(new SnapshotVideoProbe(snapshot));
    }

    private static VideoInspector CreateThrowingInspector(Exception exception)
    {
        return new VideoInspector(new ThrowingVideoProbe(exception));
    }

    private sealed class StubVideoProbe : IVideoProbe
    {
        private readonly SourceVideo _video;

        public StubVideoProbe(SourceVideo video)
        {
            _video = video;
        }

        public VideoProbeSnapshot Probe(string filePath)
        {
            return new VideoProbeSnapshot(
                container: _video.Container,
                streams:
                [
                    new VideoProbeStream("video", _video.VideoCodec, _video.Width, _video.Height, _video.FramesPerSecond),
                    .. _video.AudioCodecs.Select(codec => new VideoProbeStream("audio", codec))
                ],
                duration: _video.Duration);
        }
    }

    private sealed class ThrowingVideoProbe : IVideoProbe
    {
        private readonly Exception _exception;

        public ThrowingVideoProbe(Exception exception)
        {
            _exception = exception;
        }

        public VideoProbeSnapshot Probe(string filePath)
        {
            throw _exception;
        }
    }

    private sealed class SnapshotVideoProbe : IVideoProbe
    {
        private readonly VideoProbeSnapshot _snapshot;

        public SnapshotVideoProbe(VideoProbeSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public VideoProbeSnapshot Probe(string filePath)
        {
            return _snapshot;
        }
    }

    private sealed class StubTool : ITranscodeTool
    {
        public string Name => "stub";

        public bool CanHandle(Runtime.Plans.TranscodePlan plan)
        {
            return true;
        }

        public ToolExecution BuildExecution(SourceVideo video, Runtime.Plans.TranscodePlan plan)
        {
            return ToolExecution.Single("stub", "stub");
        }
    }
}
