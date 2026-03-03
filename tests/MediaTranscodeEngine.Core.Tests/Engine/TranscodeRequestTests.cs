using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class TranscodeRequestTests
{
    [Fact]
    public void EnsureValid_WhenInputPathIsMissing_ThrowsArgumentException()
    {
        var sut = new TranscodeRequest(InputPath: " ");

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithMessage("*InputPath is required.*");
    }

    [Fact]
    public void EnsureValid_WhenCalled_ReturnsNormalizedRequest()
    {
        var sut = new TranscodeRequest(
            InputPath: " C:\\video\\movie.mp4 ",
            Info: true,
            OverlayBg: true,
            Downscale: 576,
            DownscaleAlgoOverride: "lanczos",
            ContentProfile: "film",
            QualityProfile: "high",
            NoAutoSample: true,
            AutoSampleMode: "fast",
            SyncAudio: true,
            Cq: 21,
            Maxrate: 3.5,
            Bufsize: 7.0,
            NvencPreset: "p5",
            ForceVideoEncode: true);

        var actual = sut.EnsureValid();

        actual.InputPath.Should().Be("C:\\video\\movie.mp4");
        actual.Info.Should().BeTrue();
        actual.OverlayBg.Should().BeTrue();
        actual.Downscale.Should().Be(576);
        actual.DownscaleAlgoOverride.Should().Be("lanczos");
        actual.ContentProfile.Should().Be("film");
        actual.QualityProfile.Should().Be("high");
        actual.NoAutoSample.Should().BeTrue();
        actual.AutoSampleMode.Should().Be("fast");
        actual.SyncAudio.Should().BeTrue();
        actual.Cq.Should().Be(21);
        actual.Maxrate.Should().Be(3.5);
        actual.Bufsize.Should().Be(7.0);
        actual.NvencPreset.Should().Be("p5");
        actual.ForceVideoEncode.Should().BeTrue();
    }

    [Fact]
    public void EnsureValid_WhenDownscaleAlgoOverrideIsWhitespace_SetsNull()
    {
        var sut = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            DownscaleAlgoOverride: " ");

        var actual = sut.EnsureValid();

        actual.DownscaleAlgoOverride.Should().BeNull();
    }

    [Theory]
    [InlineData(" ", "ContentProfile", "*ContentProfile is required.*")]
    [InlineData(" ", "QualityProfile", "*QualityProfile is required.*")]
    [InlineData(" ", "AutoSampleMode", "*AutoSampleMode is required.*")]
    [InlineData(" ", "NvencPreset", "*NvencPreset is required.*")]
    public void EnsureValid_WhenRequiredTextValueIsMissing_ThrowsArgumentException(
        string missingValue,
        string propertyName,
        string expectedMessage)
    {
        var sut = CreateRequestWithOverride(propertyName, missingValue);

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithParameterName(propertyName)
            .WithMessage(expectedMessage);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("doc")]
    public void EnsureValid_WhenContentProfileInvalid_ThrowsArgumentException(string contentProfile)
    {
        var sut = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            ContentProfile: contentProfile);

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithParameterName("ContentProfile")
            .WithMessage("*ContentProfile must be one of: anime, mult, film.*");
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("medium")]
    public void EnsureValid_WhenQualityProfileInvalid_ThrowsArgumentException(string qualityProfile)
    {
        var sut = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            QualityProfile: qualityProfile);

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithParameterName("QualityProfile")
            .WithMessage("*QualityProfile must be one of: high, default, low.*");
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("adaptive")]
    public void EnsureValid_WhenAutoSampleModeInvalid_ThrowsArgumentException(string autoSampleMode)
    {
        var sut = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            AutoSampleMode: autoSampleMode);

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithParameterName("AutoSampleMode")
            .WithMessage("*AutoSampleMode must be one of: accurate, fast, hybrid.*");
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("p8")]
    public void EnsureValid_WhenNvencPresetInvalid_ThrowsArgumentException(string nvencPreset)
    {
        var sut = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            NvencPreset: nvencPreset);

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithParameterName("NvencPreset")
            .WithMessage("*NvencPreset must be one of: p1, p2, p3, p4, p5, p6, p7.*");
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("nearest")]
    public void EnsureValid_WhenDownscaleAlgoOverrideInvalid_ThrowsArgumentException(string downscaleAlgoOverride)
    {
        var sut = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            DownscaleAlgoOverride: downscaleAlgoOverride);

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithParameterName("DownscaleAlgoOverride")
            .WithMessage("*DownscaleAlgoOverride must be one of: bicubic, lanczos, bilinear.*");
    }

    [Theory]
    [InlineData(480)]
    [InlineData(1080)]
    public void EnsureValid_WhenDownscaleUnsupported_ThrowsArgumentException(int downscale)
    {
        var sut = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Downscale: downscale);

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Downscale")
            .WithMessage("*Downscale must be 576 or 720.*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(52)]
    public void EnsureValid_WhenCqOutOfRange_ThrowsArgumentException(int cq)
    {
        var sut = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Cq: cq);

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Cq")
            .WithMessage("*Cq must be in range 0..51.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EnsureValid_WhenMaxrateNotPositive_ThrowsArgumentException(double maxrate)
    {
        var sut = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Maxrate: maxrate);

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Maxrate")
            .WithMessage("*Maxrate must be greater than zero.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EnsureValid_WhenBufsizeNotPositive_ThrowsArgumentException(double bufsize)
    {
        var sut = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Bufsize: bufsize);

        var action = () => sut.EnsureValid();

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Bufsize")
            .WithMessage("*Bufsize must be greater than zero.*");
    }

    private static TranscodeRequest CreateRequestWithOverride(string propertyName, string value)
    {
        return propertyName switch
        {
            "ContentProfile" => new TranscodeRequest(InputPath: "C:\\video\\movie.mp4", ContentProfile: value),
            "QualityProfile" => new TranscodeRequest(InputPath: "C:\\video\\movie.mp4", QualityProfile: value),
            "AutoSampleMode" => new TranscodeRequest(InputPath: "C:\\video\\movie.mp4", AutoSampleMode: value),
            "NvencPreset" => new TranscodeRequest(InputPath: "C:\\video\\movie.mp4", NvencPreset: value),
            _ => throw new InvalidOperationException($"Unexpected property: {propertyName}")
        };
    }
}
