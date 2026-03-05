using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Policy;
using MediaTranscodeEngine.Core.Classification;
using MediaTranscodeEngine.Core.Compatibility;
using MediaTranscodeEngine.Core.Quality;
using MediaTranscodeEngine.Core.Resolutions;
using MediaTranscodeEngine.Core.Sampling;

namespace MediaTranscodeEngine.Core.Execution;

public sealed class TranscodeExecutionPipeline : ITranscodeExecutionPipeline
{
    private readonly IReadOnlyDictionary<string, ICodecExecutionStrategy> _strategies;

    public TranscodeExecutionPipeline(
        IProbeReader probeReader,
        FfmpegCommandBuilder ffmpegCommandBuilder,
        H264CommandBuilder h264CommandBuilder,
        H264RemuxEligibilityPolicy remuxEligibilityPolicy,
        H264TimestampPolicy timestampPolicy,
        H264AudioPolicy audioPolicy,
        H264RateControlPolicy rateControlPolicy,
        ContainerPolicySelector containerPolicySelector,
        IInputClassifier inputClassifier,
        IResolutionPolicyRepository resolutionPolicyRepository,
        IQualityStrategy qualityStrategy,
        IAutoSamplingStrategy autoSamplingStrategy,
        IStreamCompatibilityPolicy streamCompatibilityPolicy,
        IAutoSampleReductionProvider? autoSampleReductionProvider = null,
        IEnumerable<ICodecExecutionStrategy>? codecExecutionStrategies = null)
    {
        var strategies = codecExecutionStrategies?.ToArray();
        if (strategies is null || strategies.Length == 0)
        {
            strategies =
            [
                new CopyCodecExecutionStrategy(
                    probeReader,
                    ffmpegCommandBuilder,
                    inputClassifier,
                    resolutionPolicyRepository,
                    qualityStrategy,
                    autoSamplingStrategy,
                    streamCompatibilityPolicy,
                    autoSampleReductionProvider),
                new H264GpuCodecExecutionStrategy(
                    probeReader,
                    h264CommandBuilder,
                    remuxEligibilityPolicy,
                    timestampPolicy,
                    audioPolicy,
                    rateControlPolicy,
                    containerPolicySelector,
                    inputClassifier,
                    resolutionPolicyRepository,
                    qualityStrategy,
                    autoSamplingStrategy,
                    streamCompatibilityPolicy,
                    autoSampleReductionProvider)
            ];
        }

        _strategies = strategies.ToDictionary(
            static strategy => strategy.Key,
            static strategy => strategy,
            StringComparer.OrdinalIgnoreCase);
    }

    public string ProcessByKey(string strategyKey, TranscodeRequest request)
    {
        return Process(strategyKey, request, probeOverride: null, useProbeOverride: false);
    }

    public string ProcessByKeyWithProbeResult(string strategyKey, TranscodeRequest request, ProbeResult? probe)
    {
        return Process(strategyKey, request, probe, useProbeOverride: true);
    }

    public string ProcessByKeyWithProbeJson(string strategyKey, TranscodeRequest request, string? probeJson)
    {
        return Process(strategyKey, request, ProbeJsonParser.Parse(probeJson), useProbeOverride: true);
    }

    private string Process(
        string strategyKey,
        TranscodeRequest request,
        ProbeResult? probeOverride,
        bool useProbeOverride)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_strategies.TryGetValue(strategyKey, out var strategy))
        {
            throw new InvalidOperationException($"Execution strategy '{strategyKey}' is not registered.");
        }

        return strategy.Process(request, probeOverride, useProbeOverride);
    }
}
