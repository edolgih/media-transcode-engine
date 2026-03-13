using FluentAssertions;
using MediaTranscodeEngine.Runtime.VideoSettings;
using MediaTranscodeEngine.Runtime.VideoSettings.Profiles;

namespace MediaTranscodeEngine.Runtime.Tests.VideoSettings;

/*
Это тесты autosample-логики video settings.
Они проверяют выбор режима, окон измерения и расчет итоговых ограничений по каталогу профилей.
*/
/// <summary>
/// Verifies autosample mode selection and bitrate-resolution behavior for video settings.
/// </summary>
public sealed class VideoSettingsAutoSamplerTests
{
    [Fact]
    public void Resolve_WhenModeAccurate_UsesAccurateReductionProviderAndLongWindows()
    {
        var profiles = VideoSettingsProfiles.Default;
        var sut = new VideoSettingsAutoSampler(profiles);
        var profile = profiles.GetRequiredProfile(576);
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = ResolveAnimeDefault();
        IReadOnlyList<VideoSettingsSampleWindow>? actualWindows = null;
        var callCount = 0;

        var actual = sut.Resolve(
            profile,
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
            new VideoSettingsSampleWindow(StartSeconds: 105, DurationSeconds: 30),
            new VideoSettingsSampleWindow(StartSeconds: 285, DurationSeconds: 30),
            new VideoSettingsSampleWindow(StartSeconds: 465, DurationSeconds: 30));
    }

    [Fact]
    public void Resolve_WhenModeAccurateAndReductionAboveCorridor_DecreasesCqAndIncreasesMaxrate()
    {
        var profiles = CreateProfiles(maxIterations: 1);
        var sut = new VideoSettingsAutoSampler(profiles);
        var profile = profiles.GetRequiredProfile(576);
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = ResolveAnimeDefault();

        var actual = sut.Resolve(
            profile,
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
        var profiles = VideoSettingsProfiles.Default;
        var sut = new VideoSettingsAutoSampler(profiles);
        var profile = profiles.GetRequiredProfile(576);
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = ResolveAnimeDefault();

        var actual = sut.Resolve(
            profile,
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
        var profiles = CreateProfiles(maxIterations: 1);
        var sut = new VideoSettingsAutoSampler(profiles);
        var profile = profiles.GetRequiredProfile(576);
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = ResolveAnimeDefault();

        var actual = sut.Resolve(
            profile,
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
        var profiles = VideoSettingsProfiles.Create(
            new VideoSettingsProfile(
                targetHeight: 576,
                defaultContentProfile: "anime",
                defaultQualityProfile: "default",
                rateModel: new VideoSettingsRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
                autoSampling: CreateAutoSampling(maxIterations: 8, hybridAccurateIterations: 2),
                sourceBuckets:
                [
                    new SourceHeightBucket(
                        "fhd",
                        MinHeight: 1000,
                        MaxHeight: 1300,
                        Ranges:
                        [
                            new VideoSettingsRange("anime", "default", MinInclusive: 45.0m, MaxInclusive: 60.0m)
                        ])
                ],
                defaults:
                [
                    new VideoSettingsDefaults("anime", "default", Cq: 26, Maxrate: 2.0m, Bufsize: 4.0m, Algorithm: "bilinear", CqMin: 26, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 2.0m)
                ]));
        var sut = new VideoSettingsAutoSampler(profiles);
        var profile = profiles.GetRequiredProfile(576);
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = sutResolveDefaults();

        var actual = sut.Resolve(
            profile,
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
        var profiles = VideoSettingsProfiles.Create(
            new VideoSettingsProfile(
                targetHeight: 576,
                defaultContentProfile: "anime",
                defaultQualityProfile: "default",
                rateModel: new VideoSettingsRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
                autoSampling: CreateAutoSampling(maxIterations: 1, hybridAccurateIterations: 1),
                sourceBuckets:
                [
                    new SourceHeightBucket(
                        "fhd",
                        MinHeight: 1000,
                        MaxHeight: 1300,
                        Ranges:
                        [
                            new VideoSettingsRange("anime", "default", MinExclusive: 80.0m, MaxInclusive: 90.0m)
                        ])
                ],
                defaults:
                [
                    new VideoSettingsDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: "bilinear", CqMin: 20, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 3.0m)
                ],
                globalContentRanges:
                [
                    new VideoSettingsRange("anime", "default", MinExclusive: 40.0m, MaxInclusive: 50.0m)
                ]));
        var sut = new VideoSettingsAutoSampler(profiles);
        var profile = profiles.GetRequiredProfile(576);
        var request = CreateRequest(autoSampleMode: "accurate");
        var baseSettings = new VideoSettingsDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: "bilinear", CqMin: 20, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 3.0m);

        var actual = sut.Resolve(
            profile,
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
        var profiles = CreateProfiles(maxIterations: 1, hybridAccurateIterations: 1);
        var sut = new VideoSettingsAutoSampler(profiles);
        var profile = profiles.GetRequiredProfile(576);
        var request = CreateRequest(autoSampleMode: "hybrid");
        var baseSettings = ResolveAnimeDefault();
        var accurateCalls = 0;

        var actual = sut.Resolve(
            profile,
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
        var profiles = CreateProfiles(maxIterations: 1, hybridAccurateIterations: 1);
        var sut = new VideoSettingsAutoSampler(profiles);
        var profile = profiles.GetRequiredProfile(576);
        var request = CreateRequest(autoSampleMode: "hybrid");
        var baseSettings = ResolveAnimeDefault();
        VideoSettingsDefaults? accurateStart = null;

        var actual = sut.Resolve(
            profile,
            request,
            baseSettings,
            sourceHeight: 1080,
            duration: TimeSpan.FromMinutes(10),
            sourceBitrate: 4_000_000,
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

    private static VideoSettingsRequest CreateRequest(
        string? autoSampleMode = null)
    {
        return new VideoSettingsRequest(
            contentProfile: "anime",
            qualityProfile: "default",
            autoSampleMode: autoSampleMode);
    }

    private static VideoSettingsDefaults ResolveAnimeDefault()
    {
        return VideoSettingsProfiles.Default.GetRequiredProfile(576).ResolveDefaults("anime", "default");
    }

    private static VideoSettingsProfiles CreateProfiles(int maxIterations, int hybridAccurateIterations = 2)
    {
        var profile = VideoSettings576Profile.Create();
        return VideoSettingsProfiles.Create(
            new VideoSettingsProfile(
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

    private static VideoSettingsAutoSampling CreateAutoSampling(int maxIterations, int hybridAccurateIterations)
    {
        return new VideoSettingsAutoSampling(
            EnabledByDefault: true,
            ModeDefault: "accurate",
            MaxIterations: maxIterations,
            HybridAccurateIterations: hybridAccurateIterations,
            AudioBitrateEstimateMbps: 0.192m,
            LongMinDuration: TimeSpan.FromMinutes(8),
            LongWindowCount: 3,
            LongWindowAnchors: [0.20, 0.50, 0.80],
            MediumMinDuration: TimeSpan.FromMinutes(3),
            MediumWindowCount: 2,
            MediumWindowAnchors: [0.35, 0.65],
            ShortWindowCount: 1,
            SampleWindowDuration: TimeSpan.FromSeconds(15),
            ShortWindowAnchors: [0.50]);
    }

    private static VideoSettingsDefaults sutResolveDefaults()
    {
        return new VideoSettingsDefaults("anime", "default", Cq: 26, Maxrate: 2.0m, Bufsize: 4.0m, Algorithm: "bilinear", CqMin: 26, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 2.0m);
    }
}
