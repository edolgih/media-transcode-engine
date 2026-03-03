using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Policy;

public class TargetVideoCodecResolverTests
{
    private readonly TargetVideoCodecResolver _sut = new();

    [Fact]
    public void Resolve_WhenPreferH264Enabled_ReturnsH264()
    {
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mkv",
            PreferH264: true);

        var actual = _sut.Resolve(request);

        actual.Should().Be(TargetVideoCodec.H264);
    }

    [Fact]
    public void Resolve_WhenComputeIsCpu_ReturnsH264()
    {
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mkv",
            ComputeMode: RequestContracts.Unified.CpuComputeMode);

        var actual = _sut.Resolve(request);

        actual.Should().Be(TargetVideoCodec.H264);
    }

    [Fact]
    public void Resolve_WhenContainerIsMp4_ReturnsH264()
    {
        var request = UnifiedTranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mkv",
            TargetContainer: RequestContracts.Unified.Mp4Container);

        var actual = _sut.Resolve(request);

        actual.Should().Be(TargetVideoCodec.H264);
    }

    [Fact]
    public void Resolve_WhenDefaultMkvGpuSettings_ReturnsCopy()
    {
        var request = UnifiedTranscodeRequest.Create(InputPath: "C:\\video\\movie.mkv");

        var actual = _sut.Resolve(request);

        actual.Should().Be(TargetVideoCodec.Copy);
    }
}
