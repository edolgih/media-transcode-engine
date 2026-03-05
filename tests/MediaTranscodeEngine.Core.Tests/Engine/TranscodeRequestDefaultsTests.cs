using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class TranscodeRequestTests
{
    [Fact]
    public void Create_WhenInputPathIsMissing_ThrowsArgumentException()
    {
        Action action = () => TranscodeRequest.Create(InputPath: " ");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("InputPath")
            .WithMessage("*InputPath is required.*");
    }

    [Fact]
    public void Create_WhenDefaultsUsed_UsesContractDefaults()
    {
        var request = TranscodeRequest.Create(InputPath: "C:\\video\\movie.mkv");

        request.TargetContainer.Should().Be(RequestContracts.General.DefaultContainer);
        request.EncoderBackend.Should().Be(RequestContracts.General.DefaultEncoderBackend);
        request.VideoPreset.Should().Be(RequestContracts.General.DefaultVideoPreset);
        request.DownscaleAlgo.Should().Be(RequestContracts.General.DefaultDownscaleAlgorithm);
        request.AqStrength.Should().Be(RequestContracts.General.DefaultAqStrength);
    }

    [Theory]
    [InlineData("avi")]
    [InlineData("")]
    public void Create_WhenTargetContainerInvalid_ThrowsArgumentException(string container)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mkv",
            TargetContainer: container);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("TargetContainer");
    }

    [Theory]
    [InlineData("vpu")]
    [InlineData("")]
    public void Create_WhenEncoderBackendInvalid_ThrowsArgumentException(string encoderBackend)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mkv",
            EncoderBackend: encoderBackend);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("EncoderBackend");
    }

    [Fact]
    public void Create_WhenPresetInvalid_ThrowsArgumentException()
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mkv",
            VideoPreset: "slow");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("VideoPreset")
            .WithMessage("*VideoPreset must be one of: p1, p2, p3, p4, p5, p6, p7.*");
    }
}
