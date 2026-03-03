namespace MediaTranscodeEngine.Core.Engine.Behaviors;

public sealed class TranscodeBehaviorSelector
{
    private readonly IReadOnlyList<ITranscodeBehavior> _behaviors;

    public TranscodeBehaviorSelector(IEnumerable<ITranscodeBehavior> behaviors)
    {
        ArgumentNullException.ThrowIfNull(behaviors);
        _behaviors = behaviors.ToArray();
    }

    public ITranscodeBehavior Select(TargetVideoCodec targetCodec, UnifiedTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var behavior = _behaviors.FirstOrDefault(candidate => candidate.CanHandle(targetCodec, request));
        if (behavior is null)
        {
            throw new InvalidOperationException(
                $"No transcode behavior registered for codec '{targetCodec}' and compute mode '{request.ComputeMode}'.");
        }

        return behavior;
    }
}
