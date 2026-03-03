using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class TranscodeRequestTests
{
    [Fact]
    public void Create_WhenInputPathIsMissing_ThrowsArgumentException()
    {
        Action action = () => TranscodeRequest.Create(InputPath: " ");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("InputPath")
            .WithMessage("*InputPath is required.*");
    }

    [Fact]
    public void Create_WhenCalled_ReturnsNormalizedRequest()
    {
        var actual = TranscodeRequest.Create(
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
    public void Create_WhenDefaultsUsed_UsesContractDefaults()
    {
        var actual = TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4");

        actual.ContentProfile.Should().Be(RequestContracts.Transcode.DefaultContentProfile);
        actual.QualityProfile.Should().Be(RequestContracts.Transcode.DefaultQualityProfile);
        actual.AutoSampleMode.Should().Be(RequestContracts.Transcode.DefaultAutoSampleMode);
        actual.NvencPreset.Should().Be(RequestContracts.Transcode.DefaultNvencPreset);
        actual.DownscaleAlgoOverride.Should().BeNull();
    }

    [Fact]
    public void Create_WhenDownscaleAlgoOverrideIsWhitespace_SetsNull()
    {
        var actual = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            DownscaleAlgoOverride: " ");

        actual.DownscaleAlgoOverride.Should().BeNull();
    }

    [Theory]
    [InlineData(" ", "ContentProfile", "*ContentProfile is required.*")]
    [InlineData(" ", "QualityProfile", "*QualityProfile is required.*")]
    [InlineData(" ", "AutoSampleMode", "*AutoSampleMode is required.*")]
    [InlineData(" ", "NvencPreset", "*NvencPreset is required.*")]
    public void Create_WhenRequiredTextValueIsMissing_ThrowsArgumentException(
        string missingValue,
        string propertyName,
        string expectedMessage)
    {
        var action = CreateActionWithOverride(propertyName, missingValue);

        action.Should().Throw<ArgumentException>()
            .WithParameterName(propertyName)
            .WithMessage(expectedMessage);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("doc")]
    public void Create_WhenContentProfileInvalid_ThrowsArgumentException(string contentProfile)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            ContentProfile: contentProfile);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("ContentProfile")
            .WithMessage("*ContentProfile must be one of: anime, mult, film.*");
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("medium")]
    public void Create_WhenQualityProfileInvalid_ThrowsArgumentException(string qualityProfile)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            QualityProfile: qualityProfile);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("QualityProfile")
            .WithMessage("*QualityProfile must be one of: high, default, low.*");
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("adaptive")]
    public void Create_WhenAutoSampleModeInvalid_ThrowsArgumentException(string autoSampleMode)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            AutoSampleMode: autoSampleMode);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("AutoSampleMode")
            .WithMessage("*AutoSampleMode must be one of: accurate, fast, hybrid.*");
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("p8")]
    public void Create_WhenNvencPresetInvalid_ThrowsArgumentException(string nvencPreset)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            NvencPreset: nvencPreset);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("NvencPreset")
            .WithMessage("*NvencPreset must be one of: p1, p2, p3, p4, p5, p6, p7.*");
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("nearest")]
    public void Create_WhenDownscaleAlgoOverrideInvalid_ThrowsArgumentException(string downscaleAlgoOverride)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            DownscaleAlgoOverride: downscaleAlgoOverride);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("DownscaleAlgoOverride")
            .WithMessage("*DownscaleAlgoOverride must be one of: bicubic, lanczos, bilinear.*");
    }

    [Theory]
    [InlineData(480)]
    [InlineData(1080)]
    public void Create_WhenDownscaleUnsupported_ThrowsArgumentException(int downscale)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            Downscale: downscale);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Downscale")
            .WithMessage("*Downscale must be 576 or 720.*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(52)]
    public void Create_WhenCqOutOfRange_ThrowsArgumentException(int cq)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            Cq: cq);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Cq")
            .WithMessage("*Cq must be in range 0..51.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenMaxrateNotPositive_ThrowsArgumentException(double maxrate)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            Maxrate: maxrate);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Maxrate")
            .WithMessage("*Maxrate must be greater than zero.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenBufsizeNotPositive_ThrowsArgumentException(double bufsize)
    {
        Action action = () => TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            Bufsize: bufsize);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("Bufsize")
            .WithMessage("*Bufsize must be greater than zero.*");
    }

    private static Action CreateActionWithOverride(string propertyName, string value)
    {
        return propertyName switch
        {
            "ContentProfile" => () => TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4", ContentProfile: value),
            "QualityProfile" => () => TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4", QualityProfile: value),
            "AutoSampleMode" => () => TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4", AutoSampleMode: value),
            "NvencPreset" => () => TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4", NvencPreset: value),
            _ => throw new InvalidOperationException($"Unexpected property: {propertyName}")
        };
    }
}
