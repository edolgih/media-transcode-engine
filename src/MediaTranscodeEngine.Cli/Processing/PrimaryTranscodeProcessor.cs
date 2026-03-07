using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Cli.Processing;

internal sealed class PrimaryTranscodeProcessor : ITranscodeProcessor
{
    private readonly VideoInspector _videoInspector;
    private readonly ITranscodeTool _transcodeTool;
    private readonly ToMkvGpuInfoFormatter _infoFormatter;
    private readonly ILogger<PrimaryTranscodeProcessor> _logger;

    public PrimaryTranscodeProcessor(
        VideoInspector videoInspector,
        ITranscodeTool transcodeTool,
        ToMkvGpuInfoFormatter infoFormatter,
        ILogger<PrimaryTranscodeProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(videoInspector);
        ArgumentNullException.ThrowIfNull(transcodeTool);
        ArgumentNullException.ThrowIfNull(infoFormatter);
        ArgumentNullException.ThrowIfNull(logger);

        _videoInspector = videoInspector;
        _transcodeTool = transcodeTool;
        _infoFormatter = infoFormatter;
        _logger = logger;
    }

    public string Process(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        LogRequestStart(request);
        var scenario = CreateScenario(request);

        try
        {
            var video = _videoInspector.Load(request.InputPath);
            LogVideoInspected(video);
            var plan = scenario.BuildPlan(video);
            LogPlanBuilt(request, plan);

            if (request.Info)
            {
                _logger.LogInformation("Info output generated. InputPath={InputPath}", request.InputPath);
                return _infoFormatter.Format(video, plan);
            }

            var execution = _transcodeTool.BuildExecution(video, plan);
            _logger.LogInformation(
                "Tool execution built. InputPath={InputPath} ToolName={ToolName} CommandCount={CommandCount} IsEmpty={IsEmpty}",
                request.InputPath,
                execution.ToolName,
                execution.Commands.Count,
                execution.IsEmpty);
            return execution.IsEmpty
                ? string.Empty
                : string.Join(" && ", execution.Commands);
        }
        catch (Exception exception)
        {
            var failure = ClassifyHandledFailure(request, exception);
            if (failure is null)
            {
                throw;
            }

            _logger.LogWarning(
                exception,
                "Processing returned legacy failure marker. InputPath={InputPath} Info={Info} FailureKind={FailureKind}",
                request.InputPath,
                request.Info,
                failure.Value.LogToken);
            return FormatFailure(request, exception, failure.Value);
        }
    }

    private void LogRequestStart(CliTranscodeRequest request)
    {
        var downscale = request.ToMkvGpu.Downscale;
        _logger.LogInformation(
            "Processing started. InputPath={InputPath} Scenario={Scenario} Info={Info} OverlayBackground={OverlayBackground} SynchronizeAudio={SynchronizeAudio} KeepSource={KeepSource} DownscaleTarget={DownscaleTarget} ContentProfile={ContentProfile} QualityProfile={QualityProfile} NoAutoSample={NoAutoSample} AutoSampleMode={AutoSampleMode} Algorithm={Algorithm} Cq={Cq} Maxrate={Maxrate} Bufsize={Bufsize} NvencPreset={NvencPreset}",
            request.InputPath,
            request.ScenarioName,
            request.Info,
            request.ToMkvGpu.OverlayBackground,
            request.ToMkvGpu.SynchronizeAudio,
            request.ToMkvGpu.KeepSource,
            downscale?.TargetHeight,
            downscale?.ContentProfile,
            downscale?.QualityProfile,
            downscale?.NoAutoSample ?? false,
            downscale?.AutoSampleMode,
            downscale?.Algorithm,
            downscale?.Cq,
            downscale?.Maxrate,
            downscale?.Bufsize,
            request.ToMkvGpu.NvencPreset);
    }

