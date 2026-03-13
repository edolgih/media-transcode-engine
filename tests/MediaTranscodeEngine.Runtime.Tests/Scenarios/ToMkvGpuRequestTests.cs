using FluentAssertions;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.Tests.Scenarios;

/*
Это тесты request-модели tomkvgpu.
Они проверяют нормализацию supported values и сценарные инварианты этой legacy-модели.
*/
/// <summary>
/// Verifies normalization and invariants of the ToMkvGpu request model.
/// </summary>
public sealed class ToMkvGpuRequestTests
{
    [Fact]
    public void Constructor_WithValidOptions_NormalizesValues()
    {
        var request = new ToMkvGpuRequest(
            overlayBackground: true,
            synchronizeAudio: true,
            keepSource: true,
            videoSettings: new VideoSettingsRequest(
                contentProfile: "Film",
                qualityProfile: "Default",
                autoSampleMode: "Fast",
                cq: 24,
                maxrate: 3.7m,
                bufsize: 7.4m),
            downscale: new DownscaleRequest(576, "Bicubic"),
            nvencPreset: "P6",
            maxFramesPerSecond: 40);

        request.KeepSource.Should().BeTrue();
        request.OverlayBackground.Should().BeTrue();
        request.SynchronizeAudio.Should().BeTrue();
        request.Downscale.Should().NotBeNull();
        request.Downscale!.TargetHeight.Should().Be(576);
        request.Downscale.Algorithm.Should().Be("bicubic");
        request.VideoSettings.Should().NotBeNull();
        request.VideoSettings!.ContentProfile.Should().Be("film");
        request.VideoSettings.QualityProfile.Should().Be("default");
        request.VideoSettings.AutoSampleMode.Should().Be("fast");
        request.VideoSettings.Cq.Should().Be(24);
        request.VideoSettings.Maxrate.Should().Be(3.7m);
        request.VideoSettings.Bufsize.Should().Be(7.4m);
        request.NvencPreset.Should().Be("p6");
        request.MaxFramesPerSecond.Should().Be(40);
    }

    [Fact]
    public void Constructor_WhenNvencPresetIsUnsupported_Throws()
    {
        Action action = static () => _ = new ToMkvGpuRequest(nvencPreset: "p8");

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("nvencPreset");
    }

    [Fact]
    public void DownscaleRequest_WhenAlgorithmIsUnsupported_Throws()
    {
        Action action = static () => _ = new DownscaleRequest(576, "nearest");

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("algorithm");
    }
}
