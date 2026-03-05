using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Codecs;

public sealed class TranscodeRouteSelector
{
    private readonly TranscodeCatalog _catalog;
    private readonly HashSet<string> _strategyKeys;

    public TranscodeRouteSelector(
        TranscodeCatalog catalog,
        IEnumerable<string> strategyKeys)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(strategyKeys);
        _catalog = catalog;
        _strategyKeys = new HashSet<string>(strategyKeys, StringComparer.OrdinalIgnoreCase);
    }

    public string SelectStrategyKey(TranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TargetVideoCodec.Equals(RequestContracts.General.CopyVideoCodec, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.TargetContainer.Equals(RequestContracts.General.MkvContainer, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"Unsupported transcode combination: codec 'copy' with container '{request.TargetContainer}'.");
            }

            if (!_strategyKeys.Contains(CodecExecutionKeys.Copy))
            {
                throw new NotSupportedException(
                    $"Unsupported transcode combination: codec 'copy' has no registered strategy ('{CodecExecutionKeys.Copy}').");
            }

            return CodecExecutionKeys.Copy;
        }

        if (!_catalog.TryGetCodec(request.TargetVideoCodec, out var codecDescriptor))
        {
            throw new NotSupportedException(
                $"Unsupported transcode combination: encoder backend '{request.EncoderBackend}', codec '{request.TargetVideoCodec}' and container '{request.TargetContainer}'.");
        }

        if (!codecDescriptor.SupportsContainer(request.TargetContainer))
        {
            throw new NotSupportedException(
                $"Unsupported transcode combination: codec '{request.TargetVideoCodec}' with backend '{request.EncoderBackend}' does not support container '{request.TargetContainer}'.");
        }

        if (!_catalog.TryGetBackend(request.EncoderBackend, out var backendDescriptor))
        {
            throw new NotSupportedException(
                $"Unsupported transcode combination: encoder backend '{request.EncoderBackend}' is not registered.");
        }

        if (!backendDescriptor.TryGetStrategyKey(request.TargetVideoCodec, out var strategyKey))
        {
            throw new NotSupportedException(
                $"Unsupported transcode combination: codec '{request.TargetVideoCodec}' is not supported by backend '{request.EncoderBackend}'.");
        }

        if (!_strategyKeys.Contains(strategyKey))
        {
            throw new NotSupportedException(
                $"Unsupported transcode combination: codec '{request.TargetVideoCodec}', encoder backend '{request.EncoderBackend}' has no registered strategy ('{strategyKey}').");
        }

        return strategyKey;
    }
}
