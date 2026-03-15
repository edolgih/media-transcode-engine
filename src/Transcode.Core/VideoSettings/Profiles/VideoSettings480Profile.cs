using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;

namespace Transcode.Core.VideoSettings.Profiles;

/*
Это фабрика video-settings профиля для целевой высоты 480.
Она возвращает типизированный профиль со своими defaults, corridor и bucket-правилами.
*/
/// <summary>
/// Builds the typed video-settings profile for target height 480.
/// </summary>
internal static class VideoSettings480Profile
{
    public static VideoSettingsProfile Create()
    {
        return new VideoSettingsProfile(
            targetHeight: 480,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new VideoSettingsRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
            autoSampling: new VideoSettingsAutoSampling(
                ModeDefault: "accurate",
                MaxIterations: 8,
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
                    "sd_576",
                    MinHeight: 481,
                    MaxHeight: 649,
                    Ranges:
                    [
                        new VideoSettingsRange("anime", "high", MinInclusive: 10.0m, MaxInclusive: 24.0m),
                        new VideoSettingsRange("anime", "default", MinInclusive: 24.0m, MaxInclusive: 38.0m),
                        new VideoSettingsRange("anime", "low", MinInclusive: 38.0m, MaxInclusive: 55.0m),
                        new VideoSettingsRange("mult", "high", MinInclusive: 8.0m, MaxInclusive: 22.0m),
                        new VideoSettingsRange("mult", "default", MinInclusive: 22.0m, MaxInclusive: 36.0m),
                        new VideoSettingsRange("mult", "low", MinInclusive: 36.0m, MaxInclusive: 52.0m),
                        new VideoSettingsRange("film", "high", MinInclusive: 6.0m, MaxInclusive: 18.0m),
                        new VideoSettingsRange("film", "default", MinInclusive: 18.0m, MaxInclusive: 32.0m),
                        new VideoSettingsRange("film", "low", MinInclusive: 32.0m, MaxInclusive: 48.0m)
                    ]),
                new SourceHeightBucket(
                    "hd_720",
                    MinHeight: 650,
                    MaxHeight: 899,
                    Ranges:
                    [
                        new VideoSettingsRange("anime", "high", MinInclusive: 20.0m, MaxInclusive: 34.0m),
                        new VideoSettingsRange("anime", "default", MinInclusive: 34.0m, MaxInclusive: 48.0m),
                        new VideoSettingsRange("anime", "low", MinInclusive: 48.0m, MaxInclusive: 66.0m),
                        new VideoSettingsRange("mult", "high", MinInclusive: 18.0m, MaxInclusive: 30.0m),
                        new VideoSettingsRange("mult", "default", MinInclusive: 30.0m, MaxInclusive: 44.0m),
                        new VideoSettingsRange("mult", "low", MinInclusive: 44.0m, MaxInclusive: 62.0m),
                        new VideoSettingsRange("film", "high", MinInclusive: 14.0m, MaxInclusive: 28.0m),
                        new VideoSettingsRange("film", "default", MinInclusive: 28.0m, MaxInclusive: 42.0m),
                        new VideoSettingsRange("film", "low", MinInclusive: 42.0m, MaxInclusive: 58.0m)
                    ],
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "high", CqMin: 16, MaxrateMax: 3.2m),
                        new VideoSettingsBoundsOverride("mult", "default", CqMin: 20, MaxrateMax: 2.6m),
                        new VideoSettingsBoundsOverride("mult", "low", CqMin: 25, MaxrateMax: 1.8m)
                    ]),
                new SourceHeightBucket(
                    "fhd_1080",
                    MinHeight: 900,
                    MaxHeight: 1300,
                    Ranges:
                    [
                        new VideoSettingsRange("anime", "high", MinInclusive: 35.0m, MaxInclusive: 50.0m),
                        new VideoSettingsRange("anime", "default", MinInclusive: 50.0m, MaxInclusive: 65.0m),
                        new VideoSettingsRange("anime", "low", MinInclusive: 65.0m, MaxInclusive: 82.0m),
                        new VideoSettingsRange("mult", "high", MinInclusive: 32.0m, MaxInclusive: 47.0m),
                        new VideoSettingsRange("mult", "default", MinInclusive: 47.0m, MaxInclusive: 62.0m),
                        new VideoSettingsRange("mult", "low", MinInclusive: 62.0m, MaxInclusive: 80.0m),
                        new VideoSettingsRange("film", "high", MinInclusive: 26.0m, MaxInclusive: 40.0m),
                        new VideoSettingsRange("film", "default", MinInclusive: 40.0m, MaxInclusive: 55.0m),
                        new VideoSettingsRange("film", "low", MinInclusive: 55.0m, MaxInclusive: 72.0m)
                    ],
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "low", CqMax: 34, MaxrateMin: 1.0m)
                    ])
            ],
            defaults:
            [
                new VideoSettingsDefaults("anime", "high", Cq: 23, Maxrate: 2.5m, Bufsize: 5.0m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 25, MaxrateMin: 1.8m, MaxrateMax: 3.2m),
                new VideoSettingsDefaults("anime", "default", Cq: 24, Maxrate: 1.8m, Bufsize: 3.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 21, CqMax: 27, MaxrateMin: 1.5m, MaxrateMax: 2.3m),
                new VideoSettingsDefaults("anime", "low", Cq: 30, Maxrate: 1.6m, Bufsize: 3.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 25, CqMax: 36, MaxrateMin: 0.8m, MaxrateMax: 2.4m),
                new VideoSettingsDefaults("mult", "high", Cq: 25, Maxrate: 2.0m, Bufsize: 4.0m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 22, CqMax: 27, MaxrateMin: 1.8m, MaxrateMax: 2.4m),
                new VideoSettingsDefaults("mult", "default", Cq: 27, Maxrate: 1.8m, Bufsize: 3.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 24, CqMax: 30, MaxrateMin: 1.5m, MaxrateMax: 2.1m),
                new VideoSettingsDefaults("mult", "low", Cq: 30, Maxrate: 1.3m, Bufsize: 2.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 27, CqMax: 32, MaxrateMin: 1.2m, MaxrateMax: 1.5m),
                new VideoSettingsDefaults("film", "high", Cq: 25, Maxrate: 2.8m, Bufsize: 5.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 17, CqMax: 34, MaxrateMin: 1.5m, MaxrateMax: 6.0m),
                new VideoSettingsDefaults("film", "default", Cq: 27, Maxrate: 2.6m, Bufsize: 5.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 19, CqMax: 36, MaxrateMin: 1.2m, MaxrateMax: 6.0m),
                new VideoSettingsDefaults("film", "low", Cq: 31, Maxrate: 1.7m, Bufsize: 3.4m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 21, CqMax: 39, MaxrateMin: 0.9m, MaxrateMax: 3.0m)
            ],
            globalQualityRanges:
            [
                new VideoSettingsQualityRange("high", MinInclusive: 30.0m, MaxInclusive: 45.0m),
                new VideoSettingsQualityRange("default", MinExclusive: 45.0m, MaxInclusive: 58.0m),
                new VideoSettingsQualityRange("low", MinExclusive: 58.0m)
            ],
            globalContentRanges:
            [
                new VideoSettingsRange("anime", "high", MinInclusive: 30.0m, MaxInclusive: 45.0m),
                new VideoSettingsRange("anime", "default", MinExclusive: 45.0m, MaxInclusive: 58.0m),
                new VideoSettingsRange("anime", "low", MinExclusive: 58.0m, MaxInclusive: 85.0m),
                new VideoSettingsRange("mult", "high", MinInclusive: 28.0m, MaxInclusive: 43.0m),
                new VideoSettingsRange("mult", "default", MinExclusive: 43.0m, MaxInclusive: 56.0m),
                new VideoSettingsRange("mult", "low", MinExclusive: 56.0m, MaxInclusive: 82.0m),
                new VideoSettingsRange("film", "high", MinInclusive: 22.0m, MaxInclusive: 38.0m),
                new VideoSettingsRange("film", "default", MinExclusive: 38.0m, MaxInclusive: 52.0m),
                new VideoSettingsRange("film", "low", MinExclusive: 52.0m, MaxInclusive: 78.0m)
            ]);
    }
}
