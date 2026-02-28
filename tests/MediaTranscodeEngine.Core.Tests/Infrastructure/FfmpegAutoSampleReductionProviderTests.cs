using System.Text.RegularExpressions;
using FluentAssertions;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Infrastructure;
using NSubstitute;

namespace MediaTranscodeEngine.Core.Tests.Infrastructure;

public class FfmpegAutoSampleReductionProviderTests
{
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

    private static FfmpegAutoSampleReductionProvider CreateSut(
        IProbeReader probeReader,
        IProcessRunner processRunner)
    {
        return new FfmpegAutoSampleReductionProvider(
            probeReader,
            processRunner,
            ffmpegPath: "ffmpeg",
            timeoutMs: 30_000,
            sampleDurationSeconds: 60,
            nvencPreset: "p6");
    }

    private static string CreateTempFileWithLength(long lengthBytes)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
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

    private sealed class ScriptedProcessRunner(
        IReadOnlyList<long> encodedSampleSizesBytes,
        IReadOnlyList<int>? failOnCallNumbers = null) : IProcessRunner
    {
        private readonly Queue<long> _remainingSizes = new(encodedSampleSizesBytes);
        private readonly HashSet<int> _failOnCalls = new(failOnCallNumbers ?? []);
        public List<string> Calls { get; } = [];

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
