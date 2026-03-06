using FluentAssertions;
using MediaTranscodeEngine.Runtime.Inspection;

namespace MediaTranscodeEngine.Runtime.Tests.Inspection;

public sealed class FfprobeVideoProbeTests
{
    [Fact]
    public void Probe_WhenProcessReturnsValidJson_ReturnsMappedSnapshot()
    {
        var capturedPath = string.Empty;
        var sut = new FfprobeVideoProbe(filePath =>
        {
            capturedPath = filePath;
            return new FfprobeProcessResult(
                ExitCode: 0,
                StandardOutput: CreateValidJson(),
                StandardError: string.Empty);
        });

        var actual = sut.Probe(@".\input.mp4");

        capturedPath.Should().Be(Path.GetFullPath(@".\input.mp4"));
        actual.container.Should().Be("mp4");
        actual.duration.Should().Be(TimeSpan.FromSeconds(600.123));
        actual.streams.Should().HaveCount(2);
        actual.streams[0].streamType.Should().Be("video");
        actual.streams[0].codec.Should().Be("h264");
        actual.streams[0].width.Should().Be(1920);
        actual.streams[0].height.Should().Be(1080);
        actual.streams[0].framesPerSecond.Should().BeApproximately(30000d / 1001d, 0.0001);
        actual.streams[1].streamType.Should().Be("audio");
        actual.streams[1].codec.Should().Be("aac");
    }

    [Fact]
    public void Probe_WhenProcessFails_ThrowsInvalidOperationException()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 1,
            StandardOutput: string.Empty,
            StandardError: "ffprobe exploded"));

        Action action = () => sut.Probe(@"C:\video\input.mp4");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ffprobe process failed*ExitCode=1*ffprobe exploded*");
    }

    [Fact]
    public void Probe_WhenStdOutIsEmpty_ThrowsInvalidOperationException()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: "   ",
            StandardError: string.Empty));

        Action action = () => sut.Probe(@"C:\video\input.mp4");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty JSON output*");
    }

    [Fact]
    public void Probe_WhenJsonIsInvalid_ThrowsInvalidOperationException()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: "{invalid",
            StandardError: string.Empty));

        Action action = () => sut.Probe(@"C:\video\input.mp4");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*invalid JSON output*");
    }

    [Fact]
    public void Probe_WhenRequiredStreamFieldIsMissing_ThrowsInvalidOperationException()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: CreateJsonWithoutCodecName(),
            StandardError: string.Empty));

        Action action = () => sut.Probe(@"C:\video\input.mp4");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*required field*codec_name*stream*");
    }

    private static string CreateValidJson()
    {
        return """
               {
                 "format": {
                   "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
                   "duration": "600.123"
                 },
                 "streams": [
                   {
                     "codec_type": "video",
                     "codec_name": "h264",
                     "width": 1920,
                     "height": 1080,
                     "r_frame_rate": "30000/1001"
                   },
                   {
                     "codec_type": "audio",
                     "codec_name": "aac"
                   }
                 ]
               }
               """;
    }

    private static string CreateJsonWithoutCodecName()
    {
        return """
               {
                 "format": {
                   "format_name": "mp4",
                   "duration": "120.5"
                 },
                 "streams": [
                   {
                     "codec_type": "video",
                     "width": 1920,
                     "height": 1080,
                     "r_frame_rate": "25/1"
                   }
                 ]
               }
               """;
    }
}
