using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Codecs;

public interface ITranscodeCapabilityPolicy
{
    TranscodeCapabilityDecision Decide(TranscodeRequest request);
}

public sealed record TranscodeCapabilityDecision(
    bool IsSupported,
    string? Reason = null);
