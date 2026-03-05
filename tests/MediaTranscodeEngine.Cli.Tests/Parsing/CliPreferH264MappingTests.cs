using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class CliPreferH264MappingTests
{
    private const string DefaultInputPath = "C:\\video\\movie.mp4";

    [Fact]
    public void TryParse_WithOutputMkv_SetsPreferH264True()
    {
        var ok = Parse(
            ["--input", DefaultInputPath, "--output-mkv"],
            out var parsed,
            out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.PreferH264.Should().BeTrue();
        parsed.RequestTemplate.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
    }

    [Fact]
    public void TryParse_WithUseAq_SetsPreferH264True()
    {
        var ok = Parse(
            ["--input", DefaultInputPath, "--use-aq"],
            out var parsed,
            out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.PreferH264.Should().BeTrue();
        parsed.RequestTemplate.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
    }

    [Fact]
    public void TryParse_WithAqStrength_SetsPreferH264True()
    {
        var ok = Parse(
            ["--input", DefaultInputPath, "--aq-strength", "9"],
            out var parsed,
            out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.PreferH264.Should().BeTrue();
        parsed.RequestTemplate.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
    }

    [Fact]
    public void TryParse_WithDenoise_SetsPreferH264True()
    {
        var ok = Parse(
            ["--input", DefaultInputPath, "--denoise"],
            out var parsed,
            out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.PreferH264.Should().BeTrue();
        parsed.RequestTemplate.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
    }

    [Fact]
    public void TryParse_WithFixTimestamps_SetsPreferH264True()
    {
        var ok = Parse(
            ["--input", DefaultInputPath, "--fix-timestamps"],
            out var parsed,
            out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.PreferH264.Should().BeTrue();
        parsed.RequestTemplate.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
    }

    [Fact]
    public void TryParse_WithContainerMkv_DoesNotForcePreferH264()
    {
        var ok = Parse(
            ["--input", DefaultInputPath, "--container", "mkv"],
            out var parsed,
            out var errorText);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.RequestTemplate.PreferH264.Should().BeFalse();
        parsed.RequestTemplate.TargetVideoCodec.Should().Be(RequestContracts.General.CopyVideoCodec);
    }

    private static bool Parse(
        string[] args,
        out CliParseResult parsed,
        out string? errorText)
    {
        return CliArgumentParser.TryParse(args, out parsed, out errorText);
    }
}
