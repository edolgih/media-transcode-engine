using FluentAssertions;
using MediaTranscodeEngine.Runtime.Downscaling;

namespace MediaTranscodeEngine.Runtime.Tests.Downscaling;

public sealed class DownscaleAutoSamplerTests
{
    [Fact]
    public void Resolve_WhenModeAccurate_UsesAccurateReductionProviderAndLongWindows()
    {
        var sut = new DownscaleAutoSampler(DownscaleProfiles.Default);
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = ResolveAnimeDefault();
        IReadOnlyList<DownscaleSampleWindow>? actualWindows = null;
        var callCount = 0;

        var actual = sut.Resolve(
            request,
            baseSettings,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: null,
            hasAudio: true,
            accurateReductionProvider: (_, windows) =>
            {
                callCount++;
                actualWindows = windows;
                return 45m;
            });

        callCount.Should().Be(1);
        actual.Cq.Should().Be(23);
        actual.Maxrate.Should().Be(2.4m);
        actual.Bufsize.Should().Be(4.8m);
        actualWindows.Should().Equal(
            new DownscaleSampleWindow(StartSeconds: 60, DurationSeconds: 120),
            new DownscaleSampleWindow(StartSeconds: 240, DurationSeconds: 120),
            new DownscaleSampleWindow(StartSeconds: 420, DurationSeconds: 120));
    }

    [Fact]
    public void Resolve_WhenModeAccurateAndReductionAboveCorridor_DecreasesCqAndIncreasesMaxrate()
    {
        var sut = new DownscaleAutoSampler(CreateProfiles(maxIterations: 1));
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = ResolveAnimeDefault();

        var actual = sut.Resolve(
            request,
            baseSettings,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: null,
            hasAudio: true,
            accurateReductionProvider: (_, _) => 70m);

        actual.Cq.Should().Be(22);
        actual.Maxrate.Should().Be(2.8m);
        actual.Bufsize.Should().Be(5.6m);
    }

    [Fact]
    public void Resolve_WhenAccurateReductionIsNull_ReturnsBaseSettings()
    {
        var sut = new DownscaleAutoSampler(DownscaleProfiles.Default);
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = ResolveAnimeDefault();

        var actual = sut.Resolve(
            request,
            baseSettings,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: null,
            hasAudio: true,
            accurateReductionProvider: (_, _) => null);

        actual.Should().Be(baseSettings);
    }

    [Fact]
    public void Resolve_WhenMaxIterationsReached_ReturnsLastResolvedSettings()
    {
        var sut = new DownscaleAutoSampler(CreateProfiles(maxIterations: 1));
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = ResolveAnimeDefault();

        var actual = sut.Resolve(
            request,
            baseSettings,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: null,
            hasAudio: true,
            accurateReductionProvider: (_, _) => 30m);

        actual.Cq.Should().Be(24);
        actual.Maxrate.Should().Be(2.0m);
        actual.Bufsize.Should().Be(4.0m);
    }

    [Fact]
    public void Resolve_WhenLimitsAreReachedAndStillOutsideCorridor_StopsWithoutFurtherChanges()
    {
        var sut = new DownscaleAutoSampler(DownscaleProfiles.Create(
            new DownscaleProfile(
                targetHeight: 576,
                defaultContentProfile: "anime",
                defaultQualityProfile: "default",
                rateModel: new DownscaleRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
                autoSampling: CreateAutoSampling(maxIterations: 8, hybridAccurateIterations: 2),
                sourceBuckets:
                [
                    new SourceHeightBucket(
                        "fhd",
                        MinHeight: 1000,
                        MaxHeight: 1300,
                        Ranges:
                        [
                            new DownscaleRange("anime", "default", MinInclusive: 45.0m, MaxInclusive: 60.0m)
                        ])
                ],
                defaults:
                [
                    new DownscaleDefaults("anime", "default", Cq: 26, Maxrate: 2.0m, Bufsize: 4.0m, Algorithm: "bilinear", CqMin: 26, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 2.0m)
                ])));
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = sutResolveDefaults();

        var actual = sut.Resolve(
            request,
            baseSettings,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: null,
            hasAudio: true,
            accurateReductionProvider: (_, _) => 30m);

        actual.Cq.Should().Be(26);
        actual.Maxrate.Should().Be(2.0m);
        actual.Bufsize.Should().Be(4.0m);
    }

