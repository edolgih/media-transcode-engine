using FluentAssertions;
using MediaTranscodeEngine.Core.Codecs;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Tests.Codecs;

public class GpuEncodeRouteTests
{
    [Fact]
    public void CanHandle_WhenGpuAndCodecIsNotCopy_ReturnsTrue()
    {
        var route = new GpuEncodeRoute(new SpyPipeline());
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H265VideoCodec);

        var actual = route.CanHandle(request);

        actual.Should().BeTrue();
    }

    [Fact]
    public void Process_WhenCodecIsH265_UsesCodecBasedStrategyKey()
    {
        var pipeline = new SpyPipeline();
        var route = new GpuEncodeRoute(pipeline);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H265VideoCodec);

        var actual = route.Process(request);

        actual.Should().Be("h265-gpu");
        pipeline.LastStrategyKey.Should().Be("h265-gpu");
    }

    private sealed class SpyPipeline : ITranscodeExecutionPipeline
    {
        public string? LastStrategyKey { get; private set; }

        public string ProcessByKey(string strategyKey, TranscodeRequest request)
        {
            _ = request;
            LastStrategyKey = strategyKey;
            return strategyKey;
        }

        public string ProcessByKeyWithProbeResult(string strategyKey, TranscodeRequest request, ProbeResult? probe)
        {
            _ = (request, probe);
            LastStrategyKey = strategyKey;
            return strategyKey;
        }

        public string ProcessByKeyWithProbeJson(string strategyKey, TranscodeRequest request, string? probeJson)
        {
            _ = (request, probeJson);
            LastStrategyKey = strategyKey;
            return strategyKey;
        }
    }
}
