using MediaTranscodeEngine.Cli.Scenarios;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Scenarios;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Cli.Processing;

/*
Это основная orchestration-реализация CLI:
инспекция файла, построение сценария, получение общего плана и optional execution spec,
а затем выбор tool для генерации итоговой команды или маркера.
*/
/// <summary>
/// Orchestrates the CLI flow from inspected source facts through scenario planning to tool execution output.
/// </summary>
internal sealed class PrimaryTranscodeProcessor : ITranscodeProcessor
{
    private readonly VideoInspector _videoInspector;
    private readonly IReadOnlyList<ITranscodeTool> _transcodeTools;
    private readonly CliScenarioRegistry _scenarioRegistry;
    private readonly ILogger<PrimaryTranscodeProcessor> _logger;

    /// <summary>
    /// Initializes the primary CLI transcode processor.
    /// </summary>
    /// <param name="videoInspector">Source video inspector.</param>
    /// <param name="transcodeTools">Registered transcode tools.</param>
    /// <param name="scenarioRegistry">Registered CLI scenarios.</param>
    /// <param name="logger">Processor logger.</param>
    public PrimaryTranscodeProcessor(
        VideoInspector videoInspector,
        IEnumerable<ITranscodeTool> transcodeTools,
        CliScenarioRegistry scenarioRegistry,
        ILogger<PrimaryTranscodeProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(videoInspector);
        ArgumentNullException.ThrowIfNull(transcodeTools);
        ArgumentNullException.ThrowIfNull(scenarioRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _videoInspector = videoInspector;
        _transcodeTools = transcodeTools.ToArray();
        _scenarioRegistry = scenarioRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Processes one CLI input and returns either info output, legacy diagnostics, or a generated command line.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <returns>Single output line for the input.</returns>
    public string Process(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scenarioHandler = ResolveScenarioHandler(request.ScenarioName);
        LogRequestStart(request);

        try
        {
            var scenario = scenarioHandler.CreateScenario(request);
            var video = _videoInspector.Load(request.InputPath);
            LogVideoInspected(video);
            var plan = scenario.BuildPlan(video);
            LogPlanBuilt(request, plan);

            if (request.Info)
            {
                _logger.LogInformation("Info output generated. InputPath={InputPath}", request.InputPath);
                return scenarioHandler.FormatInfo(request, video, plan);
            }

            var executionSpec = scenario.BuildExecutionSpec(video, plan);
            var tool = ResolveTool(plan, executionSpec);
            var execution = tool.BuildExecution(video, plan, executionSpec);
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
            var failure = scenarioHandler.DescribeFailure(request, exception);
            LogFailure(request, exception, failure);
            return request.Info
                ? failure.InfoOutput
                : failure.NonInfoOutput;
        }
    }

    private void LogRequestStart(CliTranscodeRequest request)
    {
        _logger.LogInformation(
            "Processing started. InputPath={InputPath} Scenario={Scenario} Info={Info} ScenarioArgCount={ScenarioArgCount}",
            request.InputPath,
            request.ScenarioName,
            request.Info,
            request.ScenarioArgs.Count);
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

    private void LogPlanBuilt(CliTranscodeRequest request, TranscodePlan plan)
    {
        var encodeVideo = plan.Video as EncodeVideoPlan;
        var targetHeight = encodeVideo?.Downscale?.TargetHeight;
        var targetFramesPerSecond = encodeVideo?.TargetFramesPerSecond;
        var targetVideoCodec = encodeVideo?.TargetVideoCodec;

        _logger.LogInformation(
            "Transcode plan built. InputPath={InputPath} Scenario={Scenario} TargetContainer={TargetContainer} TargetVideoCodec={TargetVideoCodec} CopyVideo={CopyVideo} CopyAudio={CopyAudio} TargetHeight={TargetHeight} TargetFramesPerSecond={TargetFramesPerSecond} RequiresVideoEncode={RequiresVideoEncode} RequiresAudioEncode={RequiresAudioEncode} ApplyOverlayBackground={ApplyOverlayBackground} SynchronizeAudio={SynchronizeAudio}",
            request.InputPath,
            request.ScenarioName,
            plan.TargetContainer,
            targetVideoCodec,
            plan.CopyVideo,
            plan.CopyAudio,
            targetHeight,
            targetFramesPerSecond,
            plan.RequiresVideoEncode,
            plan.RequiresAudioEncode,
            plan.ApplyOverlayBackground,
            plan.SynchronizeAudio);
    }

    private ICliScenarioHandler ResolveScenarioHandler(string scenarioName)
    {
        if (_scenarioRegistry.TryGetScenario(scenarioName, out var scenarioHandler))
        {
            return scenarioHandler;
        }

        throw new NotSupportedException($"Scenario '{scenarioName}' is not supported by Runtime CLI.");
    }

    private ITranscodeTool ResolveTool(TranscodePlan plan, TranscodeExecutionSpec? executionSpec)
    {
        var tool = _transcodeTools.FirstOrDefault(candidate => candidate.CanHandle(plan, executionSpec));
        if (tool is not null)
        {
            return tool;
        }

        throw new NotSupportedException(
            $"No registered transcode tool can handle the plan for container '{plan.TargetContainer}'.");
    }

    private void LogFailure(CliTranscodeRequest request, Exception exception, CliScenarioFailure failure)
    {
        if (failure.Level == LogLevel.Error)
        {
            _logger.LogError(
                exception,
                "Processing returned failure marker. InputPath={InputPath} Info={Info} FailureKind={FailureKind}",
                request.InputPath,
                request.Info,
                failure.LogToken);
            return;
        }

        _logger.LogWarning(
            exception,
            "Processing returned failure marker. InputPath={InputPath} Info={Info} FailureKind={FailureKind} FailureMessage={FailureMessage}",
            request.InputPath,
            request.Info,
            failure.LogToken,
            exception.Message);
    }
}