    [Fact]
    public void Resolve_WhenMatchedBucketRangeDiffers_UsesMatchedBucketBounds()
    {
        var sut = new DownscaleAutoSampler(DownscaleProfiles.Create(
            new DownscaleProfile(
                targetHeight: 576,
                defaultContentProfile: "anime",
                defaultQualityProfile: "default",
                rateModel: new DownscaleRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
                autoSampling: CreateAutoSampling(maxIterations: 1, hybridAccurateIterations: 1),
                sourceBuckets:
                [
                    new SourceHeightBucket(
                        "fhd",
                        MinHeight: 1000,
                        MaxHeight: 1300,
                        Ranges:
                        [
                            new DownscaleRange("anime", "default", MinExclusive: 80.0m, MaxInclusive: 90.0m)
                        ])
                ],
                defaults:
                [
                    new DownscaleDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: "bilinear", CqMin: 20, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 3.0m)
                ],
                globalContentRanges:
                [
                    new DownscaleRange("anime", "default", MinExclusive: 40.0m, MaxInclusive: 50.0m)
                ])));
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = new DownscaleDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: "bilinear", CqMin: 20, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 3.0m);

        var actual = sut.Resolve(
            request,
            baseSettings,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: null,
            hasAudio: true,
            accurateReductionProvider: (_, _) => 45m);

        actual.Cq.Should().Be(24);
        actual.Maxrate.Should().Be(2.0m);
        actual.Bufsize.Should().Be(4.0m);
    }

    [Fact]
    public void Resolve_WhenModeHybridAndFastEstimateIsWithinCorridor_SkipsAccurate()
    {
        var sut = new DownscaleAutoSampler(CreateProfiles(maxIterations: 1, hybridAccurateIterations: 1));
        var request = CreateRequest(autoSampleMode: "hybrid");
        var baseSettings = ResolveAnimeDefault();
        var accurateCalls = 0;

        var actual = sut.Resolve(
            request,
            baseSettings,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: 5_184_000,
            hasAudio: true,
            accurateReductionProvider: (_, _) =>
            {
                accurateCalls++;
                return 30m;
            });

        accurateCalls.Should().Be(0);
        actual.Should().Be(baseSettings);
    }

    [Fact]
    public void Resolve_WhenModeHybridAndFastEstimateIsOutsideCorridor_RunsAccurateFromFastSeed()
    {
        var sut = new DownscaleAutoSampler(CreateProfiles(maxIterations: 1, hybridAccurateIterations: 1));
        var request = CreateRequest(autoSampleMode: "hybrid");
        var baseSettings = ResolveAnimeDefault();
        DownscaleDefaults? accurateStart = null;

        var actual = sut.Resolve(
            request,
            baseSettings,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: 4_500_000,
            hasAudio: true,
            accurateReductionProvider: (settings, _) =>
            {
                accurateStart = settings;
                return 45m;
            });

        accurateStart.Should().NotBeNull();
        accurateStart!.Cq.Should().Be(24);
        accurateStart.Maxrate.Should().Be(2.0m);
        actual.Cq.Should().Be(24);
        actual.Maxrate.Should().Be(2.0m);
        actual.Bufsize.Should().Be(4.0m);
    }

    private static DownscaleRequest CreateRequest(
        string? autoSampleMode = null,
        bool noAutoSample = false)
    {
        return new DownscaleRequest(
            targetHeight: 576,
            contentProfile: "anime",
            qualityProfile: "default",
            noAutoSample: noAutoSample,
            autoSampleMode: autoSampleMode);
    }

    private static DownscaleDefaults ResolveAnimeDefault()
    {
        return DownscaleProfiles.Default.GetRequiredProfile(576).ResolveDefaults("anime", "default");
    }

    private static DownscaleProfiles CreateProfiles(int maxIterations, int hybridAccurateIterations = 2)
    {
        var profile = Downscale576Profile.Create();
        return DownscaleProfiles.Create(
            new DownscaleProfile(
                targetHeight: profile.TargetHeight,
                defaultContentProfile: profile.DefaultContentProfile,
                defaultQualityProfile: profile.DefaultQualityProfile,
                rateModel: profile.RateModel,
                autoSampling: profile.AutoSampling with
                {
                    MaxIterations = maxIterations,
                    HybridAccurateIterations = hybridAccurateIterations
                },
                sourceBuckets: profile.SourceBuckets,
                defaults: profile.Defaults));
    }

    private static DownscaleAutoSampling CreateAutoSampling(int maxIterations, int hybridAccurateIterations)
    {
        return new DownscaleAutoSampling(
            EnabledByDefault: true,
            ModeDefault: "accurate",
            MaxIterations: maxIterations,
            HybridAccurateIterations: hybridAccurateIterations,
            AudioBitrateEstimateMbps: 0.192m,
            LongMinDuration: TimeSpan.FromMinutes(8),
            LongWindowCount: 3,
            LongWindowDuration: TimeSpan.FromSeconds(120),
            MediumMinDuration: TimeSpan.FromMinutes(3),
            MediumWindowCount: 2,
            MediumWindowDuration: TimeSpan.FromSeconds(120),
            ShortWindowCount: 1,
            ShortWindowDuration: TimeSpan.FromSeconds(90));
    }

    private static DownscaleDefaults sutResolveDefaults()
    {
        return new DownscaleDefaults("anime", "default", Cq: 26, Maxrate: 2.0m, Bufsize: 4.0m, Algorithm: "bilinear", CqMin: 26, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 2.0m);
    }
}
