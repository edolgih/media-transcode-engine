using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Execution;

public static class CodecExecutionKeys
{
    public const string Copy = "copy";
    public const string H264Gpu = "h264-gpu";

    public static string BuildGpuEncodeKey(string codecToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codecToken);
        return $"{codecToken.Trim().ToLowerInvariant()}-{RequestContracts.General.GpuEncoderBackend}";
    }
}
