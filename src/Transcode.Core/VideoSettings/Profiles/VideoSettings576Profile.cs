using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;

namespace Transcode.Core.VideoSettings.Profiles;

/*
Это фабрика video-settings профиля для целевой высоты 576.
Она задаёт профильные defaults и autosample-матрицу для этого target height.
*/
/// <summary>
/// Builds the typed video-settings profile for target height 576.
/// </summary>
internal static class VideoSettings576Profile
{
    public static VideoSettingsProfile Create()
    {
        return new VideoSettingsProfile(
            targetHeight: 576,
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
                    "hd_720",
                    MinHeight: 650,
                    MaxHeight: 899,
                    Ranges:
                    [
                        new VideoSettingsRange("anime", "high", MinInclusive: 18.0m, MaxInclusive: 32.0m),
                        new VideoSettingsRange("anime", "default", MinInclusive: 32.0m, MaxInclusive: 46.0m),
                        new VideoSettingsRange("anime", "low", MinInclusive: 46.0m, MaxInclusive: 65.0m),
                        new VideoSettingsRange("mult", "high", MinInclusive: 15.0m, MaxInclusive: 28.0m),
                        new VideoSettingsRange("mult", "default", MinInclusive: 28.0m, MaxInclusive: 42.0m),
                        new VideoSettingsRange("mult", "low", MinInclusive: 42.0m, MaxInclusive: 60.0m),
                        new VideoSettingsRange("film", "high", MinInclusive: 10.0m, MaxInclusive: 25.0m),
                        new VideoSettingsRange("film", "default", MinInclusive: 25.0m, MaxInclusive: 40.0m),
                        new VideoSettingsRange("film", "low", MinInclusive: 40.0m, MaxInclusive: 55.0m)
                    ],
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "high", CqMin: 15, MaxrateMax: 4.4m),
                        new VideoSettingsBoundsOverride("mult", "default", CqMin: 19, MaxrateMax: 3.6m),
                        new VideoSettingsBoundsOverride("mult", "low", CqMin: 24, MaxrateMax: 2.4m)
                    ]),
                new SourceHeightBucket(
                    "fhd_1080",
                    MinHeight: 1000,
                    MaxHeight: 1300,
                    Ranges:
                    [
                        new VideoSettingsRange("anime", "high", MinInclusive: 30.0m, MaxInclusive: 45.0m),
                        new VideoSettingsRange("anime", "default", MinInclusive: 45.0m, MaxInclusive: 60.0m),
                        new VideoSettingsRange("anime", "low", MinInclusive: 60.0m, MaxInclusive: 80.0m),
                        new VideoSettingsRange("mult", "high", MinInclusive: 28.0m, MaxInclusive: 42.0m),
                        new VideoSettingsRange("mult", "default", MinInclusive: 42.0m, MaxInclusive: 57.0m),
                        new VideoSettingsRange("mult", "low", MinInclusive: 57.0m, MaxInclusive: 77.0m),
                        new VideoSettingsRange("film", "high", MinInclusive: 20.0m, MaxInclusive: 35.0m),
                        new VideoSettingsRange("film", "default", MinInclusive: 35.0m, MaxInclusive: 50.0m),
                        new VideoSettingsRange("film", "low", MinInclusive: 50.0m, MaxInclusive: 70.0m)
                    ],
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "low", CqMax: 33, MaxrateMin: 1.4m)
                    ])
            ],
            defaults:
            [
                new VideoSettingsDefaults("anime", "high", Cq: 22, Maxrate: 3.3m, Bufsize: 6.5m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 19, CqMax: 24, MaxrateMin: 2.4m, MaxrateMax: 4.2m),
                new VideoSettingsDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 3.0m),
                new VideoSettingsDefaults("anime", "low", Cq: 29, Maxrate: 2.1m, Bufsize: 4.1m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 24, CqMax: 35, MaxrateMin: 1.0m, MaxrateMax: 3.2m),
                new VideoSettingsDefaults("mult", "high", Cq: 24, Maxrate: 2.7m, Bufsize: 5.3m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 21, CqMax: 26, MaxrateMin: 2.4m, MaxrateMax: 3.2m),
                new VideoSettingsDefaults("mult", "default", Cq: 26, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 23, CqMax: 29, MaxrateMin: 2.0m, MaxrateMax: 2.8m),
                new VideoSettingsDefaults("mult", "low", Cq: 29, Maxrate: 1.7m, Bufsize: 3.5m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 26, CqMax: 31, MaxrateMin: 1.6m, MaxrateMax: 2.0m),
                new VideoSettingsDefaults("film", "high", Cq: 24, Maxrate: 3.7m, Bufsize: 7.4m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 16, CqMax: 33, MaxrateMin: 2.0m, MaxrateMax: 8.0m),
                new VideoSettingsDefaults("film", "default", Cq: 26, Maxrate: 3.4m, Bufsize: 6.9m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 18, CqMax: 35, MaxrateMin: 1.6m, MaxrateMax: 8.0m),
                new VideoSettingsDefaults("film", "low", Cq: 30, Maxrate: 2.2m, Bufsize: 4.5m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 38, MaxrateMin: 1.2m, MaxrateMax: 4.0m)
            ],
            globalQualityRanges:
                VideoSettingsGlobalRanges.CreateStandardQualityRanges(),
            globalContentRanges:
                VideoSettingsGlobalRanges.CreateStandardContentRanges());
    }
}
