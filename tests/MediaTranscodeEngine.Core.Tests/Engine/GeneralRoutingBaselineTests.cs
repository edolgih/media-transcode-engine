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

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class GeneralRoutingBaselineTests
{
    [Fact]
    public void Process_WhenDefaultMkvGpu_UsesCopyRoute()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe());
        var request = TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4");

        var actual = sut.Process(request);

        actual.Should().Contain("-map 0:v:0 -c:v copy");
    }

    [Fact]
    public void Process_WhenContainerMp4_UsesH264Route()
    {
        var (sut, probeReader) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe());
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mkv",
            TargetContainer: RequestContracts.General.Mp4Container);

        var actual = sut.Process(request);

        actual.Should().Contain("-c:v h264_nvenc");
    }

    private static (TranscodeOrchestrator Sut, IProbeReader ProbeReader) CreateSut()
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
        var sut = new TranscodeOrchestrator(
            new TranscodeRouteSelector(
            [
                new CopyRoute(pipeline),
                new GpuEncodeRoute(pipeline)
            ],
                new StrategyBackedTranscodeCapabilityPolicy([CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu])));

        return (sut, probeReader);
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
