namespace MediaTranscodeEngine.Cli;

internal sealed record CliTranscodeRequest(
    string InputPath,
    string ScenarioName,
    bool Info,
    bool KeepSource,
    bool OverlayBackground,
    int? DownscaleTarget,
    bool SynchronizeAudio);
