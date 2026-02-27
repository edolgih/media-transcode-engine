using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Abstractions;

public interface IProfileRepository
{
    TranscodePolicyConfig Get576Config();
}
