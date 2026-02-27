using FluentAssertions;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Policy;

public class TranscodePolicyTests
{
    [Fact]
    public void Resolve576Settings_WhenNoOverrides_UsesProfileDefaults()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var input = CreateInput();

        var actual = sut.Resolve576Settings(config, input);

        actual.Cq.Should().Be(23);
        actual.Maxrate.Should().Be(2.4);
        actual.Bufsize.Should().Be(4.8);
        actual.DownscaleAlgo.Should().Be("bilinear");
    }

    [Fact]
    public void Resolve576Settings_WhenContentProfileNotSupported_ThrowsArgumentException()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var input = CreateInput(contentProfile: "unknown");

        var act = () => sut.Resolve576Settings(config, input);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unsupported ContentProfile*");
    }

    [Fact]
    public void Resolve576Settings_WhenQualityProfileNotSupported_ThrowsArgumentException()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var input = CreateInput(qualityProfile: "ultra");

        var act = () => sut.Resolve576Settings(config, input);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unsupported QualityProfile*");
    }

    [Fact]
    public void Resolve576Settings_WhenCqOverrideProvided_RecomputesMaxrateAndBufsize()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var input = CreateInput(cq: 21);

        var actual = sut.Resolve576Settings(config, input);

        actual.Cq.Should().Be(21);
        actual.Maxrate.Should().Be(3.0);
        actual.Bufsize.Should().Be(6.0);
    }

    [Fact]
    public void Resolve576Settings_WhenCqOverrideProducesMaxrateAboveLimit_ClampsToUpperLimit()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var input = CreateInput(cq: 10);

        var actual = sut.Resolve576Settings(config, input);

        actual.Cq.Should().Be(10);
        actual.Maxrate.Should().Be(3.0);
        actual.Bufsize.Should().Be(6.0);
    }

    [Fact]
    public void Resolve576Settings_WhenCqOverrideProducesMaxrateBelowLimit_ClampsToLowerLimit()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var input = CreateInput(cq: 40);

        var actual = sut.Resolve576Settings(config, input);

        actual.Cq.Should().Be(40);
        actual.Maxrate.Should().Be(2.0);
        actual.Bufsize.Should().Be(4.0);
    }

    [Fact]
    public void Resolve576Settings_WhenMaxrateOverrideProvided_ComputesBufsizeFromMultiplier()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var input = CreateInput(maxrate: 2.6);

        var actual = sut.Resolve576Settings(config, input);

        actual.Cq.Should().Be(23);
        actual.Maxrate.Should().Be(2.6);
        actual.Bufsize.Should().Be(5.2);
    }

    [Fact]
    public void Resolve576Settings_WhenExplicitBufsizeProvided_UsesExplicitBufsize()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var input = CreateInput(maxrate: 2.6, bufsize: 6.8);

        var actual = sut.Resolve576Settings(config, input);

        actual.Maxrate.Should().Be(2.6);
        actual.Bufsize.Should().Be(6.8);
    }

    [Fact]
    public void Resolve576Settings_WhenDownscaleAlgoOverrideProvided_UsesOverride()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var input = CreateInput(downscaleAlgo: "lanczos");

        var actual = sut.Resolve576Settings(config, input);

        actual.DownscaleAlgo.Should().Be("lanczos");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Resolve576Settings_WhenDownscaleAlgoOverrideBlank_UsesDefault(string? downscaleAlgo)
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var input = CreateInput(downscaleAlgo: downscaleAlgo);

        var actual = sut.Resolve576Settings(config, input);

        actual.DownscaleAlgo.Should().Be("bilinear");
    }

    private static TranscodePolicy CreateSut()
    {
        return new TranscodePolicy();
    }

    private static TranscodePolicyConfig CreateConfig()
    {
        var defaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8)
        };

        var limits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileLimits(CqMin: 20, CqMax: 26, MaxrateMin: 2.0, MaxrateMax: 3.0)
        };

        var contentProfiles = new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new ContentProfileSettings(
                AlgoDefault: "bilinear",
                Defaults: defaults,
                Limits: limits)
        };

        return new TranscodePolicyConfig(
            ContentProfiles: contentProfiles,
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0));
    }

    private static TranscodePolicyInput CreateInput(
        string contentProfile = "anime",
        string qualityProfile = "default",
        int? cq = null,
        double? maxrate = null,
        double? bufsize = null,
        string? downscaleAlgo = null)
    {
        return new TranscodePolicyInput(
            ContentProfile: contentProfile,
            QualityProfile: qualityProfile,
            Cq: cq,
            Maxrate: maxrate,
            Bufsize: bufsize,
            DownscaleAlgo: downscaleAlgo);
    }
}
