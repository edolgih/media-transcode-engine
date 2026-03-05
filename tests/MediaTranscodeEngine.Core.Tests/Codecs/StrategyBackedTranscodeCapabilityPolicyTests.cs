using FluentAssertions;
using MediaTranscodeEngine.Core.Codecs;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Tests.Codecs;

public class StrategyBackedTranscodeCapabilityPolicyTests
{
    [Fact]
    public void Decide_WhenGpuCopyToMkvAndCopyStrategyRegistered_ReturnsSupported()
    {
        var policy = new StrategyBackedTranscodeCapabilityPolicy([CodecExecutionKeys.Copy]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetContainer: RequestContracts.General.MkvContainer,
            TargetVideoCodec: RequestContracts.General.CopyVideoCodec);

        var decision = policy.Decide(request);

        decision.IsSupported.Should().BeTrue();
        decision.Reason.Should().BeNull();
    }

    [Fact]
    public void Decide_WhenGpuCopyToMp4_ReturnsUnsupported()
    {
        var policy = new StrategyBackedTranscodeCapabilityPolicy([CodecExecutionKeys.Copy]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetContainer: RequestContracts.General.Mp4Container,
            TargetVideoCodec: RequestContracts.General.CopyVideoCodec);

        var decision = policy.Decide(request);

        decision.IsSupported.Should().BeFalse();
        decision.Reason.Should().Contain("copy").And.Contain("mkv");
    }

    [Fact]
    public void Decide_WhenGpuH265AndStrategyMissing_ReturnsUnsupported()
    {
        var policy = new StrategyBackedTranscodeCapabilityPolicy([CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H265VideoCodec);

        var decision = policy.Decide(request);

        decision.IsSupported.Should().BeFalse();
        decision.Reason.Should().Contain("h265").And.Contain("gpu");
    }

    [Fact]
    public void Decide_WhenGpuH265AndStrategyRegistered_ReturnsSupported()
    {
        var policy = new StrategyBackedTranscodeCapabilityPolicy([CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu, "h265-gpu"]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H265VideoCodec);

        var decision = policy.Decide(request);

        decision.IsSupported.Should().BeTrue();
        decision.Reason.Should().BeNull();
    }

    [Fact]
    public void Decide_WhenCpuBackend_ReturnsUnsupported()
    {
        var policy = new StrategyBackedTranscodeCapabilityPolicy([CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.CpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H264VideoCodec);

        var decision = policy.Decide(request);

        decision.IsSupported.Should().BeFalse();
        decision.Reason.Should().Contain("cpu");
    }
}
