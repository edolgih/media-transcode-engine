using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

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
            ToMkvGpu: new ToMkvGpuRequest(
                overlayBackground: template.OverlayBackground,
                downscaleTarget: template.DownscaleTarget,
                synchronizeAudio: template.SynchronizeAudio,
                keepSource: template.KeepSource,
                contentProfile: template.ContentProfile,
                qualityProfile: template.QualityProfile,
                noAutoSample: template.NoAutoSample,
                autoSampleMode: template.AutoSampleMode,
                downscaleAlgorithm: template.DownscaleAlgorithm,
                cq: template.Cq,
                maxrate: template.Maxrate,
                bufsize: template.Bufsize,
                nvencPreset: template.NvencPreset));
    }
}
