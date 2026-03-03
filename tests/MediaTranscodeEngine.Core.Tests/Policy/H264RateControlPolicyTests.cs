using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Policy;

public class H264RateControlPolicyTests
{
    [Fact]
    public void Resolve_WhenDownscaleAndSourceFpsAbove30AndKeepFpsFalse_UsesCappedFpsAndExpectedGop()
    {
        var sut = CreateSut();
        var input = CreateInput(
            video: CreateVideoStream(rFrameRate: "60/1", avgFrameRate: "60/1"),
            useDownscale: true,
            keepFps: false);

        var actual = sut.Resolve(input);

        actual.FpsToken.Should().Be("30000/1001");
        actual.Gop.Should().Be(60);
    }

    [Fact]
    public void Resolve_WhenKeepFpsTrue_UsesSourceFpsAndExpectedGop()
    {
        var sut = CreateSut();
        var input = CreateInput(
            video: CreateVideoStream(rFrameRate: "60/1", avgFrameRate: "60/1"),
            useDownscale: true,
            keepFps: true);

        var actual = sut.Resolve(input);

        actual.FpsToken.Should().Be("60/1");
        actual.Gop.Should().Be(120);
    }

    [Fact]
    public void Resolve_WhenRFrameRateIsZeroTokenAndAvgFrameRateProvided_UsesAverageRateToken()
    {
        var sut = CreateSut();
        var input = CreateInput(
            video: CreateVideoStream(rFrameRate: "0/0", avgFrameRate: "24000/1001"));

        var actual = sut.Resolve(input);

        actual.FpsToken.Should().Be("24000/1001");
        actual.Gop.Should().Be(48);
    }

    [Fact]
    public void Resolve_WhenFpsTokenIsInvalid_UsesDefaultGop()
    {
        var sut = CreateSut();
        var input = CreateInput(
            video: CreateVideoStream(rFrameRate: "invalid", avgFrameRate: "0/0"));

        var actual = sut.Resolve(input);

        actual.FpsToken.Should().Be("invalid");
        actual.Gop.Should().Be(60);
    }

    [Fact]
    public void Resolve_WhenFpsTokensMissing_UsesDefaultFpsAndDefaultGop()
    {
        var sut = CreateSut();
        var input = CreateInput(
            video: CreateVideoStream(rFrameRate: null, avgFrameRate: null),
            useDownscale: false,
            keepFps: false);

        var actual = sut.Resolve(input);

        actual.FpsToken.Should().Be("30/1");
        actual.Gop.Should().Be(60);
    }

    [Theory]
    [InlineData(null, 19)]
    [InlineData(23, 23)]
    public void Resolve_WhenCqOverrideChanges_ReturnsExpectedCq(int? cqOverride, int expectedCq)
    {
        var sut = CreateSut();
        var input = CreateInput(
            video: CreateVideoStream(rFrameRate: "25/1", avgFrameRate: "25/1"),
            cqOverride: cqOverride);

        var actual = sut.Resolve(input);

        actual.Cq.Should().Be(expectedCq);
    }

    private static H264RateControlPolicy CreateSut()
    {
        return new H264RateControlPolicy();
    }

    private static H264RateControlInput CreateInput(
        ProbeStream? video = null,
        bool useDownscale = false,
        bool keepFps = false,
        int? cqOverride = null)
    {
        return new H264RateControlInput(
            Video: video ?? CreateVideoStream(),
            UseDownscale: useDownscale,
            KeepFps: keepFps,
            CqOverride: cqOverride);
    }

    private static ProbeStream CreateVideoStream(
        string? rFrameRate = "25/1",
        string? avgFrameRate = "25/1")
    {
        return new ProbeStream(
            CodecType: "video",
            CodecName: "h264",
            RFrameRate: rFrameRate,
            AvgFrameRate: avgFrameRate);
    }
}
