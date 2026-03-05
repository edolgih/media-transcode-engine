using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Scenarios;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class CliScenarioPresetTests
{
    [Fact]
    public void Parse_WhenScenarioTomkvgpuProvided_AppliesPreset()
    {
        var ok = CliArgumentParser.TryParse(
            ["--input", "C:\\video\\movie.mp4", "--scenario", "tomkvgpu"],
            out var parsed,
            out var errorText);
        var merger = new ScenarioRequestMerger(new InMemoryScenarioPresetRepository());

        var merged = merger.Merge(parsed.RequestTemplate, parsed.ExplicitTemplateFields);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        merged.Scenario.Should().Be("tomkvgpu");
        merged.TargetContainer.Should().Be(RequestContracts.General.MkvContainer);
        merged.EncoderBackend.Should().Be(RequestContracts.General.GpuEncoderBackend);
        merged.PreferH264.Should().BeFalse();
        merged.TargetVideoCodec.Should().Be(RequestContracts.General.CopyVideoCodec);
    }

    [Fact]
    public void Parse_WhenScenarioAndExplicitCq_ExplicitWins()
    {
        var repository = new InMemoryScenarioPresetRepository(
        [
            new ScenarioPreset(
                Name: "custom",
                Cq: 24)
        ]);
        var ok = CliArgumentParser.TryParse(
            ["--input", "C:\\video\\movie.mp4", "--scenario", "custom", "--cq", "19"],
            out var parsed,
            out var errorText);
        var merger = new ScenarioRequestMerger(repository);

        var merged = merger.Merge(parsed.RequestTemplate, parsed.ExplicitTemplateFields);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        merged.Cq.Should().Be(19);
    }

    [Fact]
    public void Parse_WhenScenarioUnknown_ReturnsValidationError()
    {
        var ok = CliArgumentParser.TryParse(
            ["--input", "C:\\video\\movie.mp4", "--scenario", "missing"],
            out var parsed,
            out var errorText);
        var merger = new ScenarioRequestMerger(new InMemoryScenarioPresetRepository());

        var act = () => merger.Merge(parsed.RequestTemplate, parsed.ExplicitTemplateFields);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown scenario: missing*");
    }
}
