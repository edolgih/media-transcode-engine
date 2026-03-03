using MediaTranscodeEngine.Core.Engine.Behaviors;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Engine;

public sealed class UnifiedTranscodeEngine
{
    private readonly TargetVideoCodecResolver _codecResolver;
    private readonly TranscodeBehaviorSelector _behaviorSelector;

    public UnifiedTranscodeEngine(
        TargetVideoCodecResolver codecResolver,
        TranscodeBehaviorSelector behaviorSelector)
    {
        _codecResolver = codecResolver;
        _behaviorSelector = behaviorSelector;
    }

    public string Process(UnifiedTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var targetCodec = _codecResolver.Resolve(request);
        var behavior = _behaviorSelector.Select(targetCodec, request);
        return behavior.Process(request);
    }

    public string ProcessWithProbeResult(UnifiedTranscodeRequest request, ProbeResult? probe)
    {
        ArgumentNullException.ThrowIfNull(request);

        var targetCodec = _codecResolver.Resolve(request);
        var behavior = _behaviorSelector.Select(targetCodec, request);
        return behavior.ProcessWithProbeResult(request, probe);
    }

    public string ProcessWithProbeJson(UnifiedTranscodeRequest request, string? probeJson)
    {
        ArgumentNullException.ThrowIfNull(request);

        var targetCodec = _codecResolver.Resolve(request);
        var behavior = _behaviorSelector.Select(targetCodec, request);
        return behavior.ProcessWithProbeJson(request, probeJson);
    }
}