    private void LogVideoInspected(SourceVideo video)
    {
        _logger.LogInformation(
            "Video inspected. InputPath={InputPath} Container={Container} VideoCodec={VideoCodec} Width={Width} Height={Height} FramesPerSecond={FramesPerSecond} DurationSeconds={DurationSeconds} AudioStreamCount={AudioStreamCount} Bitrate={Bitrate}",
            video.FilePath,
            video.Container,
            video.VideoCodec,
            video.Width,
            video.Height,
            video.FramesPerSecond,
            video.Duration.TotalSeconds,
            video.AudioCodecs.Count,
            video.Bitrate);
    }

    private void LogPlanBuilt(CliTranscodeRequest request, Runtime.Plans.TranscodePlan plan)
    {
        _logger.LogInformation(
            "Transcode plan built. InputPath={InputPath} TargetContainer={TargetContainer} TargetVideoCodec={TargetVideoCodec} CopyVideo={CopyVideo} CopyAudio={CopyAudio} TargetHeight={TargetHeight} TargetFramesPerSecond={TargetFramesPerSecond} RequiresVideoEncode={RequiresVideoEncode} RequiresAudioEncode={RequiresAudioEncode} ApplyOverlayBackground={ApplyOverlayBackground} SynchronizeAudio={SynchronizeAudio}",
            request.InputPath,
            plan.TargetContainer,
            plan.TargetVideoCodec,
            plan.CopyVideo,
            plan.CopyAudio,
            plan.TargetHeight,
            plan.TargetFramesPerSecond,
            plan.RequiresVideoEncode,
            plan.RequiresAudioEncode,
            plan.ApplyOverlayBackground,
            plan.SynchronizeAudio);
    }

    private static ToMkvGpuScenario CreateScenario(CliTranscodeRequest request)
    {
        if (!request.ScenarioName.Equals(CliContracts.SupportedScenario, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Scenario '{request.ScenarioName}' is not supported by Runtime CLI.");
        }

        return new ToMkvGpuScenario(request.ToMkvGpu);
    }

    private string FormatFailure(CliTranscodeRequest request, Exception exception, HandledFailure failure)
    {
        var fileName = Path.GetFileName(request.InputPath);
        return failure.Kind switch
        {
            HandledFailureKind.InfoFailure => _infoFormatter.FormatFailure(request.InputPath, exception),
            HandledFailureKind.UnknownDimensionsOverlay => $"REM Unknown dimensions: {fileName}",
            HandledFailureKind.NoVideoStream => $"REM Нет видеопотока: {fileName}",
            HandledFailureKind.DownscaleNotImplemented => $"REM Downscale 720 not implemented: {fileName}",
            HandledFailureKind.DownscaleSourceBucket => $"REM {exception.Message}",
            HandledFailureKind.ProbeFailure => $"REM ffprobe failed: {fileName}",
            _ => throw new InvalidOperationException($"Unhandled failure kind '{failure.Kind}'.")
        };
    }

    private static HandledFailure? ClassifyHandledFailure(CliTranscodeRequest request, Exception exception)
    {
        if (request.Info)
        {
            return new HandledFailure(HandledFailureKind.InfoFailure, "info_failure");
        }

        var message = exception.Message;
        if (request.ToMkvGpu.OverlayBackground &&
            (message.Contains("valid video width", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("valid video height", StringComparison.OrdinalIgnoreCase)))
        {
            return new HandledFailure(HandledFailureKind.UnknownDimensionsOverlay, "unknown_dimensions");
        }

        if (message.Contains("video stream", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.NoVideoStream, "no_video_stream");
        }

        if (message.Contains("downscale", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("720", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.DownscaleNotImplemented, "downscale_not_implemented");
        }

        if (message.Contains("source bucket missing", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("source bucket invalid", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.DownscaleSourceBucket, "downscale_source_bucket");
        }

        if (message.Contains("ffprobe", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("video probe", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("streams", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.ProbeFailure, "probe_failure");
        }

        return null;
    }

    private enum HandledFailureKind
    {
        InfoFailure,
        UnknownDimensionsOverlay,
        NoVideoStream,
        DownscaleNotImplemented,
        DownscaleSourceBucket,
        ProbeFailure
    }

    private readonly record struct HandledFailure(HandledFailureKind Kind, string LogToken);
}
