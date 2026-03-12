using FluentAssertions;
using MediaTranscodeEngine.Cli.Processing;
using MediaTranscodeEngine.Cli.Scenarios;
using MediaTranscodeEngine.Cli.Tests.Logging;
using MediaTranscodeEngine.Runtime.Inspection;
using MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Cli.Tests.Processing;

public sealed class PrimaryTranscodeProcessorTests
{
    [Fact]
    public void Process_WhenNonInfoEncodeIsNeeded_ReturnsSingleLegacyCommandLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mkv", container: "mkv", videoCodec: "av1")),
            [new ToMkvGpuFfmpegTool("ffmpeg", CreateLogger<ToMkvGpuFfmpegTool>())],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mkv"));

        actual.Should().StartWith("ffmpeg ");
        actual.Should().Contain(" && del \"C:\\video\\a.mkv\" && ren \"C:\\video\\a_temp.mkv\" \"a.mkv\"");
        actual.Should().NotContain(Environment.NewLine);
    }

    [Fact]
    public void Process_WhenScenarioIsToH264Gpu_ReturnsMp4RemuxCommand()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(new VideoProbeSnapshot(
                container: "mp4",
                streams:
                [
                    new VideoProbeStream(
                        streamType: "video",
                        codec: "h264",
                        width: 1920,
                        height: 1080,
                        framesPerSecond: 29.97,
                        bitrate: 4_000_000,
                        rawFramesPerSecond: 29.97,
                        averageFramesPerSecond: 29.97),
                    new VideoProbeStream(
                        streamType: "audio",
                        codec: "aac",
                        bitrate: 192_000)
                ],
                duration: TimeSpan.FromMinutes(10),
                formatBitrate: 4_500_000,
                formatName: "mov,mp4,m4a,3gp,3g2,mj2")),
            [
                new ToH264GpuFfmpegTool("ffmpeg", CreateLogger<ToH264GpuFfmpegTool>()),
                new ToMkvGpuFfmpegTool("ffmpeg", CreateLogger<ToMkvGpuFfmpegTool>())
            ],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.m4v", "toh264gpu"));

        actual.Should().Contain("-c:v copy");
        actual.Should().Contain("-map 0:a:0? -c:a copy");
        actual.Should().Contain("-movflags +faststart");
        actual.Should().Contain("\"C:\\video\\a.mp4\"");
    }

    [Fact]
    public void Process_WhenFirstToolCannotHandlePlan_UsesNextMatchingTool()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mkv", container: "mkv", videoCodec: "av1")),
            [new RejectingTool(), new StubTool()],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mkv"));

        actual.Should().Be("stub");
    }

    [Fact]
    public void Process_WhenProbeReturnsNoVideoStream_ReturnsLegacyRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("Video probe did not return a video stream.")),
            [new StubTool()],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4"));

        actual.Should().Be("REM Нет видеопотока: a.mp4");
    }

    [Fact]
    public void Process_WhenProbeFails_ReturnsLegacyFfprobeRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("ffprobe returned invalid JSON output.")),
            [new StubTool()],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4"));

        actual.Should().Be("REM ffprobe failed: a.mp4");
    }

    [Fact]
    public void Process_WhenOverlayHasUnknownDimensions_ReturnsLegacyUnknownDimensionsRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("Video probe did not return a valid video width.")),
            [new StubTool()],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4", false, "--overlay-bg"));

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
            [new ToMkvGpuFfmpegTool("ffmpeg", CreateLogger<ToMkvGpuFfmpegTool>())],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4", false, "--overlay-bg"));

        actual.Should().Contain("scale=1920:-1,crop=1920:1080");
        actual.Should().Contain("-map \"[v]\"");
        actual.Should().NotContain("REM Unknown dimensions");
    }

    [Fact]
    public void Process_WhenDownscale720IsRequested_ReturnsCudaScaleCommand()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mp4", container: "mp4", videoCodec: "h264")),
            [new ToMkvGpuFfmpegTool("ffmpeg", CreateLogger<ToMkvGpuFfmpegTool>())],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4", false, "--downscale", "720"));

        actual.Should().StartWith("ffmpeg ");
        actual.Should().Contain("scale_cuda=-2:720");
        actual.Should().NotContain("REM ");
    }

    [Fact]
    public void Process_WhenDownscale576SourceBucketIsMissing_ReturnsLegacyBucketRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("576 source bucket missing: height 900; add SourceBuckets")),
            [new StubTool()],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4", false, "--downscale", "576"));

        actual.Should().Be("REM 576 source bucket missing: height 900; add SourceBuckets");
    }

    [Fact]
    public void Process_WhenDownscale576SourceBucketMatrixIsIncomplete_ReturnsLegacyBucketRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("576 source bucket invalid: missing corridor 'mult/low'")),
            [new StubTool()],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4", false, "--downscale", "576"));

        actual.Should().Be("REM 576 source bucket invalid: missing corridor 'mult/low'");
    }

    [Fact]
    public void Process_WhenDownscale576IsRequestedForZeroHeight_ReturnsLegacyBucketRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(new VideoProbeSnapshot(
                container: "mkv",
                streams:
                [
                    new VideoProbeStream(streamType: "video", codec: "h264", width: 1920, height: 0, framesPerSecond: 25),
                    new VideoProbeStream(streamType: "audio", codec: "aac")
                ],
                duration: TimeSpan.FromMinutes(10))),
            [new StubTool()],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mkv", false, "--downscale", "576"));

        actual.Should().Be("REM 576 source bucket missing: height 0; add SourceBuckets");
    }

    [Fact]
    public void Process_WhenInfoModeDownscale576IsRequestedForZeroHeight_ReturnsInfoBucketMarker()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(new VideoProbeSnapshot(
                container: "mkv",
                streams:
                [
                    new VideoProbeStream(streamType: "video", codec: "h264", width: 1920, height: 0, framesPerSecond: 25),
                    new VideoProbeStream(streamType: "audio", codec: "aac")
                ],
                duration: TimeSpan.FromMinutes(10))),
            [new StubTool()],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mkv", true, "--downscale", "576"));

        actual.Should().Be("a.mkv: [576 source bucket missing: height 0; add SourceBuckets]");
    }

    [Fact]
    public void Process_WhenInfoModeProbeFails_ReturnsInfoMarker()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("ffprobe returned invalid JSON output.")),
            [new StubTool()],
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4", true));

        actual.Should().Be("a.mp4: [ffprobe failed]");
    }

    [Fact]
    public void Process_WhenToolThrowsUnexpectedException_ReturnsLegacyUnexpectedRemLineAndLogsWarning()
    {
        using var provider = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mp4", container: "mp4", videoCodec: "h264")),
            [new ThrowingTool(new InvalidOperationException("Unexpected tool failure."))],
            CreateScenarioRegistry(),
            loggerFactory.CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4"));

        actual.Should().Be("REM Unexpected failure: a.mp4");
        var warningEntry = provider.Entries.Single(entry => entry.Level == LogLevel.Warning &&
                                                            entry.Message.Contains("Processing returned failure marker.", StringComparison.Ordinal) &&
                                                            Equals(entry.Properties["FailureKind"], "unexpected_failure"));
        warningEntry.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Process_WhenInspectorThrowsIOException_ReturnsLegacyIoRemLineAndLogsError()
    {
        using var provider = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new IOException("Disk read failed.")),
            [new StubTool()],
            CreateScenarioRegistry(),
            loggerFactory.CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4"));

        actual.Should().Be("REM I/O error: a.mp4");
        var errorEntry = provider.Entries.Single(entry => entry.Level == LogLevel.Error &&
                                                          entry.Message.Contains("Processing returned failure marker.", StringComparison.Ordinal) &&
                                                          Equals(entry.Properties["FailureKind"], "io_error"));
        errorEntry.Exception.Should().BeOfType<IOException>();
    }

    [Fact]
    public void Process_WhenNonInfoEncodeIsNeeded_LogsInspectionPlanAndExecution()
    {
        using var provider = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mkv", container: "mkv", videoCodec: "av1")),
            [new ToMkvGpuFfmpegTool("ffmpeg", loggerFactory.CreateLogger<ToMkvGpuFfmpegTool>())],
            CreateScenarioRegistry(),
            loggerFactory.CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mkv"));

        actual.Should().StartWith("ffmpeg ");
        provider.Entries.Should().Contain(entry => entry.Level == LogLevel.Information &&
                                                  entry.Message.Contains("Processing started.", StringComparison.Ordinal));
        provider.Entries.Should().Contain(entry => entry.Level == LogLevel.Information &&
                                                  entry.Message.Contains("Video inspected.", StringComparison.Ordinal) &&
                                                  Equals(entry.Properties["Container"], "mkv") &&
                                                  Equals(entry.Properties["VideoCodec"], "av1"));
        provider.Entries.Should().Contain(entry => entry.Level == LogLevel.Information &&
                                                  entry.Message.Contains("Transcode plan built.", StringComparison.Ordinal) &&
                                                  Equals(entry.Properties["TargetContainer"], "mkv"));
        provider.Entries.Should().Contain(entry => entry.Level == LogLevel.Information &&
                                                  entry.Message.Contains("Tool execution built.", StringComparison.Ordinal) &&
                                                  Equals(entry.Properties["ToolName"], "ffmpeg") &&
                                                  Equals(entry.Properties["IsEmpty"], false));
    }

    [Fact]
    public void Process_WhenFailureIsHandled_LogsWarningWithFailureKind()
    {
        using var provider = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(new InvalidOperationException("Video probe did not return a video stream.")),
            [new StubTool()],
            CreateScenarioRegistry(),
            loggerFactory.CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4"));

        actual.Should().Be("REM Нет видеопотока: a.mp4");
        var warningEntry = provider.Entries.Single(entry => entry.Level == LogLevel.Warning &&
                                                            entry.Message.Contains("Processing returned failure marker.", StringComparison.Ordinal) &&
                                                            Equals(entry.Properties["FailureKind"], "no_video_stream"));
        warningEntry.Exception.Should().BeOfType<InvalidOperationException>();
    }

    private static ILogger<T> CreateLogger<T>()
    {
        return LoggerFactory.Create(static _ => { }).CreateLogger<T>();
    }

    private static CliTranscodeRequest CreateRequest(string inputPath, bool info = false, params string[] scenarioArgs)
    {
        return CreateRequest(inputPath, "tomkvgpu", info, scenarioArgs);
    }

    private static CliTranscodeRequest CreateRequest(string inputPath, string scenarioName, bool info = false, params string[] scenarioArgs)
    {
        return new CliTranscodeRequest(inputPath, scenarioName, info, scenarioArgs);
    }

    private static CliScenarioRegistry CreateScenarioRegistry()
    {
        return new CliScenarioRegistry(
            [
                new ToH264GpuCliScenarioHandler(new ToH264GpuInfoFormatter()),
                new ToMkvGpuCliScenarioHandler(new ToMkvGpuInfoFormatter())
            ]);
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

    private sealed class ThrowingTool : ITranscodeTool
    {
        private readonly Exception _exception;

        public ThrowingTool(Exception exception)
        {
            _exception = exception;
        }

        public string Name => "throwing";

        public bool CanHandle(Runtime.Plans.TranscodePlan plan)
        {
            return true;
        }

        public ToolExecution BuildExecution(SourceVideo video, Runtime.Plans.TranscodePlan plan)
        {
            throw _exception;
        }
    }

    private sealed class RejectingTool : ITranscodeTool
    {
        public string Name => "rejecting";

        public bool CanHandle(Runtime.Plans.TranscodePlan plan)
        {
            return false;
        }

        public ToolExecution BuildExecution(SourceVideo video, Runtime.Plans.TranscodePlan plan)
        {
            throw new InvalidOperationException("Rejecting tool must not be called.");
        }
    }
}
