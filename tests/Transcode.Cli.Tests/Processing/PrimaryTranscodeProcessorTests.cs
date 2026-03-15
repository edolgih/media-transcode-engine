using FluentAssertions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Parsing;
using Transcode.Cli.Core.Processing;
using Transcode.Cli.Core.Scenarios;
using Transcode.Cli.Tests.Logging;
using Transcode.Runtime.Failures;
using Transcode.Runtime.Inspection;
using Transcode.Runtime.Scenarios;
using Transcode.Runtime.Videos;
using Transcode.Scenarios.ToH264Gpu.Cli;
using Transcode.Scenarios.ToH264Gpu.Runtime;
using Transcode.Scenarios.ToMkvGpu.Cli;
using Transcode.Scenarios.ToMkvGpu.Runtime;

namespace Transcode.Cli.Tests.Processing;

/*
Это тесты главного CLI processor-а.
Они покрывают orchestration между parsing, inspection, scenario selection и scenario execution assembly.
*/
/// <summary>
/// Verifies the primary CLI processor that orchestrates parsing, inspection, and scenario execution.
/// </summary>
public sealed class PrimaryTranscodeProcessorTests
{
    [Fact]
    public void Process_WhenNonInfoEncodeIsNeeded_ReturnsSingleLegacyCommandLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mkv", container: "mkv", videoCodec: "av1")),
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
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.m4v", "toh264gpu"));

