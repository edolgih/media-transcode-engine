namespace MediaTranscodeEngine.Core.Engine.Behaviors;

public sealed class H264CpuNotImplementedBehavior : ITranscodeBehavior
{
    public bool CanHandle(TargetVideoCodec targetCodec, UnifiedTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return targetCodec is TargetVideoCodec.H264 &&
               request.ComputeMode.Equals(RequestContracts.Unified.CpuComputeMode, StringComparison.OrdinalIgnoreCase);
    }

    public string Process(UnifiedTranscodeRequest request)
    {
        return BuildNotImplementedLine(request);
    }

    public string ProcessWithProbeResult(UnifiedTranscodeRequest request, ProbeResult? probe)
    {
        return BuildNotImplementedLine(request);
    }

    public string ProcessWithProbeJson(UnifiedTranscodeRequest request, string? probeJson)
    {
        return BuildNotImplementedLine(request);
    }

    private static string BuildNotImplementedLine(UnifiedTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var inputPath = request.InputPath;
        if (request.Info)
        {
            return $"{Path.GetFileName(inputPath)}: [h264 cpu not implemented]";
        }

        return $"REM h264 cpu not implemented: {inputPath}";
    }
}
