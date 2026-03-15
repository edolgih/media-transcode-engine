using FluentAssertions;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Parsing;
using Transcode.Cli.Core.Scenarios;
using Transcode.Scenarios.ToH264Gpu.Cli;
using Transcode.Scenarios.ToH264Gpu.Core;
using Transcode.Scenarios.ToMkvGpu.Cli;
using Transcode.Scenarios.ToMkvGpu.Core;

namespace Transcode.Cli.Tests.Parsing;

/*
Это тесты разбора CLI-аргументов.
Они проверяют, что transport-слой сохраняет сценарные токены и правильно маппит их в runtime-request.
*/
/// <summary>
/// Verifies CLI argument parsing and scenario-specific request binding behavior.
/// </summary>
public sealed class CliParsingTests
{
    [Fact]
    public void TryParse_WhenToMkvGpuProfileOptionsAreProvided_BindsScenarioInputOnce()
    {
        var actual = CliArgumentParser.TryParse(
            [
                "--scenario", "tomkvgpu",
                "--input", @"C:\video\a.mp4",
                "--downscale", "576",
                "--content-profile", "Anime",
                "--quality-profile", "High",
                "--autosample-mode", "Hybrid",
                "--downscale-algo", "Lanczos",
                "--max-fps", "50",
                "--cq", "23",
                "--maxrate", "3.4",
                "--bufsize", "6.8",
                "--nvenc-preset", "P5"
            ],
            CreateRegistry(),
            out var parsed,
            out var errorText);

        actual.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.Inputs.Should().ContainSingle().Which.Should().Be(@"C:\video\a.mp4");
        parsed.Scenario.Should().Be("tomkvgpu");
        parsed.Info.Should().BeFalse();
        parsed.ScenarioArgCount.Should().Be(20);
        var scenarioInput = parsed.ScenarioInput.Should().BeOfType<ToMkvGpuRequest>().Subject;
        scenarioInput.Downscale.Should().NotBeNull();
        scenarioInput.VideoSettings.Should().NotBeNull();
        var downscale = scenarioInput.Downscale!;
        var videoSettings = scenarioInput.VideoSettings!;
        downscale.TargetHeight.Should().Be(576);
        videoSettings.ContentProfile.Should().Be("anime");
        videoSettings.QualityProfile.Should().Be("high");
        videoSettings.AutoSampleMode.Should().Be("hybrid");
        downscale.Algorithm.Should().Be("lanczos");
        videoSettings.Cq.Should().Be(23);
        videoSettings.Maxrate.Should().Be(3.4m);
        videoSettings.Bufsize.Should().Be(6.8m);
        scenarioInput.MaxFramesPerSecond.Should().Be(50);
        scenarioInput.NvencPreset.Should().Be("p5");
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
                "--autosample-mode", "Fast",
                "--downscale-algo", "Bicubic",
                "--max-fps", "40",
                "--cq", "24",
                "--maxrate", "3.7",
                "--bufsize", "7.4",
                "--nvenc-preset", "P6"
            ],
            CreateRegistry(),
            out var parsed,
            out var errorText);

        parsedOk.Should().BeTrue();
        errorText.Should().BeNull();

        var request = new CliTranscodeRequest(
            inputPath: @"C:\video\a.mp4",
            scenarioName: parsed.Scenario,
            info: parsed.Info,
            scenarioInput: parsed.ScenarioInput,
            scenarioArgCount: parsed.ScenarioArgCount);

        var actual = new ToMkvGpuCliScenarioHandler(new ToMkvGpuInfoFormatter())
            .CreateScenario(request)
            .Should()
            .BeOfType<ToMkvGpuScenario>()
            .Subject;
        var scenarioRequest = actual.Request;

        request.InputPath.Should().Be(@"C:\video\a.mp4");
        scenarioRequest.KeepSource.Should().BeTrue();
        scenarioRequest.OverlayBackground.Should().BeTrue();
        scenarioRequest.SynchronizeAudio.Should().BeTrue();
        scenarioRequest.VideoSettings.Should().NotBeNull();
        scenarioRequest.Downscale.Should().NotBeNull();
        scenarioRequest.Downscale!.TargetHeight.Should().Be(576);
        scenarioRequest.VideoSettings.ContentProfile.Should().Be("film");
        scenarioRequest.VideoSettings.QualityProfile.Should().Be("default");
        scenarioRequest.VideoSettings.AutoSampleMode.Should().Be("fast");
        scenarioRequest.Downscale.Algorithm.Should().Be("bicubic");
        scenarioRequest.VideoSettings.Cq.Should().Be(24);
        scenarioRequest.VideoSettings.Maxrate.Should().Be(3.7m);
        scenarioRequest.VideoSettings.Bufsize.Should().Be(7.4m);
        scenarioRequest.MaxFramesPerSecond.Should().Be(40);
        scenarioRequest.NvencPreset.Should().Be("p6");
    }

    [Fact]
    public void TryParse_WhenToH264GpuOptionsAreProvided_BindsScenarioInputOnce()
    {
        var actual = CliArgumentParser.TryParse(
            [
                "--scenario", "toh264gpu",
                "--input", @"C:\video\a.mkv",
                "--keep-source",
                "--downscale", "576",
                "--keep-fps",
                "--content-profile", "film",
                "--quality-profile", "default",
                "--autosample-mode", "fast",
                "--downscale-algo", "lanczos",
                "--cq", "21",
                "--nvenc-preset", "p6",
                "--denoise",
                "--sync-audio",
                "--mkv"
            ],
            CreateRegistry(),
            out var parsed,
            out var errorText);

        actual.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.Inputs.Should().ContainSingle().Which.Should().Be(@"C:\video\a.mkv");
        parsed.Scenario.Should().Be("toh264gpu");
        parsed.Info.Should().BeFalse();
        parsed.ScenarioArgCount.Should().Be(19);
        var scenarioInput = parsed.ScenarioInput.Should().BeOfType<ToH264GpuRequest>().Subject;
        scenarioInput.KeepSource.Should().BeTrue();
        scenarioInput.Downscale.Should().NotBeNull();
        scenarioInput.Downscale!.TargetHeight.Should().Be(576);
        scenarioInput.KeepFramesPerSecond.Should().BeTrue();
        scenarioInput.VideoSettings!.ContentProfile.Should().Be("film");
        scenarioInput.VideoSettings.QualityProfile.Should().Be("default");
        scenarioInput.VideoSettings.AutoSampleMode.Should().Be("fast");
        scenarioInput.Downscale.Algorithm.Should().Be("lanczos");
        scenarioInput.VideoSettings.Cq.Should().Be(21);
        scenarioInput.NvencPreset.Should().Be("p6");
        scenarioInput.Denoise.Should().BeTrue();
        scenarioInput.SynchronizeAudio.Should().BeTrue();
        scenarioInput.OutputMkv.Should().BeTrue();
    }

    [Fact]
    public void CreateScenario_WhenParsedArgsContainToH264GpuOptions_MapsRuntimeRequest()
    {
        var parsedOk = CliArgumentParser.TryParse(
            [
                "--scenario", "toh264gpu",
                "--input", @"C:\video\a.mkv",
                "--keep-source",
                "--downscale", "576",
                "--keep-fps",
                "--content-profile", "film",
                "--quality-profile", "default",
                "--autosample-mode", "fast",
                "--downscale-algo", "lanczos",
                "--cq", "21",
                "--nvenc-preset", "p6",
                "--denoise",
                "--sync-audio",
                "--mkv"
            ],
            CreateRegistry(),
            out var parsed,
            out var errorText);

        parsedOk.Should().BeTrue();
        errorText.Should().BeNull();

        var request = new CliTranscodeRequest(
            inputPath: @"C:\video\a.mkv",
            scenarioName: parsed.Scenario,
            info: parsed.Info,
            scenarioInput: parsed.ScenarioInput,
            scenarioArgCount: parsed.ScenarioArgCount);

        var actual = new ToH264GpuCliScenarioHandler(new ToH264GpuInfoFormatter())
            .CreateScenario(request)
            .Should()
            .BeOfType<ToH264GpuScenario>()
            .Subject;
        var scenarioRequest = actual.Request;

        scenarioRequest.KeepSource.Should().BeTrue();
        scenarioRequest.Downscale.Should().NotBeNull();
        scenarioRequest.Downscale!.TargetHeight.Should().Be(576);
        scenarioRequest.KeepFramesPerSecond.Should().BeTrue();
        scenarioRequest.VideoSettings.Should().NotBeNull();
        scenarioRequest.VideoSettings!.ContentProfile.Should().Be("film");
        scenarioRequest.VideoSettings.QualityProfile.Should().Be("default");
        scenarioRequest.VideoSettings.AutoSampleMode.Should().Be("fast");
        scenarioRequest.Downscale.Algorithm.Should().Be("lanczos");
        scenarioRequest.VideoSettings.Cq.Should().Be(21);
        scenarioRequest.NvencPreset.Should().Be("p6");
        scenarioRequest.Denoise.Should().BeTrue();
        scenarioRequest.SynchronizeAudio.Should().BeTrue();
        scenarioRequest.OutputMkv.Should().BeTrue();
    }

    [Fact]
    public void CreateScenario_WhenTomkvgpuUsesSupportedHdDownscale_MapsRuntimeRequest()
    {
        var parsedOk = CliArgumentParser.TryParse(
            [
                "--scenario", "tomkvgpu",
                "--input", @"C:\video\a.mkv",
                "--downscale", "720"
            ],
            CreateRegistry(),
            out var parsed,
            out var errorText);

        parsedOk.Should().BeTrue();
        errorText.Should().BeNull();
        parsed.Should().NotBeNull();
        var request = new CliTranscodeRequest(
            inputPath: @"C:\video\a.mkv",
            scenarioName: parsed!.Scenario,
            info: parsed.Info,
            scenarioInput: parsed.ScenarioInput,
            scenarioArgCount: parsed.ScenarioArgCount);

        var scenario = new ToMkvGpuCliScenarioHandler(new ToMkvGpuInfoFormatter())
            .CreateScenario(request)
            .Should()
            .BeOfType<ToMkvGpuScenario>()
            .Subject;

        scenario.Request.Downscale.Should().NotBeNull();
        scenario.Request.Downscale!.TargetHeight.Should().Be(720);
    }

    [Theory]
    [InlineData(480)]
    [InlineData(424)]
    public void TryParse_WhenToH264GpuUsesSupportedSdDownscale_ReturnsScenarioRequest(int targetHeight)
    {
        var actual = CliArgumentParser.TryParse(
            [
                "--scenario", "toh264gpu",
                "--input", @"C:\video\a.mkv",
                "--downscale", targetHeight.ToString()
            ],
            CreateRegistry(),
            out var parseResult,
            out var errorText);

        actual.Should().BeTrue();
        errorText.Should().BeNull();
        parseResult.Should().NotBeNull();
        var parsed = parseResult!;
        var request = new CliTranscodeRequest(
            inputPath: @"C:\video\a.mkv",
            scenarioName: parsed.Scenario,
            info: parsed.Info,
            scenarioInput: parsed.ScenarioInput,
            scenarioArgCount: parsed.ScenarioArgCount);
        var scenario = new ToH264GpuCliScenarioHandler(new ToH264GpuInfoFormatter())
            .CreateScenario(request)
            .Should()
            .BeOfType<ToH264GpuScenario>()
            .Subject;

        scenario.Request.Downscale.Should().NotBeNull();
        scenario.Request.Downscale!.TargetHeight.Should().Be(targetHeight);
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
            CreateRegistry(),
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
            CreateRegistry(),
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
            CreateRegistry(),
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
            CreateRegistry(),
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
            CreateRegistry(),
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
            CreateRegistry(),
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
            CreateRegistry(),
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("--max-fps must be one of: 50, 40, 30, 24.");
    }

    [Fact]
    public void TryParse_WhenTomkvgpuDownscaleIsUnsupported_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            ["--scenario", "tomkvgpu", "--input", @"C:\video\a.mp4", "--downscale", "360"],
            CreateRegistry(),
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("--downscale must be one of: 720, 576, 480, 424.");
    }

    [Fact]
    public void TryParse_WhenTomkvgpuDownscaleAlgorithmIsUnsupported_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            ["--scenario", "tomkvgpu", "--input", @"C:\video\a.mp4", "--downscale", "576", "--downscale-algo", "nearest"],
            CreateRegistry(),
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("--downscale-algo must be one of: bilinear, bicubic, lanczos.");
    }

    [Fact]
    public void TryParse_WhenToH264GpuDownscaleAlgorithmIsUnsupported_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            ["--scenario", "toh264gpu", "--input", @"C:\video\a.mp4", "--downscale", "576", "--downscale-algo", "nearest"],
            CreateRegistry(),
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("--downscale-algo must be one of: bilinear, bicubic, lanczos.");
    }

    [Fact]
    public void TryParse_WhenToH264GpuCqIsAboveSupportedRange_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            ["--scenario", "toh264gpu", "--input", @"C:\video\a.mp4", "--cq", "52"],
            CreateRegistry(),
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("--cq must be an integer from 1 to 51.");
    }

    [Fact]
    public void TryParse_WhenScenarioIsMissing_ReturnsFalse()
    {
        var actual = CliArgumentParser.TryParse(
            ["--input", @"C:\video\a.mp4"],
            CreateRegistry(),
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
            CreateRegistry(),
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("Unsupported scenario: other. Available scenarios: toh264gpu, tomkvgpu.");
    }
    private static CliScenarioRegistry CreateRegistry()
    {
        return new CliScenarioRegistry(
            [
                new ToH264GpuCliScenarioHandler(new ToH264GpuInfoFormatter()),
                new ToMkvGpuCliScenarioHandler(new ToMkvGpuInfoFormatter())
            ]);
    }
}
