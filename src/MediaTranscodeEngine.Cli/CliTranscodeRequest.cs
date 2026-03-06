using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

namespace MediaTranscodeEngine.Cli;

internal sealed record CliTranscodeRequest(
    string InputPath,
    string ScenarioName,
    bool Info,
    ToMkvGpuRequest ToMkvGpu);
