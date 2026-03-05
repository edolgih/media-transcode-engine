using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Codecs;

public sealed class TranscodeCatalog
{
    private readonly IReadOnlyDictionary<string, TranscodeProfile> _profiles;

    public TranscodeCatalog(
        IEnumerable<TranscodeProfile>? profiles = null)
    {
        var effectiveProfiles = profiles?.ToArray();
        if (effectiveProfiles is null || effectiveProfiles.Length == 0)
        {
            effectiveProfiles = CreateDefaultProfiles().ToArray();
        }

        _profiles = effectiveProfiles.ToDictionary(
            static profile => BuildKey(profile.EncoderBackend, profile.CodecId),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetProfile(string encoderBackend, string codecId, out TranscodeProfile profile)
    {
        if (string.IsNullOrWhiteSpace(encoderBackend) || string.IsNullOrWhiteSpace(codecId))
        {
            profile = null!;
            return false;
        }

        return _profiles.TryGetValue(BuildKey(encoderBackend, codecId), out profile!);
    }

    private static string BuildKey(string encoderBackend, string codecId)
    {
        return $"{encoderBackend.Trim().ToLowerInvariant()}::{codecId.Trim().ToLowerInvariant()}";
    }

    private static IReadOnlyList<TranscodeProfile> CreateDefaultProfiles()
    {
        return
        [
            new TranscodeProfile(
                codecId: RequestContracts.General.H264VideoCodec,
                encoderBackend: RequestContracts.General.GpuEncoderBackend,
                strategyKey: CodecExecutionKeys.BuildGpuEncodeKey(RequestContracts.General.H264VideoCodec),
                supportedContainers: [RequestContracts.General.MkvContainer, RequestContracts.General.Mp4Container]),
            new TranscodeProfile(
                codecId: RequestContracts.General.H265VideoCodec,
                encoderBackend: RequestContracts.General.GpuEncoderBackend,
                strategyKey: CodecExecutionKeys.BuildGpuEncodeKey(RequestContracts.General.H265VideoCodec),
                supportedContainers: [RequestContracts.General.MkvContainer, RequestContracts.General.Mp4Container])
        ];
    }
}
