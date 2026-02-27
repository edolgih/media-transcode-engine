using FluentAssertions;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Policy;

public class H264RemuxEligibilityPolicyTests
{
    [Fact]
    public void CanRemux_WhenInputIsEligible_ReturnsTrue()
    {
        var sut = CreateSut();
        var input = CreateInput();

        var actual = sut.CanRemux(input);

        actual.Should().BeTrue();
    }

    [Theory]
    [InlineData(".mkv")]
    [InlineData(".avi")]
    public void CanRemux_WhenInputExtensionIsNotMp4Family_ReturnsFalse(string extension)
    {
        var sut = CreateSut();
        var input = CreateInput(inputExtension: extension);

        var actual = sut.CanRemux(input);

        actual.Should().BeFalse();
    }

    [Theory]
    [InlineData("ac3")]
    [InlineData("dts")]
    public void CanRemux_WhenAudioCodecIsNotCopyCompatible_ReturnsFalse(string audioCodec)
    {
        var sut = CreateSut();
        var input = CreateInput(audioCodec: audioCodec);

        var actual = sut.CanRemux(input);

        actual.Should().BeFalse();
    }

    [Theory]
    [InlineData("25/1", "25/1", true)]
    [InlineData("30000/1001", "30000/1001", true)]
    [InlineData("25/1", "24/1", false)]
    [InlineData("0/0", "25/1", true)]
    public void CanRemux_WhenFrameRateChanges_ReturnsExpectedOutcome(
        string rFrameRate,
        string avgFrameRate,
        bool expected)
    {
        var sut = CreateSut();
        var input = CreateInput(rFrameRate: rFrameRate, avgFrameRate: avgFrameRate);

        var actual = sut.CanRemux(input);

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void CanRemux_WhenGuardFlagsChange_ReturnsExpectedOutcome(
        bool denoise,
        bool fixTimestamps,
        bool useDownscale,
        bool expected)
    {
        var sut = CreateSut();
        var input = CreateInput(
            denoise: denoise,
            fixTimestamps: fixTimestamps,
            useDownscale: useDownscale);

        var actual = sut.CanRemux(input);

        actual.Should().Be(expected);
    }

    private static H264RemuxEligibilityPolicy CreateSut()
    {
        return new H264RemuxEligibilityPolicy();
    }

    private static H264RemuxEligibilityInput CreateInput(
        string inputExtension = ".mp4",
        string formatName = "mov,mp4,m4a,3gp,3g2,mj2",
        string videoCodec = "h264",
        string? audioCodec = "aac",
        string rFrameRate = "25/1",
        string avgFrameRate = "25/1",
        bool denoise = false,
        bool fixTimestamps = false,
        bool useDownscale = false)
    {
        return new H264RemuxEligibilityInput(
            InputExtension: inputExtension,
            FormatName: formatName,
            VideoCodec: videoCodec,
            AudioCodec: audioCodec,
            RFrameRate: rFrameRate,
            AvgFrameRate: avgFrameRate,
            Denoise: denoise,
            FixTimestamps: fixTimestamps,
            UseDownscale: useDownscale);
    }
}
