using FluentAssertions;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Policy;
using MediaTranscodeEngine.Core;
using MediaTranscodeEngine.Core.Classification;
using MediaTranscodeEngine.Core.Codecs;
using MediaTranscodeEngine.Core.Compatibility;
using MediaTranscodeEngine.Core.Execution;
using MediaTranscodeEngine.Core.Profiles;
using MediaTranscodeEngine.Core.Quality;
using MediaTranscodeEngine.Core.Resolutions;
using MediaTranscodeEngine.Core.Sampling;
using NSubstitute;

namespace MediaTranscodeEngine.Core.Tests.Codecs;

public class RouteParityTests
{
    [Fact]
    public void ProcessWithProbeResult_WhenRouteSelected_DelegatesToExpectedPipelineBranch()
    {
        var (orchestrator, pipeline, probeReader) = CreateSut();
        var probe = CreateProbe();
        probeReader.Read(Arg.Any<string>()).Returns(probe);

        var copyRequest = TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4");
        var h264Request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mkv",
            TargetContainer: RequestContracts.General.Mp4Container);
        var cpuRequest = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.CpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H264VideoCodec);

        orchestrator.ProcessWithProbeResult(copyRequest, probe)
            .Should().Be(pipeline.ProcessByKeyWithProbeResult(CodecExecutionKeys.Copy, copyRequest, probe));
        orchestrator.ProcessWithProbeResult(h264Request, probe)
            .Should().Be(pipeline.ProcessByKeyWithProbeResult(CodecExecutionKeys.H264Gpu, h264Request, probe));
        var unsupported = () => orchestrator.ProcessWithProbeResult(cpuRequest, probe);
        unsupported.Should().Throw<NotSupportedException>()
            .WithMessage("*backend 'cpu'*codec 'h264'*");
    }

    private static (TranscodeOrchestrator Orchestrator, ITranscodeExecutionPipeline Pipeline, IProbeReader ProbeReader) CreateSut()
    {
        var probeReader = Substitute.For<IProbeReader>();
        var containerPolicySelector = new ContainerPolicySelector(
        [
            new MkvContainerPolicy(),
            new Mp4ContainerPolicy()
        ]);
        var profileDefinitionRepository = new LegacyPolicyConfigProfileRepository(new StaticProfileRepository());
        var profilePolicy = new ProfilePolicy();
        ITranscodeExecutionPipeline pipeline = new TranscodeExecutionPipeline(
            probeReader: probeReader,
            ffmpegCommandBuilder: new FfmpegCommandBuilder(),
            h264CommandBuilder: new H264CommandBuilder(),
            remuxEligibilityPolicy: new H264RemuxEligibilityPolicy(),
            timestampPolicy: new H264TimestampPolicy(),
            audioPolicy: new H264AudioPolicy(),
            rateControlPolicy: new H264RateControlPolicy(),
            containerPolicySelector: containerPolicySelector,
            inputClassifier: new DefaultInputClassifier(),
            resolutionPolicyRepository: new ProfileBackedResolutionPolicyRepository(
                profileDefinitionRepository,
                profilePolicy),
            qualityStrategy: new ProfileBackedQualityStrategy(
                profileDefinitionRepository,
                profilePolicy),
            autoSamplingStrategy: new PolicyDrivenAutoSamplingStrategy(
                profileDefinitionRepository,
                profilePolicy),
            streamCompatibilityPolicy: new DefaultStreamCompatibilityPolicy());
        var catalog = new TranscodeCatalog();
        var registeredStrategyKeys = new[]
        {
            CodecExecutionKeys.Copy,
            CodecExecutionKeys.H264Gpu
        };
        var orchestrator = new TranscodeOrchestrator(
            new TranscodeRouteSelector(
                catalog,
                registeredStrategyKeys),
            pipeline);

        return (orchestrator, pipeline, probeReader);
    }

    private static ProbeResult CreateProbe()
    {
        return new ProbeResult(
            Format: new ProbeFormat(DurationSeconds: 600, BitrateBps: 6_000_000, FormatName: "mov,mp4,m4a,3gp,3g2,mj2"),
            Streams:
            [
                new ProbeStream("video", "h264", Width: 1920, Height: 1080, RFrameRate: "30000/1001", AvgFrameRate: "30000/1001"),
                new ProbeStream("audio", "aac")
            ]);
    }
}
