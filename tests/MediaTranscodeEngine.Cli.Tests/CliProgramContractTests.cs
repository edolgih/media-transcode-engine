using FluentAssertions;

namespace MediaTranscodeEngine.Cli.Tests;

public class CliProgramContractTests
{
    [Fact]
    public async Task Main_WithHelpOption_ReturnsExitCodeZeroAndWritesHelpToStdOut()
    {
        var result = await RunCliAsync("--help");

        result.ExitCode.Should().Be(0);
        result.StdOut.Should().Contain("MediaTranscodeEngine CLI");
        result.StdErr.Should().BeEmpty();
    }

    [Fact]
    public async Task Main_WithUnknownOption_ReturnsExitCodeOneAndWritesUnknownOptionToStdErr()
    {
        var result = await RunCliAsync("--unknown");

        result.ExitCode.Should().Be(1);
        result.StdErr.Should().Contain("Unknown option: --unknown");
    }

    [Fact]
    public async Task Main_WithUnknownScenario_ReturnsExitCodeOneAndWritesUnknownScenarioToStdErr()
    {
        var result = await RunCliAsync("--scenario", "unknown", "--input", "C:\\video\\movie.mp4");

        result.ExitCode.Should().Be(1);
        result.StdErr.Should().Contain("Unknown scenario: unknown");
    }

    [Fact]
    public async Task Main_WithHelpOption_ReturnsHelpContainingCommonAndScenarioSections()
    {
        var result = await RunCliAsync("--help");

        result.ExitCode.Should().Be(0);
        result.StdOut.Should().Contain("Common options:");
        result.StdOut.Should().Contain("Scenario: tomkvgpu");
        result.StdOut.Should().Contain("Scenario: toh264gpu");
        result.StdOut.Should().Contain("--scenario <name>");
        result.StdOut.Should().Contain("--output-mkv");
    }

    [Fact]
    public async Task Main_WithInputFromStdIn_ReturnsWithoutNoInputError()
    {
        var result = await RunCliWithStdInAsync(
            stdIn: "C:\\video\\movie.mp4" + Environment.NewLine);

        result.ExitCode.Should().Be(0);
        result.StdErr.Should().NotContain("No input files provided");
    }

    private static Task<CliProcessResult> RunCliAsync(params string[] args)
    {
        return CliProcessRunner.RunAsync(args: args);
    }

    private static Task<CliProcessResult> RunCliWithStdInAsync(
        string stdIn,
        params string[] args)
    {
        return CliProcessRunner.RunAsync(args: args, stdIn: stdIn);
    }
}
