using FluentAssertions;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Policy;

public class H264TimestampPolicyTests
{
    [Fact]
    public void ShouldFixTimestamps_WhenForceFixEnabled_ReturnsTrue()
    {
        var sut = CreateSut();
        var input = CreateInput(forceFixTimestamps: true);

        var actual = sut.ShouldFixTimestamps(input);

        actual.Should().BeTrue();
    }

    [Theory]
    [InlineData("C:\\video\\a.wmv")]
    [InlineData("C:\\video\\a.asf")]
    public void ShouldFixTimestamps_WhenInputExtensionIsAsfFamily_ReturnsTrue(string inputPath)
    {
        var sut = CreateSut();
        var input = CreateInput(inputPath: inputPath);

        var actual = sut.ShouldFixTimestamps(input);

        actual.Should().BeTrue();
    }

    [Fact]
    public void ShouldFixTimestamps_WhenFormatContainsAsfToken_ReturnsTrue()
    {
        var sut = CreateSut();
        var input = CreateInput(formatName: "asf,wmv");

        var actual = sut.ShouldFixTimestamps(input);

        actual.Should().BeTrue();
    }

    [Fact]
    public void ShouldFixTimestamps_WhenNoSignalsPresent_ReturnsFalse()
    {
        var sut = CreateSut();
        var input = CreateInput(
            inputPath: "C:\\video\\a.mp4",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2");

        var actual = sut.ShouldFixTimestamps(input);

        actual.Should().BeFalse();
    }

    private static H264TimestampPolicy CreateSut()
    {
        return new H264TimestampPolicy();
    }

    private static H264TimestampInput CreateInput(
        string inputPath = "C:\\video\\a.mp4",
        string? formatName = null,
        bool forceFixTimestamps = false)
    {
        return new H264TimestampInput(
            InputPath: inputPath,
            FormatName: formatName,
            ForceFixTimestamps: forceFixTimestamps);
    }
}
