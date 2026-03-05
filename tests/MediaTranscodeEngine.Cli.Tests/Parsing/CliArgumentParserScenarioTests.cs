using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class CliArgumentParserScenarioTests
{
    private const string DefaultInputPath = "C:\\video\\movie.mp4";

    [Fact]
    public void TryParse_WhenScenarioOptionProvided_ReturnsTemplateWithScenario()
    {
        var ok = Parse(
            args: ["--scenario", "tomkvgpu", "--input", DefaultInputPath],
            parsed: out var parsed,
            errorText: out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.Scenario.Should().Be("tomkvgpu");
        parsed.ExplicitTemplateFields.Should().Contain(nameof(RawTranscodeRequest.Scenario));
    }

    [Fact]
    public void TryParse_WithGeneralOptions_ReturnsTemplateWithMappedValues()
    {
        var ok = Parse(
            args:
            [
                "--input", DefaultInputPath,
                "--container", "mp4",
                "--encoder-backend", "gpu",
                "--preset", "p5"
            ],
            parsed: out var parsed,
            errorText: out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.TargetContainer.Should().Be("mp4");
        parsed.RequestTemplate.EncoderBackend.Should().Be("gpu");
        parsed.RequestTemplate.VideoPreset.Should().Be("p5");
        parsed.RequestTemplate.PreferH264.Should().BeTrue();
        parsed.RequestTemplate.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
    }

    [Fact]
    public void TryParse_WithComputeAlias_ReturnsTemplateWithEncoderBackend()
    {
        var ok = Parse(
            args:
            [
                "--input", DefaultInputPath,
                "--compute", "gpu"
            ],
            parsed: out var parsed,
            errorText: out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.EncoderBackend.Should().Be("gpu");
        parsed.ExplicitTemplateFields.Should().Contain(nameof(RawTranscodeRequest.EncoderBackend));
    }

    [Fact]
    public void TryParse_WithH264Option_ReturnsTemplateWithPreferH264Enabled()
    {
        var ok = Parse(
            args: ["--input", DefaultInputPath, "--keep-fps"],
            parsed: out var parsed,
            errorText: out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.PreferH264.Should().BeTrue();
        parsed.RequestTemplate.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
    }

    [Fact]
    public void TryParse_WithVideoCodecOption_ReturnsTemplateWithTargetVideoCodec()
    {
        var ok = Parse(
            args: ["--input", DefaultInputPath, "--video-codec", RequestContracts.General.H265VideoCodec],
            parsed: out var parsed,
            errorText: out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.TargetVideoCodec.Should().Be(RequestContracts.General.H265VideoCodec);
        parsed.ExplicitTemplateFields.Should().Contain(nameof(RawTranscodeRequest.TargetVideoCodec));
    }

    private static bool Parse(
        string[] args,
        out CliParseResult parsed,
        out string? errorText)
    {
        return CliArgumentParser.TryParse(args, out parsed, out errorText);
    }
}
