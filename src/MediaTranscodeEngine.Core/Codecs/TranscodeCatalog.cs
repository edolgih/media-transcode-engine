using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Codecs;

public sealed class TranscodeCatalog
{
    private readonly IReadOnlyDictionary<string, CodecDescriptor> _codecs;
    private readonly IReadOnlyDictionary<string, EncoderBackendDescriptor> _backends;

    public TranscodeCatalog(
        IEnumerable<CodecDescriptor>? codecs = null,
        IEnumerable<EncoderBackendDescriptor>? backends = null)
    {
        var effectiveCodecs = codecs?.ToArray();
        if (effectiveCodecs is null || effectiveCodecs.Length == 0)
        {
            effectiveCodecs = CreateDefaultCodecs().ToArray();
        }

        var effectiveBackends = backends?.ToArray();
        if (effectiveBackends is null || effectiveBackends.Length == 0)
        {
            effectiveBackends = CreateDefaultBackends().ToArray();
        }

        _codecs = effectiveCodecs.ToDictionary(
            static codec => codec.CodecId,
            StringComparer.OrdinalIgnoreCase);
        _backends = effectiveBackends.ToDictionary(
            static backend => backend.BackendId,
            StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetCodec(string codecId, out CodecDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(codecId))
        {
            descriptor = null!;
            return false;
        }

        return _codecs.TryGetValue(codecId.Trim(), out descriptor!);
    }

    public bool TryGetBackend(string backendId, out EncoderBackendDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(backendId))
        {
            descriptor = null!;
            return false;
        }

        return _backends.TryGetValue(backendId.Trim(), out descriptor!);
    }

    private static IReadOnlyList<CodecDescriptor> CreateDefaultCodecs()
    {
        return
        [
            new CodecDescriptor(
                codecId: RequestContracts.General.H264VideoCodec,
                supportedContainers: [RequestContracts.General.MkvContainer, RequestContracts.General.Mp4Container]),
            new CodecDescriptor(
                codecId: RequestContracts.General.H265VideoCodec,
                supportedContainers: [RequestContracts.General.MkvContainer, RequestContracts.General.Mp4Container])
        ];
    }

    private static IReadOnlyList<EncoderBackendDescriptor> CreateDefaultBackends()
    {
        return
        [
            new EncoderBackendDescriptor(
                backendId: RequestContracts.General.GpuEncoderBackend,
                codecStrategyKeys: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [RequestContracts.General.H264VideoCodec] = CodecExecutionKeys.BuildGpuEncodeKey(RequestContracts.General.H264VideoCodec),
                    [RequestContracts.General.H265VideoCodec] = CodecExecutionKeys.BuildGpuEncodeKey(RequestContracts.General.H265VideoCodec)
                }),
            new EncoderBackendDescriptor(
                backendId: RequestContracts.General.CpuEncoderBackend,
                codecStrategyKeys: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        ];
    }
}
