using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.VideoSettings.Profiles;

/*
Это фабрика профиля video settings для output bucket 1080.
Она задаёт quality-oriented defaults и общие autosample-коридоры для full-hd encode path.
*/
/// <summary>
/// Builds the typed profile for output height bucket 1080.
/// </summary>
internal static class VideoSettings1080Profile
{
    public static VideoSettingsProfile Create()
    {
        return new VideoSettingsProfile(
            targetHeight: 1080,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new VideoSettingsRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
            autoSampling: new VideoSettingsAutoSampling(
                EnabledByDefault: true,
                ModeDefault: "accurate",
                MaxIterations: 8,
                HybridAccurateIterations: 2,
                AudioBitrateEstimateMbps: 0.192m,
                LongMinDuration: TimeSpan.FromMinutes(8),
                LongWindowCount: 3,
                LongWindowAnchors: [0.20, 0.50, 0.80],
                MediumMinDuration: TimeSpan.FromMinutes(3),
                MediumWindowCount: 2,
                MediumWindowAnchors: [0.35, 0.65],
                ShortWindowCount: 1,
                SampleWindowDuration: TimeSpan.FromSeconds(30),
                ShortWindowAnchors: [0.50]),
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "qhd_1440",
                    MinHeight: 1301,
                    MaxHeight: 1799,
                    Ranges:
                    [
                        new VideoSettingsRange("anime", "high", MinInclusive: 18.0m, MaxInclusive: 32.0m),
                        new VideoSettingsRange("anime", "default", MinInclusive: 32.0m, MaxInclusive: 44.0m),
                        new VideoSettingsRange("anime", "low", MinInclusive: 44.0m, MaxInclusive: 60.0m),
                        new VideoSettingsRange("mult", "high", MinInclusive: 22.0m, MaxInclusive: 36.0m),
                        new VideoSettingsRange("mult", "default", MinInclusive: 36.0m, MaxInclusive: 49.0m),
                        new VideoSettingsRange("mult", "low", MinInclusive: 49.0m, MaxInclusive: 65.0m),
                        new VideoSettingsRange("film", "high", MinInclusive: 14.0m, MaxInclusive: 28.0m),
                        new VideoSettingsRange("film", "default", MinInclusive: 28.0m, MaxInclusive: 40.0m),
                        new VideoSettingsRange("film", "low", MinInclusive: 40.0m, MaxInclusive: 56.0m)
                    ],
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "high", CqMin: 17, MaxrateMax: 4.8m),
                        new VideoSettingsBoundsOverride("mult", "default", CqMin: 20, MaxrateMax: 4.0m),
                        new VideoSettingsBoundsOverride("mult", "low", CqMin: 24, MaxrateMax: 3.0m)
                    ]),
                new SourceHeightBucket(
                    "uhd_2160",
                    MinHeight: 1800,
                    MaxHeight: 2600,
                    Ranges:
                    [
                        new VideoSettingsRange("anime", "high", MinInclusive: 25.0m, MaxInclusive: 38.0m),
                        new VideoSettingsRange("anime", "default", MinInclusive: 38.0m, MaxInclusive: 52.0m),
                        new VideoSettingsRange("anime", "low", MinInclusive: 52.0m, MaxInclusive: 72.0m),
                        new VideoSettingsRange("mult", "high", MinInclusive: 30.0m, MaxInclusive: 43.0m),
                        new VideoSettingsRange("mult", "default", MinInclusive: 43.0m, MaxInclusive: 58.0m),
                        new VideoSettingsRange("mult", "low", MinInclusive: 58.0m, MaxInclusive: 78.0m),
                        new VideoSettingsRange("film", "high", MinInclusive: 20.0m, MaxInclusive: 33.0m),
                        new VideoSettingsRange("film", "default", MinInclusive: 33.0m, MaxInclusive: 48.0m),
                        new VideoSettingsRange("film", "low", MinInclusive: 48.0m, MaxInclusive: 66.0m)
                    ],
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "low", CqMax: 32, MaxrateMin: 2.0m)
                    ])
            ],
            defaults:
            [
                new VideoSettingsDefaults("anime", "high", Cq: 20, Maxrate: 4.2m, Bufsize: 8.4m, Algorithm: "bilinear", CqMin: 17, CqMax: 24, MaxrateMin: 2.8m, MaxrateMax: 5.0m),
                new VideoSettingsDefaults("anime", "default", Cq: 21, Maxrate: 3.4m, Bufsize: 6.8m, Algorithm: "bilinear", CqMin: 18, CqMax: 26, MaxrateMin: 2.4m, MaxrateMax: 4.0m),
                new VideoSettingsDefaults("anime", "low", Cq: 27, Maxrate: 2.6m, Bufsize: 5.2m, Algorithm: "bilinear", CqMin: 22, CqMax: 34, MaxrateMin: 1.4m, MaxrateMax: 3.6m),
                new VideoSettingsDefaults("mult", "high", Cq: 21, Maxrate: 3.6m, Bufsize: 7.2m, Algorithm: "bilinear", CqMin: 18, CqMax: 25, MaxrateMin: 2.6m, MaxrateMax: 4.4m),
                new VideoSettingsDefaults("mult", "default", Cq: 23, Maxrate: 3.0m, Bufsize: 6.0m, Algorithm: "bilinear", CqMin: 20, CqMax: 28, MaxrateMin: 2.2m, MaxrateMax: 3.8m),
                new VideoSettingsDefaults("mult", "low", Cq: 27, Maxrate: 2.2m, Bufsize: 4.4m, Algorithm: "bilinear", CqMin: 24, CqMax: 31, MaxrateMin: 1.6m, MaxrateMax: 2.8m),
                new VideoSettingsDefaults("film", "high", Cq: 20, Maxrate: 5.6m, Bufsize: 11.2m, Algorithm: "bilinear", CqMin: 15, CqMax: 31, MaxrateMin: 2.8m, MaxrateMax: 8.0m),
                new VideoSettingsDefaults("film", "default", Cq: 21, Maxrate: 5.2m, Bufsize: 10.4m, Algorithm: "bilinear", CqMin: 16, CqMax: 33, MaxrateMin: 2.4m, MaxrateMax: 8.0m),
                new VideoSettingsDefaults("film", "low", Cq: 26, Maxrate: 3.6m, Bufsize: 7.2m, Algorithm: "bilinear", CqMin: 18, CqMax: 36, MaxrateMin: 1.8m, MaxrateMax: 5.0m)
            ],
            globalQualityRanges:
            [
                new VideoSettingsQualityRange("high", MinInclusive: 25.0m, MaxInclusive: 40.0m),
                new VideoSettingsQualityRange("default", MinExclusive: 40.0m, MaxInclusive: 50.0m),
                new VideoSettingsQualityRange("low", MinExclusive: 50.0m)
            ],
            globalContentRanges:
            [
                new VideoSettingsRange("anime", "high", MinInclusive: 25.0m, MaxInclusive: 40.0m),
                new VideoSettingsRange("anime", "default", MinExclusive: 40.0m, MaxInclusive: 50.0m),
                new VideoSettingsRange("anime", "low", MinExclusive: 50.0m, MaxInclusive: 80.0m),
                new VideoSettingsRange("mult", "high", MinInclusive: 30.0m, MaxInclusive: 45.0m),
                new VideoSettingsRange("mult", "default", MinExclusive: 45.0m, MaxInclusive: 58.0m),
                new VideoSettingsRange("mult", "low", MinExclusive: 58.0m, MaxInclusive: 85.0m),
                new VideoSettingsRange("film", "high", MinInclusive: 20.0m, MaxInclusive: 38.0m),
                new VideoSettingsRange("film", "default", MinExclusive: 38.0m, MaxInclusive: 52.0m),
                new VideoSettingsRange("film", "low", MinExclusive: 52.0m, MaxInclusive: 78.0m)
            ]);
    }
}
