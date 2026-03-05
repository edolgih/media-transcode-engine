using FluentAssertions;
using MediaTranscodeEngine.Core.Codecs;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Tests.Codecs;

public class CodecProfileRoutingTests
{
    [Fact]
    public void SelectStrategyKey_WhenGpuAndCodecIsNotCopy_ReturnsCodecBasedKey()
    {
        var selector = new TranscodeRouteSelector(
            new TranscodeCatalog(),
            [CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu, "h265-gpu"]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H265VideoCodec);

        var strategyKey = selector.SelectStrategyKey(request);

        strategyKey.Should().Be("h265-gpu");
    }

    [Fact]
    public void SelectStrategyKey_WhenCustomProfileRegistered_UsesProfileStrategyKey()
    {
        var catalog = new TranscodeCatalog(
            profiles:
            [
                new TranscodeProfile(
                    codecId: "h266",
                    encoderBackend: RequestContracts.General.GpuEncoderBackend,
                    strategyKey: "h266-gpu",
                    supportedContainers: [RequestContracts.General.MkvContainer, RequestContracts.General.Mp4Container])
            ]);
        var selector = new TranscodeRouteSelector(catalog, ["h266-gpu"]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetVideoCodec: "h266");

        var strategyKey = selector.SelectStrategyKey(request);

        strategyKey.Should().Be("h266-gpu");
    }

    [Fact]
    public void SelectStrategyKey_WhenCustomCodecHasNoStrategy_ThrowsNotSupportedException()
    {
        var catalog = new TranscodeCatalog(
            profiles:
            [
                new TranscodeProfile(
                    codecId: "h266",
                    encoderBackend: RequestContracts.General.GpuEncoderBackend,
                    strategyKey: "h266-gpu",
                    supportedContainers: [RequestContracts.General.MkvContainer, RequestContracts.General.Mp4Container])
            ]);
        var selector = new TranscodeRouteSelector(catalog, [CodecExecutionKeys.Copy]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetVideoCodec: "h266");

        var act = () => selector.SelectStrategyKey(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*h266-gpu*");
    }
}
