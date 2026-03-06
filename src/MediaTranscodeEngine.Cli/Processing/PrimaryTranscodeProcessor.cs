using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Cli.Processing;

internal sealed class PrimaryTranscodeProcessor : ITranscodeProcessor
{
    private readonly VideoInspector _videoInspector;
    private readonly ITranscodeTool _transcodeTool;
    private readonly ToMkvGpuInfoFormatter _infoFormatter;

    public PrimaryTranscodeProcessor(
        VideoInspector videoInspector,
        ITranscodeTool transcodeTool,
        ToMkvGpuInfoFormatter infoFormatter)
    {
        _videoInspector = videoInspector;
        _transcodeTool = transcodeTool;
        _infoFormatter = infoFormatter;
    }

    public string Process(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scenario = CreateScenario(request);

        try
        {
            var video = _videoInspector.Load(request.InputPath);
            var plan = scenario.BuildPlan(video);

            if (request.Info)
            {
                return _infoFormatter.Format(video, plan);
            }

            var execution = _transcodeTool.BuildExecution(video, plan);
            return execution.IsEmpty
                ? string.Empty
                : string.Join(Environment.NewLine, execution.Commands);
        }
        catch (Exception exception) when (request.Info)
        {
            return _infoFormatter.FormatFailure(request.InputPath, exception);
        }
    }

    private static ToMkvGpuScenario CreateScenario(CliTranscodeRequest request)
    {
        if (!request.ScenarioName.Equals(CliContracts.SupportedScenario, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Scenario '{request.ScenarioName}' is not supported by Runtime CLI.");
        }

        return new ToMkvGpuScenario(
            overlayBackground: request.OverlayBackground,
            downscaleTarget: request.DownscaleTarget,
            synchronizeAudio: request.SynchronizeAudio,
            keepSource: request.KeepSource);
    }
}
