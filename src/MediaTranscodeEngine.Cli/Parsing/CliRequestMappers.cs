using MediaTranscodeEngine.Runtime.Downscaling;
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
                synchronizeAudio: template.SynchronizeAudio,
                keepSource: template.KeepSource,
                downscale: BuildDownscaleRequest(template),
                nvencPreset: template.NvencPreset));
    }

    private static DownscaleRequest? BuildDownscaleRequest(CliRequestTemplate template)
    {
        var downscale = new DownscaleRequest(
            targetHeight: template.DownscaleTarget,
            contentProfile: template.ContentProfile,
            qualityProfile: template.QualityProfile,
            noAutoSample: template.NoAutoSample,
            autoSampleMode: template.AutoSampleMode,
            algorithm: template.DownscaleAlgorithm,
            cq: template.Cq,
            maxrate: template.Maxrate,
            bufsize: template.Bufsize);

        return downscale.HasValue ? downscale : null;
    }
}
