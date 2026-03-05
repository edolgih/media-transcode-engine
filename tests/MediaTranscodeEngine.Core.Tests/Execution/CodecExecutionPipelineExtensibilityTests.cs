using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Tests.Execution;

public class CodecExecutionPipelineExtensibilityTests
{
    [Fact]
    public void ProcessByKey_WhenCustomStrategyRegistered_UsesCustomStrategyWithoutPipelineApiChange()
    {
        const string strategyKey = "h265-gpu";
        const string expected = "custom h265 strategy output";
        var pipeline = new TranscodeExecutionPipeline(
            probeReader: null!,
            ffmpegCommandBuilder: null!,
            h264CommandBuilder: null!,
            remuxEligibilityPolicy: null!,
            timestampPolicy: null!,
            audioPolicy: null!,
            rateControlPolicy: null!,
            containerPolicySelector: null!,
            inputClassifier: null!,
            resolutionPolicyRepository: null!,
            qualityStrategy: null!,
            autoSamplingStrategy: null!,
            streamCompatibilityPolicy: null!,
            codecExecutionStrategies:
            [
                new NamedStrategy(strategyKey, expected)
            ]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            TargetVideoCodec: RequestContracts.General.H265VideoCodec);

        var actual = pipeline.ProcessByKey(strategyKey, request);

        actual.Should().Be(expected);
    }

    private sealed class NamedStrategy : ICodecExecutionStrategy
    {
        private readonly string _result;

        public NamedStrategy(string key, string result)
        {
            Key = key;
            _result = result;
        }

        public string Key { get; }

        public string Process(TranscodeRequest request, ProbeResult? probeOverride, bool useProbeOverride)
        {
            _ = (request, probeOverride, useProbeOverride);
            return _result;
        }
    }
}
