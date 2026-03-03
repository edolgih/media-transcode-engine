using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class CliScenarioMappersTranscodeTests
{
    [Fact]
    public void BuildToMkvRequest_WithInputPath_ReturnsDomainRequestWithNormalizedInputPath()
    {
        var template = CreateTemplate();

        var actual = CliScenarioMappers.BuildToMkvRequest(template, " C:\\video\\movie.mp4 ");

        actual.InputPath.Should().Be("C:\\video\\movie.mp4");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildToMkvRequest_WithInfoFlag_ReturnsDomainRequestWithSameInfoValue(bool info)
    {
        var template = CreateTemplate(info: info);

        var actual = CliScenarioMappers.BuildToMkvRequest(template, "C:\\video\\movie.mp4");

        actual.Info.Should().Be(info);
    }

    [Fact]
    public void BuildToMkvRequest_WithInvalidContentProfile_ThrowsArgumentException()
    {
        var template = CreateTemplate(contentProfile: "bad");

        Action action = () => CliScenarioMappers.BuildToMkvRequest(template, "C:\\video\\movie.mp4");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("ContentProfile")
            .WithMessage("*ContentProfile must be one of: anime, mult, film.*");
    }

    private static RawTranscodeRequest CreateTemplate(
        bool info = false,
        string contentProfile = RequestContracts.Transcode.DefaultContentProfile,
        int? cq = null)
    {
        return new RawTranscodeRequest(
            InputPath: "__input__",
            Info: info,
            ContentProfile: contentProfile,
            Cq: cq);
    }
}
