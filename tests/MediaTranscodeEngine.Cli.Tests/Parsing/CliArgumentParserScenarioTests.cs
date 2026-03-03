using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class CliArgumentParserScenarioTests
{
    private const string DefaultInputPath = "C:\\video\\movie.mp4";

    [Fact]
    public void TryParse_WithoutScenario_ReturnsDefaultTomkvgpu()
    {
        var ok = Parse(
            args: CreateArgsWithInput(),
            parsed: out var parsed,
            errorText: out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.ScenarioName.Should().Be(CliContracts.ToMkvGpuScenario);
    }

    [Theory]
    [InlineData(CliContracts.ToMkvGpuScenario)]
    [InlineData(CliContracts.ToH264GpuScenario)]
    public void TryParse_WithSupportedScenario_ReturnsRequestedScenario(string scenarioName)
    {
        var ok = Parse(
            args: CreateArgsWithScenario(scenarioName),
            parsed: out var parsed,
            errorText: out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.ScenarioName.Should().Be(scenarioName);
    }

    [Fact]
    public void TryParse_WithUnknownScenario_ReturnsFalseAndUnknownScenarioError()
    {
        var ok = Parse(
            args: CreateArgsWithScenario("unknown"),
            parsed: out _,
            errorText: out var errorText);

        ok.Should().BeFalse();
        errorText.Should().Be("Unknown scenario: unknown");
    }

    private static bool Parse(
        string[] args,
        out CliParseResult parsed,
        out string? errorText)
    {
        return CliArgumentParser.TryParse(args, out parsed, out errorText);
    }

    private static string[] CreateArgsWithInput(string inputPath = DefaultInputPath)
    {
        return ["--input", inputPath];
    }

    private static string[] CreateArgsWithScenario(
        string scenarioName,
        string inputPath = DefaultInputPath)
    {
        return ["--scenario", scenarioName, "--input", inputPath];
    }
}
