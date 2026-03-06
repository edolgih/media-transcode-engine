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
                : string.Join(" && ", execution.Commands);
        }
        catch (Exception exception) when (TryFormatFailure(request, exception, out _))
        {
            return FormatFailure(request, exception);
        }
    }

    private static ToMkvGpuScenario CreateScenario(CliTranscodeRequest request)
    {
        if (!request.ScenarioName.Equals(CliContracts.SupportedScenario, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Scenario '{request.ScenarioName}' is not supported by Runtime CLI.");
        }

        return new ToMkvGpuScenario(request.ToMkvGpu);
    }

    private string FormatFailure(CliTranscodeRequest request, Exception exception)
    {
        if (request.Info)
        {
            return _infoFormatter.FormatFailure(request.InputPath, exception);
        }

        var fileName = Path.GetFileName(request.InputPath);
        if (request.ToMkvGpu.OverlayBackground && IsUnknownDimensionsFailure(exception))
        {
            return $"REM Unknown dimensions: {fileName}";
        }

        if (IsNoVideoStreamFailure(exception))
        {
            return $"REM Нет видеопотока: {fileName}";
        }

        if (IsDownscaleNotImplementedFailure(exception))
        {
            return $"REM Downscale 720 not implemented: {fileName}";
        }

        return $"REM ffprobe failed: {fileName}";
    }

    private static bool TryFormatFailure(CliTranscodeRequest request, Exception exception, out string line)
    {
        if (request.Info)
        {
            line = string.Empty;
            return true;
        }

        if (IsUnknownDimensionsFailure(exception) ||
            IsNoVideoStreamFailure(exception) ||
            IsDownscaleNotImplementedFailure(exception) ||
            IsProbeFailure(exception))
        {
            line = string.Empty;
            return true;
        }

        line = string.Empty;
        return false;
    }

    private static bool IsUnknownDimensionsFailure(Exception exception)
    {
        return exception.Message.Contains("valid video width", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("valid video height", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoVideoStreamFailure(Exception exception)
    {
        return exception.Message.Contains("video stream", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDownscaleNotImplementedFailure(Exception exception)
    {
        return exception.Message.Contains("downscale", StringComparison.OrdinalIgnoreCase) &&
               exception.Message.Contains("720", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProbeFailure(Exception exception)
    {
        return exception.Message.Contains("ffprobe", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("video probe", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("streams", StringComparison.OrdinalIgnoreCase);
    }
}
