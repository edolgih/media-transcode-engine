using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Codecs;

public sealed class CopyRoute : ITranscodeRoute
{
    private readonly ITranscodeExecutionPipeline _pipeline;

    public CopyRoute(ITranscodeExecutionPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public bool CanHandle(TranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.EncoderBackend.Equals(RequestContracts.General.GpuEncoderBackend, StringComparison.OrdinalIgnoreCase) &&
               request.TargetVideoCodec.Equals(RequestContracts.General.CopyVideoCodec, StringComparison.OrdinalIgnoreCase) &&
               request.TargetContainer.Equals(RequestContracts.General.MkvContainer, StringComparison.OrdinalIgnoreCase);
    }

    public string Process(TranscodeRequest request)
    {
        return _pipeline.ProcessByKey(CodecExecutionKeys.Copy, request);
    }

    public string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe)
    {
        return _pipeline.ProcessByKeyWithProbeResult(CodecExecutionKeys.Copy, request, probe);
    }

    public string ProcessWithProbeJson(TranscodeRequest request, string? probeJson)
    {
        return _pipeline.ProcessByKeyWithProbeJson(CodecExecutionKeys.Copy, request, probeJson);
    }
}
