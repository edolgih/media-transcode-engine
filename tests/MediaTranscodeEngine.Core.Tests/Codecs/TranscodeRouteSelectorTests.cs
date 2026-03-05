using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Codecs;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Tests.Codecs;

public class TranscodeRouteSelectorTests
{
    [Fact]
    public void SelectStrategyKey_WhenCopyDescriptorMatches_ReturnsCopyStrategyKey()
    {
        var sut = new TranscodeRouteSelector(
            new TranscodeCatalog(),
            [CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu, "h265-gpu"]);
        var request = TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4");

        var actual = sut.SelectStrategyKey(request);

        actual.Should().Be(CodecExecutionKeys.Copy);
    }

    [Fact]
    public void SelectStrategyKey_WhenGpuEncodeMatches_ReturnsCodecBasedStrategyKey()
    {
        var sut = new TranscodeRouteSelector(
            new TranscodeCatalog(),
            [CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu, "h265-gpu"]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            TargetVideoCodec: RequestContracts.General.H264VideoCodec);

        var actual = sut.SelectStrategyKey(request);

        actual.Should().Be(CodecExecutionKeys.H264Gpu);
    }

    [Fact]
    public void SelectStrategyKey_WhenDescriptorMissing_ThrowsNotSupportedException()
    {
        var sut = new TranscodeRouteSelector(
            new TranscodeCatalog(),
            [CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu, "h265-gpu"]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.CpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H264VideoCodec);

        var act = () => sut.SelectStrategyKey(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*backend 'cpu'*codec 'h264'*");
    }

    [Fact]
    public void SelectStrategyKey_WhenContainerUnsupported_ThrowsNotSupportedException()
    {
        var sut = new TranscodeRouteSelector(
            new TranscodeCatalog(),
            [CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu, "h265-gpu"]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            TargetVideoCodec: RequestContracts.General.CopyVideoCodec,
            TargetContainer: RequestContracts.General.Mp4Container);

        var act = () => sut.SelectStrategyKey(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*codec 'copy' with container 'mp4'*");
    }

    [Fact]
    public void SelectStrategyKey_WhenStrategyNotRegistered_ThrowsNotSupportedException()
    {
        var sut = new TranscodeRouteSelector(
            new TranscodeCatalog(),
            [CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H265VideoCodec);

        var act = () => sut.SelectStrategyKey(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*codec 'h265'*encoder backend 'gpu'*");
    }
}
