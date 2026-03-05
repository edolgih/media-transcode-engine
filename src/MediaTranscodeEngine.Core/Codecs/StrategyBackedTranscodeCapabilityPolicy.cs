using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Codecs;

public sealed class StrategyBackedTranscodeCapabilityPolicy : ITranscodeCapabilityPolicy
{
    private readonly HashSet<string> _strategyKeys;

    public StrategyBackedTranscodeCapabilityPolicy(IEnumerable<string> strategyKeys)
    {
        ArgumentNullException.ThrowIfNull(strategyKeys);
        _strategyKeys = new HashSet<string>(strategyKeys, StringComparer.OrdinalIgnoreCase);
    }

    public TranscodeCapabilityDecision Decide(TranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.EncoderBackend.Equals(RequestContracts.General.GpuEncoderBackend, StringComparison.OrdinalIgnoreCase))
        {
            return Unsupported($"Unsupported transcode combination: encoder backend '{request.EncoderBackend}', codec '{request.TargetVideoCodec}' and container '{request.TargetContainer}'.");
        }

        if (request.TargetVideoCodec.Equals(RequestContracts.General.CopyVideoCodec, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.TargetContainer.Equals(RequestContracts.General.MkvContainer, StringComparison.OrdinalIgnoreCase))
            {
                return Unsupported($"Unsupported transcode combination: codec 'copy' requires container '{RequestContracts.General.MkvContainer}'.");
            }

            return _strategyKeys.Contains(CodecExecutionKeys.Copy)
                ? Supported()
                : Unsupported($"Unsupported transcode combination: execution strategy '{CodecExecutionKeys.Copy}' is not registered.");
        }

        var strategyKey = CodecExecutionKeys.BuildGpuEncodeKey(request.TargetVideoCodec);
        return _strategyKeys.Contains(strategyKey)
            ? Supported()
            : Unsupported($"Unsupported transcode combination: codec '{request.TargetVideoCodec}', encoder backend '{request.EncoderBackend}' has no registered strategy ('{strategyKey}').");
    }

    private static TranscodeCapabilityDecision Supported()
    {
        return new TranscodeCapabilityDecision(IsSupported: true);
    }

    private static TranscodeCapabilityDecision Unsupported(string reason)
    {
        return new TranscodeCapabilityDecision(IsSupported: false, Reason: reason);
    }
}
