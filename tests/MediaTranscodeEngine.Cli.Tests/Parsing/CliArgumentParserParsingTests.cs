using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class CliArgumentParserParsingTests
{
    private const string DefaultInputPath = "C:\\video\\movie.mp4";

    [Fact]
    public void TryParse_WithUnknownOption_ReturnsFalseAndUnknownOptionError()
    {
        var ok = Parse(args: ["--unknown"], out _, out var errorText);

        ok.Should().BeFalse();
        errorText.Should().Be("Unknown option: --unknown");
    }

    [Fact]
    public void TryParse_WithMissingOptionValue_ReturnsFalseAndMissingValueError()
    {
        var ok = Parse(args: ["--input"], out _, out var errorText);

        ok.Should().BeFalse();
        errorText.Should().Be("--input requires a value.");
    }

    [Theory]
    [InlineData("--downscale", "abc", "--downscale must be an integer.")]
    [InlineData("--cq", "oops", "--cq must be an integer.")]
    public void TryParse_WithInvalidIntegerValue_ReturnsFalseAndOptionError(
        string optionName,
        string invalidValue,
        string expectedError)
    {
        var ok = Parse(
            args: CreateArgsWithInput(optionName, invalidValue),
            parsed: out _,
            errorText: out var errorText);

        ok.Should().BeFalse();
        errorText.Should().Be(expectedError);
    }

    [Fact]
    public void TryParse_WithLegacyPositionalToken_ReturnsFalseAndLegacyTokenError()
    {
        var ok = Parse(
            args: ["tomkvgpu", "--input", DefaultInputPath],
            parsed: out _,
            errorText: out var errorText);

        ok.Should().BeFalse();
        errorText.Should().Be("Do not use 'tomkvgpu' command token. Use CLI switches directly.");
    }

    [Fact]
    public void TryParse_WithRepeatedInputOption_ReturnsAllInputs()
    {
        var ok = Parse(
            args: ["--input", "C:\\video\\1.mp4", "--input", "C:\\video\\2.mp4"],
            parsed: out var parsed,
            errorText: out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.Inputs.Should().Equal("C:\\video\\1.mp4", "C:\\video\\2.mp4");
    }

    private static bool Parse(
        string[] args,
        out CliParseResult parsed,
        out string? errorText)
    {
        return CliArgumentParser.TryParse(args, out parsed, out errorText);
    }

    private static string[] CreateArgsWithInput(
        string optionName,
        string optionValue,
        string inputPath = DefaultInputPath)
    {
        return ["--input", inputPath, optionName, optionValue];
    }
}
