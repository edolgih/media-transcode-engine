using FluentAssertions;
using MediaTranscodeEngine.Runtime.Inspection;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tests.Videos;

public sealed class ProbedVideoInspectorTests
{
    [Fact]
    public void Load_WhenProbeReturnsNull_ThrowsInvalidOperationException()
    {
        var sut = CreateSut(_ => null!);

        Action action = () => sut.Load(@"C:\video\input.mp4");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*no data*");
    }

    [Fact]
    public void Load_WhenProbeReturnsNoStreams_ThrowsInvalidOperationException()
    {
        var sut = CreateSut(_ => new VideoProbeSnapshot(
            container: "mp4",
            streams: [],
            duration: TimeSpan.FromMinutes(10)));

        Action action = () => sut.Load(@"C:\video\input.mp4");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*any streams*");
    }

    [Fact]
    public void Load_WhenProbeReturnsNoVideoStream_ThrowsInvalidOperationException()
    {
        var sut = CreateSut(_ => new VideoProbeSnapshot(
            container: "mp4",
            streams:
            [
                new VideoProbeStream(streamType: "audio", codec: "aac")
            ],
            duration: TimeSpan.FromMinutes(10)));

        Action action = () => sut.Load(@"C:\video\input.mp4");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*video stream*");
    }

    [Fact]
    public void Load_WhenProbeReturnsVideoAndAudio_ReturnsSourceVideo()
    {
        var sut = CreateSut(_ => new VideoProbeSnapshot(
            container: "mp4",
            streams:
            [
                new VideoProbeStream(streamType: "video", codec: "h264", width: 1920, height: 1080, framesPerSecond: 23.976),
                new VideoProbeStream(streamType: "audio", codec: "aac"),
                new VideoProbeStream(streamType: "audio", codec: "ac3")
            ],
            duration: TimeSpan.FromMinutes(10)));

        var actual = sut.Load(@"C:\video\input.mp4");

        actual.Container.Should().Be("mp4");
        actual.VideoCodec.Should().Be("h264");
        actual.AudioCodecs.Should().Equal("aac", "ac3");
        actual.Width.Should().Be(1920);
        actual.Height.Should().Be(1080);
        actual.FramesPerSecond.Should().Be(23.976);
        actual.Duration.Should().Be(TimeSpan.FromMinutes(10));
        actual.FilePath.Should().Be(Path.GetFullPath(@"C:\video\input.mp4"));
    }

    private static ProbedVideoInspector CreateSut(Func<string, VideoProbeSnapshot> probe)
    {
        return new ProbedVideoInspector(new FakeVideoProbe(probe));
    }

    private sealed class FakeVideoProbe : IVideoProbe
    {
        private readonly Func<string, VideoProbeSnapshot> _probe;

        public FakeVideoProbe(Func<string, VideoProbeSnapshot> probe)
        {
            _probe = probe;
        }

        public VideoProbeSnapshot Probe(string filePath)
        {
            return _probe(filePath);
        }
    }
}
