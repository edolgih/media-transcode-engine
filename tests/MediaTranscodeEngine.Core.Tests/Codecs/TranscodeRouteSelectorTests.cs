using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Codecs;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Tests.Codecs;

public class TranscodeRouteSelectorTests
{
    [Fact]
    public void Select_WhenCopyRouteMatches_ReturnsCopyRoute()
    {
        var expected = new NamedRoute("copy", _ => true);
        var sut = new TranscodeRouteSelector(
        [
            expected,
            new NamedRoute("other", _ => false)
        ],
            CreateCapabilityPolicy(CodecExecutionKeys.Copy));
        var request = TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4");

        var actual = sut.Select(request);

        actual.Should().BeSameAs(expected);
    }

    [Fact]
    public void Select_WhenGpuEncodeMatches_ReturnsGpuEncodeRoute()
    {
        var expected = new NamedRoute("h264", static request =>
            request.EncoderBackend.Equals(RequestContracts.General.GpuEncoderBackend, StringComparison.OrdinalIgnoreCase) &&
            request.TargetVideoCodec.Equals(RequestContracts.General.H264VideoCodec, StringComparison.OrdinalIgnoreCase));
        var sut = new TranscodeRouteSelector(
        [
            new NamedRoute("copy", _ => false),
            expected
        ],
            CreateCapabilityPolicy(CodecExecutionKeys.H264Gpu));
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            TargetVideoCodec: RequestContracts.General.H264VideoCodec);

        var actual = sut.Select(request);

        actual.Should().BeSameAs(expected);
    }

    [Fact]
    public void Select_WhenNoRouteMatches_ThrowsExpectedError()
    {
        var sut = new TranscodeRouteSelector(
        [
            new NamedRoute("copy", _ => false)
        ],
            CreateCapabilityPolicy(CodecExecutionKeys.Copy));
        var request = TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4");

        var act = () => sut.Select(request);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No transcode route was found*");
    }

    [Fact]
    public void Select_WhenCombinationUnsupported_ThrowsNotSupportedException()
    {
        var sut = new TranscodeRouteSelector(
        [
            new NamedRoute("catch-all", _ => true)
        ],
            CreateCapabilityPolicy(CodecExecutionKeys.H264Gpu));
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.CpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H264VideoCodec);

        var act = () => sut.Select(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*encoder backend 'cpu'*codec 'h264'*");
    }

    [Fact]
    public void Select_WhenGpuCodecStrategyNotRegistered_ThrowsNotSupportedException()
    {
        var sut = new TranscodeRouteSelector(
        [
            new NamedRoute("encode-gpu", _ => true)
        ],
            CreateCapabilityPolicy(CodecExecutionKeys.Copy, CodecExecutionKeys.H264Gpu));
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            TargetVideoCodec: RequestContracts.General.H265VideoCodec);

        var act = () => sut.Select(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*codec 'h265'*encoder backend 'gpu'*");
    }

    private static ITranscodeCapabilityPolicy CreateCapabilityPolicy(params string[] strategyKeys)
    {
        return new StrategyBackedTranscodeCapabilityPolicy(strategyKeys);
    }

    private sealed class NamedRoute : ITranscodeRoute
    {
        private readonly Func<TranscodeRequest, bool> _predicate;

        public NamedRoute(string name, Func<TranscodeRequest, bool> predicate)
        {
            Name = name;
            _predicate = predicate;
        }

        public string Name { get; }

        public bool CanHandle(TranscodeRequest request) => _predicate(request);

        public string Process(TranscodeRequest request) => Name;

        public string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe) => Name;

        public string ProcessWithProbeJson(TranscodeRequest request, string? probeJson) => Name;
    }
}
