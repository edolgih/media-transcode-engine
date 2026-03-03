using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Policy;

public sealed class TargetVideoCodecResolver
{
    public TargetVideoCodec Resolve(UnifiedTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ComputeMode.Equals(RequestContracts.Unified.CpuComputeMode, StringComparison.OrdinalIgnoreCase))
        {
            return TargetVideoCodec.H264;
        }

        if (request.PreferH264)
        {
            return TargetVideoCodec.H264;
        }

        if (!request.TargetContainer.Equals(RequestContracts.Unified.MkvContainer, StringComparison.OrdinalIgnoreCase))
        {
            return TargetVideoCodec.H264;
        }

        return TargetVideoCodec.Copy;
    }
}
