namespace MediaTranscodeEngine.Cli.Parsing;

internal static class CliRequestMappers
{
    public static CliTranscodeRequest BuildRequest(CliRequestTemplate template, string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        return new CliTranscodeRequest(
            InputPath: inputPath,
            ScenarioName: template.Scenario,
            Info: template.Info,
            KeepSource: template.KeepSource,
            OverlayBackground: template.OverlayBackground,
            DownscaleTarget: template.DownscaleTarget,
            SynchronizeAudio: template.SynchronizeAudio);
    }
}
