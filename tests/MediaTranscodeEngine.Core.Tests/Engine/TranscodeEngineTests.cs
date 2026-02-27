using FluentAssertions;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Policy;
using NSubstitute;

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class TranscodeEngineTests
{
    [Fact]
    public void Process_WhenDownscale720Requested_ReturnsNotImplementedRem()
    {
        var (sut, _, _) = CreateSut();
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4", Downscale: 720);

        var actual = sut.Process(request);

        actual.Should().StartWith("REM Downscale 720 not implemented:");
    }

    [Fact]
    public void Process_WhenProbeMissing_ReturnsFfprobeFailedRem()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns((ProbeResult?)null);
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");

        var actual = sut.Process(request);

        actual.Should().Be("REM ffprobe failed: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenNoVideoStream_ReturnsNoVideoRem()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(new ProbeResult(
            Format: null,
            Streams: new[] { new ProbeStream("audio", "aac") }));
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");

        var actual = sut.Process(request);

        actual.Should().Be("REM Нет видеопотока: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenInfoAndMkvWithoutChanges_ReturnsEmptyString()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mkv", Info: true);

        var actual = sut.Process(request);

        actual.Should().BeEmpty();
    }

    [Fact]
    public void Process_WhenDownscale576AndSourceBucketMissing_ReturnsHintRem()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 900));
        profileRepository.Get576Config().Returns(CreateConfigWithoutBuckets());
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4", Downscale: 576);

        var actual = sut.Process(request);

        actual.Should().StartWith("REM 576 source bucket missing");
        actual.Should().Contain("add SourceBuckets match");
    }

    [Fact]
    public void Process_WhenDownscale576WithProfileSettings_BuildsDownscaleCommand()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigWithBuckets());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mp4",
            Downscale: 576,
            ContentProfile: "anime",
            QualityProfile: "default");

        var actual = sut.Process(request);

        actual.Should().StartWith("ffmpeg -hide_banner");
        actual.Should().Contain("scale_cuda=-2:576:interp_algo=bilinear:format=nv12");
        actual.Should().Contain("-cq 23");
        actual.Should().Contain("-maxrate 2.4M");
        actual.Should().Contain("-bufsize 4.8M");
    }

    private static (TranscodeEngine Sut, IProbeReader ProbeReader, IProfileRepository ProfileRepository) CreateSut()
    {
        var probeReader = Substitute.For<IProbeReader>();
        var profileRepository = Substitute.For<IProfileRepository>();
        var sut = new TranscodeEngine(
            probeReader,
            profileRepository,
            new TranscodePolicy(),
            new FfmpegCommandBuilder());

        return (sut, probeReader, profileRepository);
    }

    private static ProbeResult CreateProbe(string codec, string audioCodec, int height)
    {
        return new ProbeResult(
            Format: new ProbeFormat(DurationSeconds: 600, BitrateBps: 6_000_000),
            Streams: new[]
            {
                new ProbeStream("video", codec, Width: 1920, Height: height),
                new ProbeStream("audio", audioCodec)
            });
    }

    private static TranscodePolicyConfig CreateConfigWithoutBuckets()
    {
        var defaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8)
        };

        var limits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileLimits(CqMin: 20, CqMax: 26, MaxrateMin: 2.0, MaxrateMax: 3.0)
        };

        return new TranscodePolicyConfig(
            ContentProfiles: new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["anime"] = new ContentProfileSettings("bilinear", defaults, limits),
                ["film"] = new ContentProfileSettings("bilinear", defaults, limits)
            },
            RateModel: new RateModelSettings(0.4, 2.0),
            SourceBuckets: new List<SourceBucketSettings>());
    }

    private static TranscodePolicyConfig CreateConfigWithBuckets()
    {
        var config = CreateConfigWithoutBuckets();
        var bucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 45, MaxInclusive: 60)
            }
        };

        var buckets = new List<SourceBucketSettings>
        {
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                ContentQualityRanges: bucketRanges)
        };

        return config with { SourceBuckets = buckets };
    }
}
