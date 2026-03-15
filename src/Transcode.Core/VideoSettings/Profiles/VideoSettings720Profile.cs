using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;

namespace Transcode.Core.VideoSettings.Profiles;

/*
Это фабрика профиля video settings для output bucket 720.
Она задаёт quality-oriented defaults и общие autosample-коридоры для 720-пути.
*/
/// <summary>
/// Builds the typed profile for output height bucket 720.
/// </summary>
internal static class VideoSettings720Profile
{
    public static VideoSettingsProfile Create()
    {
        return new VideoSettingsProfile(
            targetHeight: 720,
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
                    "fhd_1080",
                    MinHeight: 1000,
                    MaxHeight: 1300,
                    Ranges:
                    [
                        new VideoSettingsRange("anime", "high", MinInclusive: 20.0m, MaxInclusive: 35.0m),
                        new VideoSettingsRange("anime", "default", MinInclusive: 35.0m, MaxInclusive: 48.0m),
                        new VideoSettingsRange("anime", "low", MinInclusive: 48.0m, MaxInclusive: 66.0m),
                        new VideoSettingsRange("mult", "high", MinInclusive: 24.0m, MaxInclusive: 39.0m),
                        new VideoSettingsRange("mult", "default", MinInclusive: 39.0m, MaxInclusive: 54.0m),
                        new VideoSettingsRange("mult", "low", MinInclusive: 54.0m, MaxInclusive: 72.0m),
                        new VideoSettingsRange("film", "high", MinInclusive: 16.0m, MaxInclusive: 30.0m),
                        new VideoSettingsRange("film", "default", MinInclusive: 30.0m, MaxInclusive: 45.0m),
                        new VideoSettingsRange("film", "low", MinInclusive: 45.0m, MaxInclusive: 62.0m)
                    ],
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "high", CqMin: 18, MaxrateMax: 4.4m),
                        new VideoSettingsBoundsOverride("mult", "default", CqMin: 21, MaxrateMax: 3.6m),
                        new VideoSettingsBoundsOverride("mult", "low", CqMin: 25, MaxrateMax: 2.6m)
                    ]),
                new SourceHeightBucket(
                    "uhd_2160",
                    MinHeight: 1800,
                    MaxHeight: 2600,
                    Ranges:
                    [
                        new VideoSettingsRange("anime", "high", MinInclusive: 30.0m, MaxInclusive: 45.0m),
                        new VideoSettingsRange("anime", "default", MinInclusive: 45.0m, MaxInclusive: 60.0m),
                        new VideoSettingsRange("anime", "low", MinInclusive: 60.0m, MaxInclusive: 80.0m),
                        new VideoSettingsRange("mult", "high", MinInclusive: 36.0m, MaxInclusive: 50.0m),
                        new VideoSettingsRange("mult", "default", MinInclusive: 50.0m, MaxInclusive: 66.0m),
                        new VideoSettingsRange("mult", "low", MinInclusive: 66.0m, MaxInclusive: 86.0m),
                        new VideoSettingsRange("film", "high", MinInclusive: 24.0m, MaxInclusive: 38.0m),
                        new VideoSettingsRange("film", "default", MinInclusive: 38.0m, MaxInclusive: 54.0m),
                        new VideoSettingsRange("film", "low", MinInclusive: 54.0m, MaxInclusive: 72.0m)
                    ],
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "low", CqMax: 33, MaxrateMin: 1.8m)
                    ])
            ],
            defaults:
            [
                new VideoSettingsDefaults("anime", "high", Cq: 22, Maxrate: 3.6m, Bufsize: 7.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 19, CqMax: 25, MaxrateMin: 2.4m, MaxrateMax: 4.2m),
                new VideoSettingsDefaults("anime", "default", Cq: 23, Maxrate: 2.8m, Bufsize: 5.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 27, MaxrateMin: 2.0m, MaxrateMax: 3.4m),
                new VideoSettingsDefaults("anime", "low", Cq: 29, Maxrate: 2.3m, Bufsize: 4.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 24, CqMax: 35, MaxrateMin: 1.2m, MaxrateMax: 3.2m),
                new VideoSettingsDefaults("mult", "high", Cq: 23, Maxrate: 3.0m, Bufsize: 6.0m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 26, MaxrateMin: 2.4m, MaxrateMax: 3.8m),
                new VideoSettingsDefaults("mult", "default", Cq: 25, Maxrate: 2.6m, Bufsize: 5.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 22, CqMax: 29, MaxrateMin: 2.0m, MaxrateMax: 3.2m),
                new VideoSettingsDefaults("mult", "low", Cq: 29, Maxrate: 1.9m, Bufsize: 3.8m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 25, CqMax: 32, MaxrateMin: 1.6m, MaxrateMax: 2.4m),
                new VideoSettingsDefaults("film", "high", Cq: 22, Maxrate: 4.8m, Bufsize: 9.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 16, CqMax: 32, MaxrateMin: 2.4m, MaxrateMax: 8.0m),
                new VideoSettingsDefaults("film", "default", Cq: 23, Maxrate: 4.5m, Bufsize: 9.0m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 17, CqMax: 34, MaxrateMin: 2.0m, MaxrateMax: 8.0m),
                new VideoSettingsDefaults("film", "low", Cq: 28, Maxrate: 3.0m, Bufsize: 6.0m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 19, CqMax: 37, MaxrateMin: 1.4m, MaxrateMax: 5.0m)
            ],
            globalQualityRanges:
                VideoSettingsGlobalRanges.CreateStandardQualityRanges(),
            globalContentRanges:
                VideoSettingsGlobalRanges.CreateStandardContentRanges());
    }
}
