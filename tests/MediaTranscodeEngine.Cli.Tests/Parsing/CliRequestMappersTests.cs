using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class CliRequestMappersTests
{
    [Fact]
    public void BuildUnifiedRequest_WithInputPath_ReturnsDomainRequestWithNormalizedInputPath()
    {
        var template = CreateTemplate();

        var actual = CliRequestMappers.BuildUnifiedRequest(template, " C:\\video\\movie.mp4 ");

        actual.InputPath.Should().Be("C:\\video\\movie.mp4");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildUnifiedRequest_WithKeepSourceFlag_ReturnsDomainRequestWithSameKeepSourceValue(bool keepSource)
    {
        var template = CreateTemplate(keepSource: keepSource);

        var actual = CliRequestMappers.BuildUnifiedRequest(template, "C:\\video\\movie.mp4");

        actual.KeepSource.Should().Be(keepSource);
    }

    [Fact]
    public void BuildUnifiedRequest_WithInvalidContentProfile_ThrowsArgumentException()
    {
        var template = CreateTemplate(contentProfile: "bad");

        Action action = () => CliRequestMappers.BuildUnifiedRequest(template, "C:\\video\\movie.mp4");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("ContentProfile")
            .WithMessage("*ContentProfile must be one of: anime, mult, film.*");
    }

    private static RawUnifiedTranscodeRequest CreateTemplate(
        bool keepSource = false,
        string contentProfile = RequestContracts.Transcode.DefaultContentProfile)
    {
        return new RawUnifiedTranscodeRequest(
            InputPath: "__input__",
            KeepSource: keepSource,
            ContentProfile: contentProfile);
    }
}
