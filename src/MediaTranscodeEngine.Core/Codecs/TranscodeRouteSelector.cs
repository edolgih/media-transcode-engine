using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Codecs;

public sealed class TranscodeRouteSelector
{
    private readonly IReadOnlyList<ITranscodeRoute> _routes;
    private readonly ITranscodeCapabilityPolicy _capabilityPolicy;

    public TranscodeRouteSelector(
        IEnumerable<ITranscodeRoute> routes,
        ITranscodeCapabilityPolicy capabilityPolicy)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(capabilityPolicy);
        _routes = routes.ToArray();
        _capabilityPolicy = capabilityPolicy;
    }

    public ITranscodeRoute Select(TranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var capability = _capabilityPolicy.Decide(request);
        if (!capability.IsSupported)
        {
            throw new NotSupportedException(
                capability.Reason ??
                $"Unsupported transcode combination: encoder backend '{request.EncoderBackend}', codec '{request.TargetVideoCodec}' and container '{request.TargetContainer}'.");
        }

        var route = _routes.FirstOrDefault(candidate => candidate.CanHandle(request));
        if (route is null)
        {
            throw new InvalidOperationException(
                $"No transcode route was found for encoder backend '{request.EncoderBackend}', codec '{request.TargetVideoCodec}' and container '{request.TargetContainer}'.");
        }

        return route;
    }
}
