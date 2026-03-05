using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Scenarios;

namespace MediaTranscodeEngine.Core.Tests.Scenarios;

public class ScenarioRequestMergerTests
{
    [Fact]
    public void Merge_WhenPresetProvided_UsesPresetValues()
    {
        var repository = new InMemoryScenarioPresetRepository(
        [
            new ScenarioPreset(
                Name: "custom",
                TargetContainer: RequestContracts.General.Mp4Container,
                EncoderBackend: RequestContracts.General.CpuEncoderBackend,
                TargetVideoCodec: RequestContracts.General.H264VideoCodec,
                QualityProfile: "high")
        ]);
        var sut = new ScenarioRequestMerger(repository);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "custom");

        var actual = sut.Merge(request);

        actual.TargetContainer.Should().Be(RequestContracts.General.Mp4Container);
        actual.EncoderBackend.Should().Be(RequestContracts.General.CpuEncoderBackend);
        actual.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
        actual.QualityProfile.Should().Be("high");
    }

    [Fact]
    public void Merge_WhenExplicitOverridesPreset_UsesExplicitValues()
    {
        var repository = new InMemoryScenarioPresetRepository(
        [
            new ScenarioPreset(
                Name: "custom",
                TargetContainer: RequestContracts.General.Mp4Container,
                EncoderBackend: RequestContracts.General.CpuEncoderBackend,
                QualityProfile: "high")
        ]);
        var sut = new ScenarioRequestMerger(repository);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "custom",
            TargetContainer: RequestContracts.General.MkvContainer,
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            QualityProfile: "default");
        var explicitFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(RawTranscodeRequest.TargetContainer),
            nameof(RawTranscodeRequest.EncoderBackend),
            nameof(RawTranscodeRequest.QualityProfile)
        };

        var actual = sut.Merge(request, explicitFields);

        actual.TargetContainer.Should().Be(RequestContracts.General.MkvContainer);
        actual.EncoderBackend.Should().Be(RequestContracts.General.GpuEncoderBackend);
        actual.QualityProfile.Should().Be("default");
    }

    [Fact]
    public void Merge_WhenTargetVideoCodecExplicit_ExplicitWinsOverPreset()
    {
        var repository = new InMemoryScenarioPresetRepository(
        [
            new ScenarioPreset(
                Name: "custom",
                TargetVideoCodec: RequestContracts.General.H264VideoCodec)
        ]);
        var sut = new ScenarioRequestMerger(repository);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "custom",
            TargetVideoCodec: RequestContracts.General.CopyVideoCodec);
        var explicitFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(RawTranscodeRequest.TargetVideoCodec)
        };

        var actual = sut.Merge(request, explicitFields);

        actual.TargetVideoCodec.Should().Be(RequestContracts.General.CopyVideoCodec);
    }

    [Fact]
    public void Merge_WhenPresetUsesH265Codec_UsesPresetCodecValue()
    {
        var repository = new InMemoryScenarioPresetRepository(
        [
            new ScenarioPreset(
                Name: "custom",
                TargetVideoCodec: RequestContracts.General.H265VideoCodec)
        ]);
        var sut = new ScenarioRequestMerger(repository);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "custom");

        var actual = sut.Merge(request);

        actual.TargetVideoCodec.Should().Be(RequestContracts.General.H265VideoCodec);
    }

    [Fact]
    public void Merge_WhenNoPreset_UsesSystemDefaults()
    {
        var sut = new ScenarioRequestMerger(new InMemoryScenarioPresetRepository());
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4");

        var actual = sut.Merge(request);

        actual.Should().Be(request);
    }

    [Fact]
    public void Merge_WhenScenarioUnknown_ThrowsArgumentException()
    {
        var sut = new ScenarioRequestMerger(new InMemoryScenarioPresetRepository());
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "missing");

        var act = () => sut.Merge(request);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown scenario: missing*");
    }
}