        actual.Should().Contain("-c:v copy");
        actual.Should().Contain("-map 0:a:0? -c:a copy");
        actual.Should().Contain("-movflags +faststart");
        actual.Should().Contain("\"C:\\video\\a.mp4\"");
    }

    [Fact]
    public void Process_WhenProbeReturnsNoVideoStream_ReturnsLegacyRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(RuntimeFailures.NoVideoStream()),
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4"));

        actual.Should().Be("REM Нет видеопотока: a.mp4");
    }

    [Fact]
    public void Process_WhenProbeFails_ReturnsLegacyFfprobeRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(RuntimeFailures.ProbeInvalidJson(new JsonException())),
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4"));

        actual.Should().Be("REM ffprobe failed: a.mp4");
    }

    [Fact]
    public void Process_WhenProbeJsonIsMissingRequiredField_ReturnsLegacyFfprobeRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(RuntimeFailures.ProbeMissingRequiredField("codec_name", "stream")),
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4"));

        actual.Should().Be("REM ffprobe failed: a.mp4");
    }

    [Fact]
    public void Process_WhenOverlayHasUnknownDimensions_ReturnsLegacyUnknownDimensionsRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(RuntimeFailures.InvalidVideoWidth()),
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
            CreateThrowingInspector(RuntimeFailures.DownscaleSourceBucketIssue("576 source bucket missing: height 900; add SourceBuckets")),
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4", false, "--downscale", "576"));

        actual.Should().Be("REM 576 source bucket missing: height 900; add SourceBuckets");
    }

    [Fact]
    public void Process_WhenDownscale576SourceBucketMatrixIsIncomplete_ReturnsLegacyBucketRemLine()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(RuntimeFailures.DownscaleSourceBucketIssue("576 source bucket invalid: missing corridor 'mult/low'")),
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
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mkv", true, "--downscale", "576"));

        actual.Should().Be("a.mkv: [576 source bucket missing: height 0; add SourceBuckets]");
    }

    [Fact]
    public void Process_WhenInfoModeProbeFails_ReturnsInfoMarker()
    {
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(RuntimeFailures.ProbeInvalidJson(new JsonException())),
            CreateScenarioRegistry(),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4", true));

        actual.Should().Be("a.mp4: [ffprobe failed]");
    }

    [Fact]
    public void Process_WhenInfoModeIsRequested_DoesNotBuildExecution()
    {
        var scenarioHandler = new InfoOnlyScenarioHandler();
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mp4", container: "mp4", videoCodec: "h264")),
            new CliScenarioRegistry([scenarioHandler]),
            CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4", "info-only", true));

        actual.Should().Be("info-only");
        scenarioHandler.CreatedScenario.Should().NotBeNull();
        scenarioHandler.CreatedScenario!.BuildExecutionCallCount.Should().Be(0);
    }

    [Fact]
    public void Process_WhenScenarioExecutionThrowsUnexpectedException_ReturnsLegacyUnexpectedRemLineAndLogsWarning()
    {
        using var provider = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var scenarioHandler = new ThrowingExecutionScenarioHandler(new InvalidOperationException("Unexpected execution failure."));
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mp4", container: "mp4", videoCodec: "h264")),
            new CliScenarioRegistry([scenarioHandler]),
            loggerFactory.CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4", "throwing-execution"));

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
    public void Process_WhenNonInfoEncodeIsNeeded_LogsInspectionAndScenarioExecution()
    {
        using var provider = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var sut = new PrimaryTranscodeProcessor(
            CreateInspector(CreateVideo(filePath: @"C:\video\a.mkv", container: "mkv", videoCodec: "av1")),
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
                                                  entry.Message.Contains("Scenario execution built.", StringComparison.Ordinal) &&
                                                  Equals(entry.Properties["Scenario"], "tomkvgpu") &&
                                                  Equals(entry.Properties["IsEmpty"], false));
    }

    [Fact]
    public void Process_WhenFailureIsHandled_LogsWarningWithFailureKind()
    {
        using var provider = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var sut = new PrimaryTranscodeProcessor(
            CreateThrowingInspector(RuntimeFailures.NoVideoStream()),
            CreateScenarioRegistry(),
            loggerFactory.CreateLogger<PrimaryTranscodeProcessor>());

        var actual = sut.Process(CreateRequest(@"C:\video\a.mp4"));

        actual.Should().Be("REM Нет видеопотока: a.mp4");
        var warningEntry = provider.Entries.Single(entry => entry.Level == LogLevel.Warning &&
                                                            entry.Message.Contains("Processing returned failure marker.", StringComparison.Ordinal) &&
                                                            Equals(entry.Properties["FailureKind"], "no_video_stream"));
        warningEntry.Exception.Should().BeOfType<RuntimeFailureException>();
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
        var handler = ResolveHandler(scenarioName);
        var scenarioInput = new object();
        if (handler is not null)
        {
            handler.TryParse(scenarioArgs, out scenarioInput, out var errorText).Should().BeTrue(errorText);
        }

        return new CliTranscodeRequest(inputPath, scenarioName, info, scenarioInput, scenarioArgs.Length);
    }

    private static CliScenarioRegistry CreateScenarioRegistry()
    {
        return new CliScenarioRegistry(
            [
                new ToH264GpuCliScenarioHandler(new ToH264GpuInfoFormatter()),
                new ToMkvGpuCliScenarioHandler(new ToMkvGpuInfoFormatter())
            ]);
    }

    private static ICliScenarioHandler? ResolveHandler(string scenarioName)
    {
        return scenarioName switch
        {
            "toh264gpu" => new ToH264GpuCliScenarioHandler(new ToH264GpuInfoFormatter()),
            "tomkvgpu" => new ToMkvGpuCliScenarioHandler(new ToMkvGpuInfoFormatter()),
            _ => null
        };
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

    /*
    Это test double для video probe, который строит snapshot из заранее подготовленного SourceVideo.
    Он нужен, чтобы тестировать processor без реального ffprobe.
    */
    /// <summary>
    /// Supplies a deterministic probe snapshot derived from a prepared source video.
    /// </summary>
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

    /*
    Это test double для probe, который всегда бросает исключение.
    Через него тесты проверяют error-path поведение processor-а.
    */
    /// <summary>
    /// Throws a predefined exception when probing is requested.
    /// </summary>
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

    /*
    Это probe-заглушка, возвращающая заранее подготовленный snapshot как есть.
    Она позволяет изолировать тесты от логики преобразования SourceVideo в probe-данные.
    */
    /// <summary>
    /// Returns a prepared probe snapshot without additional transformation.
    /// </summary>
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

    private sealed class InfoOnlyScenarioHandler : ICliScenarioHandler
    {
        public string Name => "info-only";

        public IReadOnlyList<string> LegacyCommandTokens { get; } = [];

        public IReadOnlyList<CliHelpOption> HelpOptions { get; } = [];

        public InfoOnlyScenario? CreatedScenario { get; private set; }

        public IReadOnlyList<string> GetHelpExamples(string exeName)
        {
            return [];
        }

        public bool TryParse(IReadOnlyList<string> args, out object scenarioInput, out string? errorText)
        {
            scenarioInput = new object();
            errorText = null;
            return true;
        }

        public TranscodeScenario CreateScenario(CliTranscodeRequest request)
        {
            CreatedScenario = new InfoOnlyScenario();
            return CreatedScenario;
        }

        public CliScenarioFailure DescribeFailure(CliTranscodeRequest request, Exception exception)
        {
            throw new InvalidOperationException("Failure path is not expected in this test.");
        }
    }

    private sealed class InfoOnlyScenario : TranscodeScenario
    {
        public InfoOnlyScenario()
            : base("info-only")
        {
        }

        public int BuildExecutionCallCount { get; private set; }

        protected override string FormatInfoCore(SourceVideo video)
        {
            return "info-only";
        }

        protected override ScenarioExecution BuildExecutionCore(SourceVideo video)
        {
            BuildExecutionCallCount++;
            throw new InvalidOperationException("Info mode must not build execution.");
        }
    }

    private sealed class ThrowingExecutionScenarioHandler : ICliScenarioHandler
    {
        private readonly Exception _exception;

        public ThrowingExecutionScenarioHandler(Exception exception)
        {
            _exception = exception;
        }

        public string Name => "throwing-execution";

        public IReadOnlyList<string> LegacyCommandTokens { get; } = [];

        public IReadOnlyList<CliHelpOption> HelpOptions { get; } = [];

        public IReadOnlyList<string> GetHelpExamples(string exeName)
        {
            return [];
        }

        public bool TryParse(IReadOnlyList<string> args, out object scenarioInput, out string? errorText)
        {
            scenarioInput = new object();
            errorText = null;
            return true;
        }

        public TranscodeScenario CreateScenario(CliTranscodeRequest request)
        {
            return new ThrowingExecutionScenario(_exception);
        }

        public CliScenarioFailure DescribeFailure(CliTranscodeRequest request, Exception exception)
        {
            return new CliScenarioFailure(
                LogLevel.Warning,
                "unexpected_failure",
                $"REM Unexpected failure: {Path.GetFileName(request.InputPath)}",
                $"{Path.GetFileName(request.InputPath)}: [unexpected failure]");
        }
    }

    private sealed class ThrowingExecutionScenario : TranscodeScenario
    {
        private readonly Exception _exception;

        public ThrowingExecutionScenario(Exception exception)
            : base("throwing-execution")
        {
            _exception = exception;
        }

        protected override ScenarioExecution BuildExecutionCore(SourceVideo video)
        {
            throw _exception;
        }
    }
}
