using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Scenarios;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class WrapperPresetContractTests
{
    [Fact]
    public void Run_WhenPresetUsed_ForwardsResolvedRequestToCore()
    {
        var repository = new InMemoryScenarioPresetRepository(
        [
            new ScenarioPreset(
                Name: "custom",
                TargetContainer: RequestContracts.General.Mp4Container,
                EncoderBackend: RequestContracts.General.CpuEncoderBackend,
                Cq: 24)
        ]);
        var ok = CliArgumentParser.TryParse(
            ["--input", "C:\\video\\movie.mp4", "--scenario", "custom"],
            out var parsed,
            out var errorText);
        var merger = new ScenarioRequestMerger(repository);

        var mergedTemplate = merger.Merge(parsed.RequestTemplate, parsed.ExplicitTemplateFields);
        var domainRequest = CliRequestMappers.BuildRequest(mergedTemplate, "C:\\video\\movie.mp4");

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        domainRequest.TargetContainer.Should().Be(RequestContracts.General.Mp4Container);
        domainRequest.EncoderBackend.Should().Be(RequestContracts.General.CpuEncoderBackend);
        domainRequest.Cq.Should().Be(24);
    }
}
