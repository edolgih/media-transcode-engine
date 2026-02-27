using FluentAssertions;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Infrastructure;
using NSubstitute;

namespace MediaTranscodeEngine.Core.Tests.Infrastructure;

public class FfprobeReaderTests
{
    [Fact]
    public void Read_WhenProcessReturnsValidJson_ReturnsMappedProbeResult()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Run(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(new ProcessRunResult(
                ExitCode: 0,
                StdOut: CreateValidJson(),
                StdErr: string.Empty));
        var sut = CreateSut(processRunner);

        var actual = sut.Read("C:\\video\\input.mp4");

        actual.Should().NotBeNull();
        actual!.Format.Should().NotBeNull();
        actual.Format!.DurationSeconds.Should().Be(600.123);
        actual.Format.BitrateBps.Should().Be(6_000_000);
        actual.Streams.Count.Should().Be(2);
        actual.Streams[0].CodecType.Should().Be("video");
        actual.Streams[0].CodecName.Should().Be("h264");
        actual.Streams[0].Width.Should().Be(1920);
        actual.Streams[0].Height.Should().Be(1080);
        actual.Streams[1].CodecType.Should().Be("audio");
        actual.Streams[1].CodecName.Should().Be("aac");
    }

    [Fact]
    public void Read_WhenProcessExitCodeIsNotZero_ReturnsNull()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Run(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(new ProcessRunResult(
                ExitCode: 1,
                StdOut: CreateValidJson(),
                StdErr: "ffprobe error"));
        var sut = CreateSut(processRunner);

        var actual = sut.Read("C:\\video\\input.mp4");

        actual.Should().BeNull();
    }

    [Fact]
    public void Read_WhenStdOutIsEmpty_ReturnsNull()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Run(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(new ProcessRunResult(
                ExitCode: 0,
                StdOut: string.Empty,
                StdErr: string.Empty));
        var sut = CreateSut(processRunner);

        var actual = sut.Read("C:\\video\\input.mp4");

        actual.Should().BeNull();
    }

    [Fact]
    public void Read_WhenJsonIsInvalid_ReturnsNull()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Run(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(new ProcessRunResult(
                ExitCode: 0,
                StdOut: "{invalid",
                StdErr: string.Empty));
        var sut = CreateSut(processRunner);

        var actual = sut.Read("C:\\video\\input.mp4");

        actual.Should().BeNull();
    }

    [Fact]
    public void Read_WhenStreamWithoutCodecTypeOrCodecName_SkipsInvalidStream()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Run(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(new ProcessRunResult(
                ExitCode: 0,
                StdOut: CreateJsonWithInvalidStreams(),
                StdErr: string.Empty));
        var sut = CreateSut(processRunner);

        var actual = sut.Read("C:\\video\\input.mp4");

        actual.Should().NotBeNull();
        actual!.Streams.Count.Should().Be(1);
        actual.Streams[0].CodecType.Should().Be("audio");
        actual.Streams[0].CodecName.Should().Be("aac");
    }

    [Fact]
    public void Read_WhenCalled_QuotesInputPathInFfprobeArguments()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Run(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(new ProcessRunResult(
                ExitCode: 0,
                StdOut: CreateValidJson(),
                StdErr: string.Empty));
        var sut = CreateSut(processRunner, ffprobePath: "custom-ffprobe");

        _ = sut.Read("C:\\video\\my file.mp4");

        processRunner.Received(1).Run(
            "custom-ffprobe",
            Arg.Is<string>(a => a.Contains("\"C:\\video\\my file.mp4\"")),
            30_000);
    }

    private static FfprobeReader CreateSut(IProcessRunner processRunner, string ffprobePath = "ffprobe")
    {
        return new FfprobeReader(processRunner, ffprobePath, timeoutMs: 30_000);
    }

    private static string CreateValidJson()
    {
        return """
               {
                 "format": {
                   "duration": "600.123",
                   "bit_rate": "6000000"
                 },
                 "streams": [
                   {
                     "codec_type": "video",
                     "codec_name": "h264",
                     "width": 1920,
                     "height": 1080,
                     "bit_rate": "5000000"
                   },
                   {
                     "codec_type": "audio",
                     "codec_name": "aac",
                     "bit_rate": "192000"
                   }
                 ]
               }
               """;
    }

    private static string CreateJsonWithInvalidStreams()
    {
        return """
               {
                 "format": {
                   "duration": "601.5",
                   "bit_rate": "6100000"
                 },
                 "streams": [
                   {
                     "codec_type": "video"
                   },
                   {
                     "codec_name": "h264"
                   },
                   {
                     "codec_type": "audio",
                     "codec_name": "aac"
                   }
                 ]
               }
               """;
    }
}
