using FluentAssertions;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Policy;

public class H264AudioPolicyTests
{
    [Theory]
    [InlineData("aac")]
    [InlineData("mp3")]
    public void CanCopyAudio_WhenCodecIsCopyCompatibleAndFixDisabled_ReturnsTrue(string audioCodec)
    {
        var sut = CreateSut();
        var input = CreateInput(audioCodec: audioCodec, fixTimestamps: false);

        var actual = sut.CanCopyAudio(input);

        actual.Should().BeTrue();
    }

    [Theory]
    [InlineData("ac3")]
    [InlineData("dts")]
    public void CanCopyAudio_WhenCodecIsNotCopyCompatible_ReturnsFalse(string audioCodec)
    {
        var sut = CreateSut();
        var input = CreateInput(audioCodec: audioCodec, fixTimestamps: false);

        var actual = sut.CanCopyAudio(input);

        actual.Should().BeFalse();
    }

    [Fact]
    public void CanCopyAudio_WhenFixTimestampsEnabled_ReturnsFalse()
    {
        var sut = CreateSut();
        var input = CreateInput(audioCodec: "aac", fixTimestamps: true);

        var actual = sut.CanCopyAudio(input);

        actual.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CanCopyAudio_WhenAudioCodecMissing_ReturnsFalse(string? audioCodec)
    {
        var sut = CreateSut();
        var input = CreateInput(audioCodec: audioCodec, fixTimestamps: false);

        var actual = sut.CanCopyAudio(input);

        actual.Should().BeFalse();
    }

    private static H264AudioPolicy CreateSut()
    {
        return new H264AudioPolicy();
    }

    private static H264AudioInput CreateInput(
        string? audioCodec = "aac",
        bool fixTimestamps = false)
    {
        return new H264AudioInput(
            AudioCodec: audioCodec,
            FixTimestamps: fixTimestamps);
    }
}
