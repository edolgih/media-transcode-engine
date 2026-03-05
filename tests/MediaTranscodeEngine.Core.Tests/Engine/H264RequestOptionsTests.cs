using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class H264RequestOptionsTests
{
    [Fact]
    public void Create_WhenCalledWithH264Options_ReturnsNormalizedRequest()
    {
        var actual = TranscodeRequest.Create(
            InputPath: " C:\\video\\movie.mp4 ",
            TargetContainer: RequestContracts.General.MkvContainer,
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            PreferH264: true,
            Downscale: 576,
            KeepFps: true,
            DownscaleAlgo: "lanczos",
            Cq: 20,
            VideoPreset: "p6",
            UseAq: true,
            AqStrength: 7,
            Denoise: true,
            FixTimestamps: true,
            KeepSource: true);

        actual.InputPath.Should().Be("C:\\video\\movie.mp4");
        actual.TargetContainer.Should().Be(RequestContracts.General.MkvContainer);
        actual.EncoderBackend.Should().Be(RequestContracts.General.GpuEncoderBackend);
        actual.PreferH264.Should().BeTrue();
        actual.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
        actual.Downscale.Should().Be(576);
        actual.KeepFps.Should().BeTrue();
        actual.DownscaleAlgo.Should().Be("lanczos");
        actual.Cq.Should().Be(20);
        actual.VideoPreset.Should().Be("p6");
        actual.UseAq.Should().BeTrue();
        actual.AqStrength.Should().Be(7);
        actual.Denoise.Should().BeTrue();
        actual.FixTimestamps.Should().BeTrue();
        actual.KeepSource.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenDefaultsUsedForH264Options_UsesContractDefaults()
    {
        var actual = TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4");

        actual.Downscale.Should().BeNull();
        actual.KeepFps.Should().BeFalse();
        actual.DownscaleAlgo.Should().Be(RequestContracts.General.DefaultDownscaleAlgorithm);
        actual.VideoPreset.Should().Be(RequestContracts.General.DefaultVideoPreset);
        actual.UseAq.Should().BeFalse();
        actual.AqStrength.Should().Be(RequestContracts.General.DefaultAqStrength);
        actual.Denoise.Should().BeFalse();
        actual.FixTimestamps.Should().BeFalse();
        actual.KeepSource.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenDownscaleInvalid_ThrowsArgumentException(int downscale)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            Downscale: downscale);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Downscale")
            .WithMessage("*Downscale must be greater than zero.*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(52)]
    public void Create_WhenCqOutOfRange_ThrowsArgumentException(int cq)
    {
        Action action = () => TranscodeRequest.Create(
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
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            AqStrength: aqStrength);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("AqStrength")
            .WithMessage("*AqStrength must be in range 1..15.*");
    }
}
