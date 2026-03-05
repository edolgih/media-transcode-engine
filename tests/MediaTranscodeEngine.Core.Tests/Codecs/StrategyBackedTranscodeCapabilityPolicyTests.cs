using FluentAssertions;
using MediaTranscodeEngine.Core.Codecs;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Tests.Codecs;

public class TranscodeRouteSelectorCapabilityTests
{
    [Fact]
    public void SelectStrategyKey_WhenGpuCopyToMkvAndCopyStrategyRegistered_ReturnsCopy()
    {
        var selector = new TranscodeRouteSelector(
            new TranscodeCatalog(),
            [CodecExecutionKeys.Copy]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetContainer: RequestContracts.General.MkvContainer,
            TargetVideoCodec: RequestContracts.General.CopyVideoCodec);

        var strategyKey = selector.SelectStrategyKey(request);

        strategyKey.Should().Be(CodecExecutionKeys.Copy);
    }

    [Fact]
    public void SelectStrategyKey_WhenGpuCopyToMp4_ThrowsNotSupported()
    {
        var selector = new TranscodeRouteSelector(
            new TranscodeCatalog(),
            [CodecExecutionKeys.Copy]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetContainer: RequestContracts.General.Mp4Container,
            TargetVideoCodec: RequestContracts.General.CopyVideoCodec);

        var act = () => selector.SelectStrategyKey(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*copy*mp4*");
    }

    [Fact]
    public void SelectStrategyKey_WhenGpuH265AndStrategyMissing_ThrowsNotSupported()
    {
        var selector = new TranscodeRouteSelector(
            new TranscodeCatalog(),
            [CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H265VideoCodec);

        var act = () => selector.SelectStrategyKey(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*h265*g*");
    }

    [Fact]
    public void SelectStrategyKey_WhenGpuH265AndStrategyRegistered_ReturnsSupported()
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
    public void SelectStrategyKey_WhenCustomProfileAndStrategyRegistered_ReturnsSupported()
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
            TargetContainer: RequestContracts.General.Mp4Container,
            TargetVideoCodec: "h266");

        var strategyKey = selector.SelectStrategyKey(request);

        strategyKey.Should().Be("h266-gpu");
    }

    [Fact]
    public void SelectStrategyKey_WhenCpuBackend_ThrowsNotSupported()
    {
        var selector = new TranscodeRouteSelector(
            new TranscodeCatalog(),
            [CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.CpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H264VideoCodec);

        var act = () => selector.SelectStrategyKey(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*cpu*");
    }
}
