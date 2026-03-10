using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Cli.Scenarios;
using MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public sealed class CliParsingTests
{
    [Fact]
    public void TryParse_WhenToMkvGpuProfileOptionsAreProvided_PreservesScenarioArgs()
    {
        var actual = CliArgumentParser.TryParse(
            [
                "--scenario", "tomkvgpu",
                "--input", @"C:\video\a.mp4",
                "--downscale", "576",
                "--content-profile", "Anime",
                "--quality-profile", "High",
                "--no-autosample",
                "--autosample-mode", "Hybrid",
                "--downscale-algo", "Lanczos",
                "--max-fps", "50",
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
        parsed.Scenario.Should().Be("tomkvgpu");
        parsed.Info.Should().BeFalse();
        parsed.ScenarioArgs.Should().Equal(
            "--downscale", "576",
            "--content-profile", "Anime",
            "--quality-profile", "High",
            "--no-autosample",
            "--autosample-mode", "Hybrid",
            "--downscale-algo", "Lanczos",
            "--max-fps", "50",
            "--cq", "23",
            "--maxrate", "3.4",
            "--bufsize", "6.8",
            "--nvenc-preset", "P5");
    }

    [Fact]
    public void CreateScenario_WhenParsedArgsContainToMkvGpuProfileOptions_MapsRuntimeRequest()
    {
        var parsedOk = CliArgumentParser.TryParse(
            [
                "--scenario", "tomkvgpu",
                "--input", @"C:\video\a.mp4",
                "--keep-source",
                "--overlay-bg",
                "--sync-audio",
                "--downscale", "576",
                "--content-profile", "Film",
                "--quality-profile", "Default",
                "--no-autosample",
                "--autosample-mode", "Fast",
                "--downscale-algo", "Bicubic",
                "--max-fps", "40",
                "--cq", "24",
                "--maxrate", "3.7",
                "--bufsize", "7.4",
                "--nvenc-preset", "P6"
            ],
            out var parsed,
            out var errorText);

        parsedOk.Should().BeTrue();
        errorText.Should().BeNull();

        var handler = new ToMkvGpuCliScenarioHandler(new ToMkvGpuInfoFormatter());
        var request = new CliTranscodeRequest(
            inputPath: @"C:\video\a.mp4",
            scenarioName: parsed.Scenario,
            info: parsed.Info,
            scenarioArgs: parsed.ScenarioArgs);

        var actual = handler.CreateScenario(request).Should().BeOfType<ToMkvGpuScenario>().Subject;
        var scenarioRequest = actual.Request;

        request.InputPath.Should().Be(@"C:\video\a.mp4");
        scenarioRequest.KeepSource.Should().BeTrue();
        scenarioRequest.OverlayBackground.Should().BeTrue();
        scenarioRequest.SynchronizeAudio.Should().BeTrue();
        scenarioRequest.Downscale.Should().NotBeNull();
        scenarioRequest.Downscale!.TargetHeight.Should().Be(576);
        scenarioRequest.Downscale.ContentProfile.Should().Be("film");
        scenarioRequest.Downscale.QualityProfile.Should().Be("default");
        scenarioRequest.Downscale.NoAutoSample.Should().BeTrue();
        scenarioRequest.Downscale.AutoSampleMode.Should().Be("fast");
        scenarioRequest.Downscale.Algorithm.Should().Be("bicubic");
        scenarioRequest.Downscale.Cq.Should().Be(24);
        scenarioRequest.Downscale.Maxrate.Should().Be(3.7m);
        scenarioRequest.Downscale.Bufsize.Should().Be(7.4m);
        scenarioRequest.MaxFramesPerSecond.Should().Be(40);
        scenarioRequest.NvencPreset.Should().Be("p6");
    }

    [Fact]
    public void TryParse_WhenToH264GpuOptionsAreProvided_PreservesScenarioArgs()
    {
        var actual = CliArgumentParser.TryParse(
            [
                "--scenario", "toh264gpu",
                "--input", @"C:\video\a.mkv",
                "--downscale", "576",
                "--keep-fps",
                "--downscale-algo", "lanczos",
                "--cq", "21",
                "--nvenc-preset", "p6",
                "--use-aq",
                "--aq-strength", "5",
                "--denoise",
                "--fix-timestamps",
                "--mkv"
            ],
            out var parsed,
            out var errorText);

        actual.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.Inputs.Should().ContainSingle().Which.Should().Be(@"C:\video\a.mkv");
        parsed.Scenario.Should().Be("toh264gpu");
        parsed.Info.Should().BeFalse();
        parsed.ScenarioArgs.Should().Equal(
            "--downscale", "576",
            "--keep-fps",
            "--downscale-algo", "lanczos",
            "--cq", "21",
            "--nvenc-preset", "p6",
            "--use-aq",
            "--aq-strength", "5",
            "--denoise",
            "--fix-timestamps",
            "--mkv");
    }

    [Fact]
    public void CreateScenario_WhenParsedArgsContainToH264GpuOptions_MapsRuntimeRequest()
    {
        var parsedOk = CliArgumentParser.TryParse(
            [
                "--scenario", "toh264gpu",
                "--input", @"C:\video\a.mkv",
                "--downscale", "576",
                "--keep-fps",
                "--downscale-algo", "lanczos",
                "--cq", "21",
                "--nvenc-preset", "p6",
                "--use-aq",
                "--aq-strength", "5",
                "--denoise",
                "--fix-timestamps",
                "--mkv"
            ],
            out var parsed,
            out var errorText);

        parsedOk.Should().BeTrue();
        errorText.Should().BeNull();

        var handler = new ToH264GpuCliScenarioHandler(new ToH264GpuInfoFormatter());
        var request = new CliTranscodeRequest(
            inputPath: @"C:\video\a.mkv",
            scenarioName: parsed.Scenario,
            info: parsed.Info,
            scenarioArgs: parsed.ScenarioArgs);

        var actual = handler.CreateScenario(request).Should().BeOfType<ToH264GpuScenario>().Subject;
        var scenarioRequest = actual.Request;

        scenarioRequest.DownscaleTargetHeight.Should().Be(576);
        scenarioRequest.KeepFramesPerSecond.Should().BeTrue();
        scenarioRequest.DownscaleAlgorithm.Should().Be("lanczos");
        scenarioRequest.Cq.Should().Be(21);
        scenarioRequest.NvencPreset.Should().Be("p6");
        scenarioRequest.UseAdaptiveQuantization.Should().BeTrue();
        scenarioRequest.AqStrength.Should().Be(5);
        scenarioRequest.Denoise.Should().BeTrue();
        scenarioRequest.FixTimestamps.Should().BeTrue();
        scenarioRequest.OutputMkv.Should().BeTrue();
    }

    [Fact]
    public void TryParse_WhenToH264GpuArgumentsAreInvalid_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            [
                "--scenario", "toh264gpu",
                "--input", @"C:\video\a.mkv",
                "--downscale", "480"
            ],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("--downscale must be 720 or 576.");
    }

    [Theory]
    [InlineData("tomkvgpu", "Do not use legacy scenario command tokens. Use --scenario tomkvgpu.")]
    [InlineData("toh264gpu", "Do not use legacy scenario command tokens. Use --scenario toh264gpu.")]
    [InlineData("--wat", "Scenario is required. Use --scenario <name>. Available scenarios: toh264gpu, tomkvgpu.")]
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
    [InlineData("--scenario")]
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
            ["--scenario", "--info"],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("--scenario requires a value.");
    }

    [Theory]
    [InlineData("--downscale")]
    [InlineData("--max-fps")]
    public void TryParse_WhenScenarioSpecificOptionValueIsMissing_ReturnsFalse(string optionName)
    {
        var actual = CliArgumentParser.TryParse(
            ["--scenario", "tomkvgpu", optionName],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be($"{optionName} requires a value.");
    }

    [Theory]
    [InlineData("--downscale", "abc", "--downscale must be an integer.")]
    [InlineData("--max-fps", "abc", "--max-fps must be an integer.")]
    [InlineData("--cq", "abc", "--cq must be an integer.")]
    [InlineData("--maxrate", "abc", "--maxrate must be a number.")]
    [InlineData("--bufsize", "abc", "--bufsize must be a number.")]
    public void TryParse_WhenOptionValueHasInvalidType_ReturnsFalse(
        string optionName,
        string value,
        string expectedError)
    {
        var actual = CliArgumentParser.TryParse(
            ["--scenario", "tomkvgpu", optionName, value],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be(expectedError);
    }

    [Theory]
    [InlineData("--downscale", "0", "--downscale must be greater than zero.")]
    [InlineData("--cq", "0", "--cq must be greater than zero.")]
    [InlineData("--maxrate", "0", "--maxrate must be greater than zero.")]
    [InlineData("--bufsize", "0", "--bufsize must be greater than zero.")]
    public void TryParse_WhenPositiveNumericOptionIsNonPositive_ReturnsFalse(
        string optionName,
        string value,
        string expectedError)
    {
        var actual = CliArgumentParser.TryParse(
            ["--scenario", "tomkvgpu", optionName, value],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be(expectedError);
    }

    [Fact]
    public void TryParse_WhenMaxFpsIsUnsupported_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            ["--scenario", "tomkvgpu", "--input", @"C:\video\a.mp4", "--max-fps", "55"],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("--max-fps must be one of: 50, 40, 30, 24.");
    }

    [Fact]
    public void TryParse_WhenScenarioIsMissing_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            ["--input", @"C:\video\a.mp4"],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("Scenario is required. Use --scenario <name>. Available scenarios: toh264gpu, tomkvgpu.");
    }

    [Fact]
    public void TryParse_WhenScenarioIsUnsupported_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            ["--scenario", "other", "--input", @"C:\video\a.mp4"],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("Unsupported scenario: other. Available scenarios: toh264gpu, tomkvgpu.");
    }
}
