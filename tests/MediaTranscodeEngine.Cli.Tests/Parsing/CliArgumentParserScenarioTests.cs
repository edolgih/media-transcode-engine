using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class CliArgumentParserScenarioTests
{
    private const string DefaultInputPath = "C:\\video\\movie.mp4";

    [Fact]
    public void TryParse_WhenScenarioOptionProvided_ReturnsFalseAndUnknownOptionError()
    {
        var ok = Parse(
            args: ["--scenario", "tomkvgpu", "--input", DefaultInputPath],
            parsed: out _,
            errorText: out var errorText);

        ok.Should().BeFalse();
        errorText.Should().Be("Unknown option: --scenario");
    }

    [Fact]
    public void TryParse_WithUnifiedOptions_ReturnsTemplateWithMappedValues()
    {
        var ok = Parse(
            args:
            [
                "--input", DefaultInputPath,
                "--container", "mp4",
                "--compute", "gpu",
                "--preset", "p5"
            ],
            parsed: out var parsed,
            errorText: out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.TargetContainer.Should().Be("mp4");
        parsed.RequestTemplate.ComputeMode.Should().Be("gpu");
        parsed.RequestTemplate.VideoPreset.Should().Be("p5");
        parsed.RequestTemplate.PreferH264.Should().BeTrue();
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
    }

    private static bool Parse(
        string[] args,
        out CliParseResult parsed,
        out string? errorText)
    {
        return CliArgumentParser.TryParse(args, out parsed, out errorText);
    }
}
