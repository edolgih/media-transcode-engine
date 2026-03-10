using FluentAssertions;
using MediaTranscodeEngine.Runtime.Inspection;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tests.Videos;

public sealed class VideoInspectorTests
{
    [Fact]
    public void Load_WhenFilePathIsBlank_ThrowsArgumentException()
    {
        var sut = CreateSut(_ => CreateSnapshot());

        Action action = () => sut.Load("   ");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Load_WhenProbeReturnsNull_ThrowsInvalidOperationException()
    {
        var sut = CreateSut(_ => null!);

        Action action = () => sut.Load(@"C:\video\input.mkv");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*no data*");
    }

    [Fact]
    public void Load_WhenRelativePathIsProvided_PassesNormalizedPathToProbe()
    {
        var capturedPath = string.Empty;
        var sut = CreateSut(path =>
        {
            capturedPath = path;
            return CreateSnapshot();
        });

        _ = sut.Load(@".\input.mkv");

        capturedPath.Should().Be(Path.GetFullPath(@".\input.mkv"));
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
    public void Load_WhenProbeReturnsNonPositiveDimensions_PreservesThemInSourceVideo()
    {
        var sut = CreateSut(_ => new VideoProbeSnapshot(
            container: "mp4",
            streams:
            [
                new VideoProbeStream(streamType: "video", codec: "h264", width: 0, height: 0, framesPerSecond: 25),
                new VideoProbeStream(streamType: "audio", codec: "aac")
            ],
            duration: TimeSpan.FromMinutes(10)));

        var actual = sut.Load(@"C:\video\input.mp4");

        actual.Width.Should().Be(0);
        actual.Height.Should().Be(0);
    }

    [Fact]
    public void Load_WhenDimensionsAreMissing_Throws()
    {
        var sut = CreateSut(_ => new VideoProbeSnapshot(
            container: "mp4",
            streams:
            [
                new VideoProbeStream(streamType: "video", codec: "h264", framesPerSecond: 25),
                new VideoProbeStream(streamType: "audio", codec: "aac")
            ],
            duration: TimeSpan.FromMinutes(10)));

        Action action = () => sut.Load(@"C:\video\input.mp4");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*valid video width*");
    }

    [Fact]
    public void Load_WhenHeightIsMissing_Throws()
    {
        var sut = CreateSut(_ => new VideoProbeSnapshot(
            container: "mp4",
            streams:
            [
                new VideoProbeStream(streamType: "video", codec: "h264", width: 1920, framesPerSecond: 25),
                new VideoProbeStream(streamType: "audio", codec: "aac")
            ],
            duration: TimeSpan.FromMinutes(10)));

        Action action = () => sut.Load(@"C:\video\input.mp4");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*valid video height*");
    }

    [Fact]
    public void Load_WhenFrameRateIsMissingOrNonPositive_Throws()
    {
        var sut = CreateSut(_ => new VideoProbeSnapshot(
            container: "mp4",
            streams:
            [
                new VideoProbeStream(streamType: "video", codec: "h264", width: 1920, height: 1080, framesPerSecond: 0),
                new VideoProbeStream(streamType: "audio", codec: "aac")
            ],
            duration: TimeSpan.FromMinutes(10)));

        Action action = () => sut.Load(@"C:\video\input.mp4");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*valid frame rate*");
    }

    [Fact]
    public void Load_WhenProbeReturnsVideoAndAudio_ReturnsSourceVideo()
    {
        var sut = CreateSut(_ => new VideoProbeSnapshot(
            container: "mp4",
            streams:
            [
                new VideoProbeStream(streamType: "video", codec: "h264", width: 1920, height: 1080, framesPerSecond: 23.976, bitrate: 4_000_000),
                new VideoProbeStream(streamType: "audio", codec: "aac", bitrate: 192_000),
                new VideoProbeStream(streamType: "audio", codec: "ac3", bitrate: 384_000)
            ],
            duration: TimeSpan.FromMinutes(10),
            formatBitrate: 5_500_000));

        var actual = sut.Load(@"C:\video\input.mp4");

        actual.Container.Should().Be("mp4");
        actual.VideoCodec.Should().Be("h264");
        actual.AudioCodecs.Should().Equal("aac", "ac3");
        actual.Width.Should().Be(1920);
        actual.Height.Should().Be(1080);
        actual.FramesPerSecond.Should().Be(23.976);
        actual.Duration.Should().Be(TimeSpan.FromMinutes(10));
        actual.Bitrate.Should().Be(5_500_000);
        actual.FilePath.Should().Be(Path.GetFullPath(@"C:\video\input.mp4"));
    }

    [Fact]
    public void Load_WhenContainerAndFormatBitrateAreMissing_FallsBackToFileExtensionAndStreamBitrates()
    {
        var sut = CreateSut(_ => new VideoProbeSnapshot(
            container: "  ",
            streams:
            [
                new VideoProbeStream(streamType: "video", codec: "h264", width: 1920, height: 1080, framesPerSecond: 23.976, bitrate: 4_000_000),
                new VideoProbeStream(streamType: "audio", codec: "aac", bitrate: 192_000),
                new VideoProbeStream(streamType: "audio", codec: "ac3", bitrate: 384_000)
            ],
            duration: TimeSpan.FromMinutes(10),
            formatBitrate: 0));

        var actual = sut.Load(@"C:\video\input.mov");

        actual.Container.Should().Be("mov");
        actual.Bitrate.Should().Be(4_576_000);
    }

    [Fact]
    public void Load_WhenExtendedProbeFactsArePresent_PreservesThem()
    {
        var sut = CreateSut(_ => new VideoProbeSnapshot(
            container: "mp4",
            streams:
            [
                new VideoProbeStream(
                    streamType: "video",
                    codec: "h264",
                    width: 1920,
                    height: 1080,
                    framesPerSecond: 59.94,
                    bitrate: 4_000_000,
                    rawFramesPerSecond: 59.94,
                    averageFramesPerSecond: 29.97),
                new VideoProbeStream(
                    streamType: "audio",
                    codec: "aac",
                    bitrate: 128_000,
                    sampleRate: 44_100,
                    channels: 2)
            ],
            duration: TimeSpan.FromMinutes(10),
            formatBitrate: 4_500_000,
            formatName: "mov,mp4,m4a,3gp,3g2,mj2"));

        var actual = sut.Load(@"C:\video\input.mp4");

        actual.FormatName.Should().Be("mov,mp4,m4a,3gp,3g2,mj2");
        actual.RawFramesPerSecond.Should().Be(59.94);
        actual.AverageFramesPerSecond.Should().Be(29.97);
        actual.HasFrameRateMismatch.Should().BeTrue();
        actual.PrimaryAudioBitrate.Should().Be(128_000);
        actual.PrimaryAudioSampleRate.Should().Be(44_100);
        actual.PrimaryAudioChannels.Should().Be(2);
    }

    private static VideoInspector CreateSut(Func<string, VideoProbeSnapshot> probe)
    {
        return new VideoInspector(new FakeVideoProbe(probe));
    }

    private static VideoProbeSnapshot CreateSnapshot()
    {
        return new VideoProbeSnapshot(
            container: "mkv",
            streams:
            [
                new VideoProbeStream(streamType: "video", codec: "h264", width: 1920, height: 1080, framesPerSecond: 29.97),
                new VideoProbeStream(streamType: "audio", codec: "aac")
            ],
            duration: TimeSpan.FromMinutes(10));
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
