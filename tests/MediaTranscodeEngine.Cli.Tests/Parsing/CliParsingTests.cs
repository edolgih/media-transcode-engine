using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public sealed class CliParsingTests
{
    [Fact]
    public void TryParse_WhenToMkvGpuProfileOptionsAreProvided_PopulatesRequestTemplate()
    {
        var actual = CliArgumentParser.TryParse(
            [
                "--input", @"C:\video\a.mp4",
                "--downscale", "576",
                "--content-profile", "Anime",
                "--quality-profile", "High",
                "--no-autosample",
                "--autosample-mode", "Hybrid",
                "--downscale-algo", "Lanczos",
                "--cq", "23",
                "--maxrate", "3.4",
                "--bufsize", "6.8",
                "--nvenc-preset", "P5"
            ],
            out var parsed,
            out var errorText);

        actual.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.Inputs.Should().ContainSingle().Which.Should().Be(@"C:\video\a.mp4");
        parsed.RequestTemplate.DownscaleTarget.Should().Be(576);
        parsed.RequestTemplate.ContentProfile.Should().Be("Anime");
        parsed.RequestTemplate.QualityProfile.Should().Be("High");
        parsed.RequestTemplate.NoAutoSample.Should().BeTrue();
        parsed.RequestTemplate.AutoSampleMode.Should().Be("Hybrid");
        parsed.RequestTemplate.DownscaleAlgorithm.Should().Be("Lanczos");
        parsed.RequestTemplate.Cq.Should().Be(23);
        parsed.RequestTemplate.Maxrate.Should().Be(3.4m);
        parsed.RequestTemplate.Bufsize.Should().Be(6.8m);
        parsed.RequestTemplate.NvencPreset.Should().Be("P5");
    }

    [Fact]
    public void BuildRequest_WhenTemplateContainsToMkvGpuProfileOptions_MapsNestedRuntimeRequest()
    {
        var template = new CliRequestTemplate(
            Scenario: "tomkvgpu",
            Info: false,
            KeepSource: true,
            OverlayBackground: true,
            DownscaleTarget: 576,
            SynchronizeAudio: true,
            ContentProfile: "Film",
            QualityProfile: "Default",
            NoAutoSample: true,
            AutoSampleMode: "Fast",
            DownscaleAlgorithm: "Bicubic",
            Cq: 24,
            Maxrate: 3.7m,
            Bufsize: 7.4m,
            NvencPreset: "P6");

        var actual = CliRequestMappers.BuildRequest(template, @"C:\video\a.mp4");

        actual.InputPath.Should().Be(@"C:\video\a.mp4");
        actual.ToMkvGpu.KeepSource.Should().BeTrue();
        actual.ToMkvGpu.OverlayBackground.Should().BeTrue();
        actual.ToMkvGpu.SynchronizeAudio.Should().BeTrue();
        actual.ToMkvGpu.Downscale.Should().NotBeNull();
        actual.ToMkvGpu.Downscale!.TargetHeight.Should().Be(576);
        actual.ToMkvGpu.Downscale.ContentProfile.Should().Be("film");
        actual.ToMkvGpu.Downscale.QualityProfile.Should().Be("default");
        actual.ToMkvGpu.Downscale.NoAutoSample.Should().BeTrue();
        actual.ToMkvGpu.Downscale.AutoSampleMode.Should().Be("fast");
        actual.ToMkvGpu.Downscale.Algorithm.Should().Be("bicubic");
        actual.ToMkvGpu.Downscale.Cq.Should().Be(24);
        actual.ToMkvGpu.Downscale.Maxrate.Should().Be(3.7m);
        actual.ToMkvGpu.Downscale.Bufsize.Should().Be(7.4m);
        actual.ToMkvGpu.NvencPreset.Should().Be("p6");
    }

    [Theory]
    [InlineData("tomkvgpu", "Do not use legacy scenario command tokens. Use --scenario tomkvgpu.")]
    [InlineData("toh264gpu", "Do not use legacy scenario command tokens. Use --scenario tomkvgpu.")]
    [InlineData("--wat", "Unknown option: --wat")]
    [InlineData("unexpected", "Unexpected argument: unexpected")]
    public void TryParse_WhenArgsContainUnsupportedToken_ReturnsFalse(string token, string expectedError)
    {
        var actual = CliArgumentParser.TryParse(
            [token],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be(expectedError);
    }

    [Theory]
    [InlineData("--input")]
    [InlineData("--downscale")]
    public void TryParse_WhenRequiredOptionValueIsMissing_ReturnsFalse(string optionName)
    {
        var actual = CliArgumentParser.TryParse(
            [optionName],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be($"{optionName} requires a value.");
    }

    [Fact]
    public void TryParse_WhenRequiredOptionValueIsAnotherOption_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            ["--input", "--info"],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("--input requires a value.");
    }

    [Theory]
    [InlineData("--downscale", "abc", "--downscale must be an integer.")]
    [InlineData("--cq", "abc", "--cq must be an integer.")]
    [InlineData("--maxrate", "abc", "--maxrate must be a number.")]
    [InlineData("--bufsize", "abc", "--bufsize must be a number.")]
    public void TryParse_WhenOptionValueHasInvalidType_ReturnsFalse(
        string optionName,
        string value,
        string expectedError)
    {
        var actual = CliArgumentParser.TryParse(
            [optionName, value],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be(expectedError);
    }

    [Fact]
    public void TryParse_WhenScenarioIsUnsupported_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            ["--scenario", "other", "--input", @"C:\video\a.mp4"],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("Unsupported scenario: other. Only 'tomkvgpu' is available.");
    }
}
