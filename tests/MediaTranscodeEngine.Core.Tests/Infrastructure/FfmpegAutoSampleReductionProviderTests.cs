using System.Text.RegularExpressions;
using FluentAssertions;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace MediaTranscodeEngine.Core.Tests.Infrastructure;

public class FfmpegAutoSampleReductionProviderTests
{
    [Fact]
    public void Ctor_WhenNvencPresetIsUnsupported_ThrowsArgumentException()
    {
        var probeReader = Substitute.For<IProbeReader>();
        var processRunner = Substitute.For<IProcessRunner>();

        var action = () => new FfmpegAutoSampleReductionProvider(
            probeReader,
            processRunner,
            nvencPreset: "p9");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*nvencPreset must be p1..p7.*");
    }

    [Fact]
    public void EstimateFast_WhenFormatBitrateIsAvailable_ReturnsExpectedReduction()
    {
        var inputPath = "C:\\video\\movie.mkv";
        var probeReader = Substitute.For<IProbeReader>();
        probeReader.Read(inputPath).Returns(new ProbeResult(
            Format: new ProbeFormat(DurationSeconds: 3600, BitrateBps: 4_000_000),
            Streams: []));
        var processRunner = Substitute.For<IProcessRunner>();
        var sut = CreateSut(probeReader, processRunner);

        var actual = sut.EstimateFast(new AutoSampleReductionInput(
            InputPath: inputPath,
            Cq: 23,
            Maxrate: 2.0,
            Bufsize: 4.0));

        actual.Should().NotBeNull();
        actual!.Value.Should().BeApproximately(50.0, 0.001);
        processRunner.DidNotReceiveWithAnyArgs().Run(default!, default!, default);
    }

