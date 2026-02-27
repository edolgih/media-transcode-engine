using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Abstractions;

public interface IProbeReader
{
    ProbeResult? Read(string inputPath);
}
