using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class H264TranscodeRequestTests
{
    [Fact]
    public void Create_WhenInputPathIsMissing_ThrowsArgumentException()
    {
        Action action = () => H264TranscodeRequest.Create(InputPath: " ");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("InputPath")
            .WithMessage("*InputPath is required.*");
    }

    [Fact]
    public void Create_WhenCalled_ReturnsNormalizedRequest()
    {
        var actual = H264TranscodeRequest.Create(
            InputPath: " C:\\video\\movie.mp4 ",
            Downscale: 576,
            KeepFps: true,
            DownscaleAlgo: "lanczos",
            Cq: 20,
            NvencPreset: "p6",
            UseAq: true,
            AqStrength: 7,
            Denoise: true,
            FixTimestamps: true,
            OutputMkv: true);

        actual.InputPath.Should().Be("C:\\video\\movie.mp4");
        actual.Downscale.Should().Be(576);
        actual.KeepFps.Should().BeTrue();
        actual.DownscaleAlgo.Should().Be("lanczos");
        actual.Cq.Should().Be(20);
        actual.NvencPreset.Should().Be("p6");
        actual.UseAq.Should().BeTrue();
        actual.AqStrength.Should().Be(7);
        actual.Denoise.Should().BeTrue();
        actual.FixTimestamps.Should().BeTrue();
        actual.OutputMkv.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenDefaultsUsed_UsesContractDefaults()
    {
        var actual = H264TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4");

        actual.Downscale.Should().BeNull();
        actual.KeepFps.Should().BeFalse();
        actual.DownscaleAlgo.Should().Be(RequestContracts.H264.DefaultDownscaleAlgorithm);
        actual.Cq.Should().BeNull();
        actual.NvencPreset.Should().Be(RequestContracts.H264.DefaultNvencPreset);
        actual.UseAq.Should().BeFalse();
        actual.AqStrength.Should().Be(RequestContracts.H264.DefaultAqStrength);
        actual.Denoise.Should().BeFalse();
        actual.FixTimestamps.Should().BeFalse();
        actual.OutputMkv.Should().BeFalse();
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("nearest")]
    public void Create_WhenDownscaleAlgoInvalid_ThrowsArgumentException(string downscaleAlgo)
    {
        Action action = () => H264TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            DownscaleAlgo: downscaleAlgo);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("DownscaleAlgo")
            .WithMessage("*DownscaleAlgo must be one of: bicubic, lanczos, bilinear.*");
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("p8")]
    public void Create_WhenNvencPresetInvalid_ThrowsArgumentException(string nvencPreset)
    {
        Action action = () => H264TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            NvencPreset: nvencPreset);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("NvencPreset")
            .WithMessage("*NvencPreset must be one of: p1, p2, p3, p4, p5, p6, p7.*");
    }

    [Theory]
    [InlineData(480)]
    [InlineData(1080)]
    public void Create_WhenDownscaleInvalid_ThrowsArgumentException(int downscale)
    {
        Action action = () => H264TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            Downscale: downscale);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Downscale")
            .WithMessage("*Downscale must be 576 or 720.*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(52)]
    public void Create_WhenCqOutOfRange_ThrowsArgumentException(int cq)
    {
        Action action = () => H264TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            Cq: cq);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Cq")
            .WithMessage("*Cq must be in range 0..51.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public void Create_WhenAqStrengthOutOfRange_ThrowsArgumentException(int aqStrength)
    {
        Action action = () => H264TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            AqStrength: aqStrength);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("AqStrength")
            .WithMessage("*AqStrength must be in range 1..15.*");
    }
}
