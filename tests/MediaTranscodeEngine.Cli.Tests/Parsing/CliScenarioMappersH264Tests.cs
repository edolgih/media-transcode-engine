using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class CliScenarioMappersH264Tests
{
    [Fact]
    public void BuildToH264Request_WithInputPath_ReturnsDomainRequestWithNormalizedInputPath()
    {
        var template = CreateTemplate();

        var actual = CliScenarioMappers.BuildToH264Request(template, " C:\\video\\movie.mp4 ");

        actual.InputPath.Should().Be("C:\\video\\movie.mp4");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildToH264Request_WithOutputMkvFlag_ReturnsDomainRequestWithSameOutputMkvValue(bool outputMkv)
    {
        var template = CreateTemplate(outputMkv: outputMkv);

        var actual = CliScenarioMappers.BuildToH264Request(template, "C:\\video\\movie.mp4");

        actual.OutputMkv.Should().Be(outputMkv);
    }

    [Fact]
    public void BuildToH264Request_WithInvalidAqStrength_ThrowsArgumentException()
    {
        var template = CreateTemplate(aqStrength: 0);

        Action action = () => CliScenarioMappers.BuildToH264Request(template, "C:\\video\\movie.mp4");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("AqStrength")
            .WithMessage("*AqStrength must be in range 1..15.*");
    }

    private static RawH264TranscodeRequest CreateTemplate(
        bool outputMkv = false,
        int aqStrength = RequestContracts.H264.DefaultAqStrength,
        int? downscale = null)
    {
        return new RawH264TranscodeRequest(
            InputPath: "__input__",
            OutputMkv: outputMkv,
            AqStrength: aqStrength,
            Downscale: downscale);
    }
}