    [Fact]
    public void EstimateFast_WhenFormatBitrateIsMissing_FallsBackToFileSizeAndDuration()
    {
        var inputPath = CreateTempFileWithLength(9_000_000);
        try
        {
            var probeReader = Substitute.For<IProbeReader>();
            probeReader.Read(inputPath).Returns(new ProbeResult(
                Format: new ProbeFormat(DurationSeconds: 36, BitrateBps: null),
                Streams: []));
            var processRunner = Substitute.For<IProcessRunner>();
            var sut = CreateSut(probeReader, processRunner);

            var actual = sut.EstimateFast(new AutoSampleReductionInput(
                InputPath: inputPath,
                Cq: 23,
                Maxrate: 2.0,
                Bufsize: 4.0));

            actual.Should().NotBeNull();
            actual!.Value.Should().BeApproximately(0.0, 0.001);
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    [Fact]
    public void EstimateAccurate_WhenDurationIsLong_UsesThreeSampleWindowsAndExpectedReduction()
    {
        var inputPath = CreateTempFileWithLength(120_000_000);
        try
        {
            var probeReader = Substitute.For<IProbeReader>();
            probeReader.Read(inputPath).Returns(new ProbeResult(
                Format: new ProbeFormat(DurationSeconds: 7200, BitrateBps: 6_000_000),
                Streams: [new ProbeStream("video", "h264", Width: 1920, Height: 1080)]));

            var processRunner = new ScriptedProcessRunner(
                encodedSampleSizesBytes: [500_000, 500_000, 500_000]);
            var sut = CreateSut(probeReader, processRunner);

            var actual = sut.EstimateAccurate(new AutoSampleReductionInput(
                InputPath: inputPath,
                Cq: 24,
                Maxrate: 2.4,
                Bufsize: 4.8));

            actual.Should().NotBeNull();
            actual!.Value.Should().BeApproximately(50.0, 0.01);
            processRunner.Calls.Count.Should().Be(3);
            processRunner.Calls[0].Should().Contain("-cq 24");
            processRunner.Calls[0].Should().Contain("-maxrate 2.4M");
            processRunner.Calls[0].Should().Contain("-bufsize 4.8M");
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    [Fact]
    public void EstimateAccurate_WhenDefaultSampleDurationIsUsed_Uses15SecondWindow()
    {
        var inputPath = CreateTempFileWithLength(24_000_000);
        try
        {
            var probeReader = Substitute.For<IProbeReader>();
            probeReader.Read(inputPath).Returns(new ProbeResult(
                Format: new ProbeFormat(DurationSeconds: 1_200, BitrateBps: 4_000_000),
                Streams: [new ProbeStream("video", "h264", Width: 1280, Height: 720)]));

            var processRunner = new ScriptedProcessRunner(encodedSampleSizesBytes: [150_000]);
            var sut = new FfmpegAutoSampleReductionProvider(
                probeReader,
                processRunner,
                ffmpegPath: "ffmpeg",
                timeoutMs: 30_000,
                sampleEncodeInactivityTimeoutMs: 12_000,
                nvencPreset: "p6");

            var actual = sut.EstimateAccurate(new AutoSampleReductionInput(
                InputPath: inputPath,
                Cq: 23,
                Maxrate: 2.0,
                Bufsize: 4.0));

            actual.Should().NotBeNull();
            actual!.Value.Should().BeApproximately(50.0, 0.01);
            processRunner.Calls.Should().ContainSingle();
            processRunner.Calls[0].Should().Contain("-t 15 ");
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    [Theory]
    [InlineData(1_200, 1)]
    [InlineData(2_400, 2)]
    [InlineData(6_000, 3)]
    public void EstimateAccurate_WhenDurationChanges_UsesExpectedSampleWindowCount(
        double durationSeconds,
        int expectedWindowCount)
    {
        var inputPath = CreateTempFileWithLength(24_000_000);
        try
        {
            var probeReader = Substitute.For<IProbeReader>();
            probeReader.Read(inputPath).Returns(new ProbeResult(
                Format: new ProbeFormat(DurationSeconds: durationSeconds, BitrateBps: 4_000_000),
                Streams: [new ProbeStream("video", "h264", Width: 1920, Height: 1080)]));

            var sampleSizes = Enumerable.Repeat(300_000L, expectedWindowCount).ToArray();
            var processRunner = new ScriptedProcessRunner(sampleSizes);
            var sut = CreateSut(probeReader, processRunner);

            var actual = sut.EstimateAccurate(new AutoSampleReductionInput(
                InputPath: inputPath,
                Cq: 23,
                Maxrate: 2.0,
                Bufsize: 4.0));

            actual.Should().NotBeNull();
            processRunner.Calls.Count.Should().Be(expectedWindowCount);
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    [Fact]
    public void EstimateAccurate_WhenFfmpegFails_ReturnsNull()
    {
        var inputPath = CreateTempFileWithLength(48_000_000);
        try
        {
            var probeReader = Substitute.For<IProbeReader>();
            probeReader.Read(inputPath).Returns(new ProbeResult(
                Format: new ProbeFormat(DurationSeconds: 3600, BitrateBps: 4_000_000),
                Streams: [new ProbeStream("video", "h264", Width: 1920, Height: 1080)]));

            var processRunner = new ScriptedProcessRunner(
                encodedSampleSizesBytes: [500_000, 500_000],
                failOnCallNumbers: [2]);
            var sut = CreateSut(probeReader, processRunner);

            var actual = sut.EstimateAccurate(new AutoSampleReductionInput(
                InputPath: inputPath,
                Cq: 23,
                Maxrate: 2.0,
                Bufsize: 4.0));

            actual.Should().BeNull();
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    [Fact]
    public void EstimateAccurate_WhenFirstAttemptFailsAndRetrySucceeds_ReturnsReduction()
    {
        var inputPath = CreateTempFileWithLength(120_000_000);
        try
        {
            var probeReader = Substitute.For<IProbeReader>();
            probeReader.Read(inputPath).Returns(new ProbeResult(
                Format: new ProbeFormat(DurationSeconds: 7200, BitrateBps: 6_000_000),
                Streams: [new ProbeStream("video", "h264", Width: 1920, Height: 1080)]));

            var processRunner = new ScriptedProcessRunner(
                encodedSampleSizesBytes: [500_000, 500_000, 500_000],
                failOnCallNumbers: [1]);
            var sut = CreateSut(probeReader, processRunner, sampleEncodeMaxRetries: 1);

            var actual = sut.EstimateAccurate(new AutoSampleReductionInput(
                InputPath: inputPath,
                Cq: 24,
                Maxrate: 2.4,
                Bufsize: 4.8));

            actual.Should().NotBeNull();
            actual!.Value.Should().BeApproximately(50.0, 0.01);
            processRunner.Calls.Count.Should().Be(4);
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    [Fact]
    public void EstimateAccurate_WhenAttemptsExhausted_ReturnsNull()
    {
        var inputPath = CreateTempFileWithLength(24_000_000);
        try
        {
            var probeReader = Substitute.For<IProbeReader>();
            probeReader.Read(inputPath).Returns(new ProbeResult(
                Format: new ProbeFormat(DurationSeconds: 1_200, BitrateBps: 4_000_000),
                Streams: [new ProbeStream("video", "h264", Width: 1280, Height: 720)]));

            var processRunner = new ScriptedProcessRunner(
                encodedSampleSizesBytes: [300_000],
                failOnCallNumbers: [1, 2]);
            var sut = CreateSut(probeReader, processRunner, sampleEncodeMaxRetries: 1);

            var actual = sut.EstimateAccurate(new AutoSampleReductionInput(
                InputPath: inputPath,
                Cq: 23,
                Maxrate: 2.0,
                Bufsize: 4.0));

            actual.Should().BeNull();
            processRunner.Calls.Count.Should().Be(2);
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    [Fact]
    public void EstimateAccurate_WhenRetryHappens_WritesWarningLog()
    {
        var inputPath = CreateTempFileWithLength(24_000_000);
        try
        {
            var probeReader = Substitute.For<IProbeReader>();
            probeReader.Read(inputPath).Returns(new ProbeResult(
                Format: new ProbeFormat(DurationSeconds: 1_200, BitrateBps: 4_000_000),
                Streams: [new ProbeStream("video", "h264", Width: 1280, Height: 720)]));

            var processRunner = new ScriptedProcessRunner(
                encodedSampleSizesBytes: [300_000],
                failOnCallNumbers: [1]);
            var logger = Substitute.For<ILogger<FfmpegAutoSampleReductionProvider>>();
            var sut = CreateSut(probeReader, processRunner, sampleEncodeMaxRetries: 1, logger: logger);

            var actual = sut.EstimateAccurate(new AutoSampleReductionInput(
                InputPath: inputPath,
                Cq: 23,
                Maxrate: 2.0,
                Bufsize: 4.0));

            actual.Should().NotBeNull();
            logger.Received().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(state => state.ToString()!.Contains("Sample encode attempt failed", StringComparison.Ordinal)),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    [Fact]
    public void EstimateAccurate_WhenCalled_UsesInputDirectoryWorkSubfolderAndCleansItUp()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"mte-provider-workdir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        var inputPath = CreateTempFileWithLength(120_000_000, testDir);

        try
        {
            var probeReader = Substitute.For<IProbeReader>();
            probeReader.Read(inputPath).Returns(new ProbeResult(
                Format: new ProbeFormat(DurationSeconds: 7_200, BitrateBps: 6_000_000),
                Streams: [new ProbeStream("video", "h264", Width: 1920, Height: 1080)]));

            var processRunner = new ScriptedProcessRunner(
                encodedSampleSizesBytes: [500_000, 500_000, 500_000]);
            var sut = CreateSut(probeReader, processRunner);

            var actual = sut.EstimateAccurate(new AutoSampleReductionInput(
                InputPath: inputPath,
                Cq: 24,
                Maxrate: 2.4,
                Bufsize: 4.8));

            actual.Should().NotBeNull();
            processRunner.OutputPaths.Should().NotBeEmpty();
            processRunner.OutputPaths.Should().OnlyContain(path =>
                path.StartsWith(testDir, StringComparison.OrdinalIgnoreCase) &&
                path.Contains(".mte-autosample-", StringComparison.OrdinalIgnoreCase));

            var workDirs = processRunner.OutputPaths
                .Select(Path.GetDirectoryName)
                .Where(static p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            workDirs.Should().HaveCount(1);
            Directory.Exists(workDirs[0]!).Should().BeFalse();
        }
        finally
        {
            TryDelete(inputPath);
            TryDeleteDirectory(testDir);
        }
    }

    private static FfmpegAutoSampleReductionProvider CreateSut(
        IProbeReader probeReader,
        IProcessRunner processRunner,
        int sampleEncodeMaxRetries = 0,
        ILogger<FfmpegAutoSampleReductionProvider>? logger = null)
    {
        return new FfmpegAutoSampleReductionProvider(
            probeReader,
            processRunner,
            ffmpegPath: "ffmpeg",
            timeoutMs: 30_000,
            sampleEncodeInactivityTimeoutMs: 12_000,
            sampleDurationSeconds: 60,
            nvencPreset: "p6",
            sampleEncodeMaxRetries: sampleEncodeMaxRetries,
            logger: logger);
    }

    private static string CreateTempFileWithLength(long lengthBytes, string? directoryPath = null)
    {
        var directory = string.IsNullOrWhiteSpace(directoryPath) ? Path.GetTempPath() : directoryPath;
        Directory.CreateDirectory(directory);

        var path = Path.Combine(
            directory,
            $"mte-provider-test-{Guid.NewGuid():N}.mkv");

        using var stream = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        stream.SetLength(lengthBytes);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup for temporary test artifacts.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for temporary test artifacts.
        }
    }

    private sealed class ScriptedProcessRunner(
        IReadOnlyList<long> encodedSampleSizesBytes,
        IReadOnlyList<int>? failOnCallNumbers = null) : IProcessRunner
    {
        private readonly Queue<long> _remainingSizes = new(encodedSampleSizesBytes);
        private readonly HashSet<int> _failOnCalls = new(failOnCallNumbers ?? []);
        public List<string> Calls { get; } = [];
        public List<string> OutputPaths { get; } = [];

        public ProcessRunResult Run(string fileName, string arguments, int timeoutMs = 30_000)
        {
            Calls.Add(arguments);
            var callIndex = Calls.Count;
            if (_failOnCalls.Contains(callIndex))
            {
                return new ProcessRunResult(
                    ExitCode: 1,
                    StdOut: string.Empty,
                    StdErr: "ffmpeg failed");
            }

            var outputPath = ExtractLastQuotedToken(arguments);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return new ProcessRunResult(
                    ExitCode: 1,
                    StdOut: string.Empty,
                    StdErr: "output path not found");
            }

            OutputPaths.Add(outputPath);
            var size = _remainingSizes.Count > 0 ? _remainingSizes.Dequeue() : 1_024;
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.SetLength(size);

            return new ProcessRunResult(
                ExitCode: 0,
                StdOut: string.Empty,
                StdErr: string.Empty);
        }

        public ProcessRunResult RunWithInactivityTimeout(
            string fileName,
            string arguments,
            int timeoutMs = 30_000,
            int inactivityTimeoutMs = 0)
        {
            return Run(fileName, arguments, timeoutMs);
        }

        private static string ExtractLastQuotedToken(string arguments)
        {
            var matches = Regex.Matches(arguments, "\"([^\"]+)\"");
            if (matches.Count == 0)
            {
                return string.Empty;
            }

            return matches[^1].Groups[1].Value;
        }
    }
}
