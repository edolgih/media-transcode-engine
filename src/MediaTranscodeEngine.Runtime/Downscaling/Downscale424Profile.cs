namespace MediaTranscodeEngine.Runtime.Downscaling;

/*
Это фабрика профиля downscale для целевой высоты 424.
Она описывает данные профиля как код, без логики выполнения.
*/
/// <summary>
/// Builds the typed downscale profile for target height 424.
/// </summary>
internal static class Downscale424Profile
{
    public static DownscaleProfile Create()
    {
        return new DownscaleProfile(
            targetHeight: 424,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new DownscaleRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
            autoSampling: new DownscaleAutoSampling(
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
                    "sd_480",
                    MinHeight: 425,
                    MaxHeight: 649,
                    Ranges:
                    [
                        new DownscaleRange("anime", "high", MinInclusive: 8.0m, MaxInclusive: 20.0m),
                        new DownscaleRange("anime", "default", MinInclusive: 20.0m, MaxInclusive: 32.0m),
                        new DownscaleRange("anime", "low", MinInclusive: 32.0m, MaxInclusive: 48.0m),
                        new DownscaleRange("mult", "high", MinInclusive: 6.0m, MaxInclusive: 18.0m),
                        new DownscaleRange("mult", "default", MinInclusive: 18.0m, MaxInclusive: 30.0m),
                        new DownscaleRange("mult", "low", MinInclusive: 30.0m, MaxInclusive: 44.0m),
                        new DownscaleRange("film", "high", MinInclusive: 5.0m, MaxInclusive: 15.0m),
                        new DownscaleRange("film", "default", MinInclusive: 15.0m, MaxInclusive: 28.0m),
                        new DownscaleRange("film", "low", MinInclusive: 28.0m, MaxInclusive: 42.0m)
                    ]),
                new SourceHeightBucket(
                    "hd_720",
                    MinHeight: 650,
                    MaxHeight: 899,
                    Ranges:
                    [
                        new DownscaleRange("anime", "high", MinInclusive: 18.0m, MaxInclusive: 30.0m),
                        new DownscaleRange("anime", "default", MinInclusive: 30.0m, MaxInclusive: 42.0m),
                        new DownscaleRange("anime", "low", MinInclusive: 42.0m, MaxInclusive: 58.0m),
                        new DownscaleRange("mult", "high", MinInclusive: 16.0m, MaxInclusive: 27.0m),
                        new DownscaleRange("mult", "default", MinInclusive: 27.0m, MaxInclusive: 39.0m),
                        new DownscaleRange("mult", "low", MinInclusive: 39.0m, MaxInclusive: 54.0m),
                        new DownscaleRange("film", "high", MinInclusive: 12.0m, MaxInclusive: 24.0m),
                        new DownscaleRange("film", "default", MinInclusive: 24.0m, MaxInclusive: 36.0m),
                        new DownscaleRange("film", "low", MinInclusive: 36.0m, MaxInclusive: 50.0m)
                    ],
                    BoundsOverrides:
                    [
                        new DownscaleBoundsOverride("mult", "high", CqMin: 18, MaxrateMax: 2.4m),
                        new DownscaleBoundsOverride("mult", "default", CqMin: 22, MaxrateMax: 2.0m),
                        new DownscaleBoundsOverride("mult", "low", CqMin: 26, MaxrateMax: 1.4m)
                    ]),
                new SourceHeightBucket(
                    "fhd_1080",
                    MinHeight: 900,
                    MaxHeight: 1300,
                    Ranges:
                    [
                        new DownscaleRange("anime", "high", MinInclusive: 28.0m, MaxInclusive: 42.0m),
                        new DownscaleRange("anime", "default", MinInclusive: 42.0m, MaxInclusive: 56.0m),
                        new DownscaleRange("anime", "low", MinInclusive: 56.0m, MaxInclusive: 72.0m),
                        new DownscaleRange("mult", "high", MinInclusive: 25.0m, MaxInclusive: 38.0m),
                        new DownscaleRange("mult", "default", MinInclusive: 38.0m, MaxInclusive: 52.0m),
                        new DownscaleRange("mult", "low", MinInclusive: 52.0m, MaxInclusive: 68.0m),
                        new DownscaleRange("film", "high", MinInclusive: 18.0m, MaxInclusive: 32.0m),
                        new DownscaleRange("film", "default", MinInclusive: 32.0m, MaxInclusive: 46.0m),
                        new DownscaleRange("film", "low", MinInclusive: 46.0m, MaxInclusive: 60.0m)
                    ],
                    BoundsOverrides:
                    [
                        new DownscaleBoundsOverride("mult", "low", CqMax: 35, MaxrateMin: 0.9m)
                    ])
            ],
            defaults:
            [
                new DownscaleDefaults("anime", "high", Cq: 24, Maxrate: 2.1m, Bufsize: 4.2m, Algorithm: "bilinear", CqMin: 21, CqMax: 26, MaxrateMin: 1.6m, MaxrateMax: 2.8m),
                new DownscaleDefaults("anime", "default", Cq: 25, Maxrate: 1.6m, Bufsize: 3.2m, Algorithm: "bilinear", CqMin: 22, CqMax: 28, MaxrateMin: 1.3m, MaxrateMax: 2.0m),
                new DownscaleDefaults("anime", "low", Cq: 31, Maxrate: 1.4m, Bufsize: 2.8m, Algorithm: "bilinear", CqMin: 26, CqMax: 36, MaxrateMin: 0.8m, MaxrateMax: 2.0m),
                new DownscaleDefaults("mult", "high", Cq: 26, Maxrate: 1.7m, Bufsize: 3.4m, Algorithm: "bilinear", CqMin: 23, CqMax: 28, MaxrateMin: 1.5m, MaxrateMax: 2.1m),
                new DownscaleDefaults("mult", "default", Cq: 28, Maxrate: 1.5m, Bufsize: 3.0m, Algorithm: "bilinear", CqMin: 25, CqMax: 31, MaxrateMin: 1.3m, MaxrateMax: 1.9m),
                new DownscaleDefaults("mult", "low", Cq: 31, Maxrate: 1.1m, Bufsize: 2.2m, Algorithm: "bilinear", CqMin: 28, CqMax: 33, MaxrateMin: 1.0m, MaxrateMax: 1.4m),
                new DownscaleDefaults("film", "high", Cq: 26, Maxrate: 2.3m, Bufsize: 4.6m, Algorithm: "bilinear", CqMin: 18, CqMax: 35, MaxrateMin: 1.4m, MaxrateMax: 5.0m),
                new DownscaleDefaults("film", "default", Cq: 28, Maxrate: 2.1m, Bufsize: 4.2m, Algorithm: "bilinear", CqMin: 20, CqMax: 37, MaxrateMin: 1.1m, MaxrateMax: 5.0m),
                new DownscaleDefaults("film", "low", Cq: 32, Maxrate: 1.5m, Bufsize: 3.0m, Algorithm: "bilinear", CqMin: 22, CqMax: 40, MaxrateMin: 0.8m, MaxrateMax: 2.6m)
            ],
            globalQualityRanges:
            [
                new DownscaleQualityRange("high", MinInclusive: 26.0m, MaxInclusive: 40.0m),
                new DownscaleQualityRange("default", MinExclusive: 40.0m, MaxInclusive: 52.0m),
                new DownscaleQualityRange("low", MinExclusive: 52.0m)
            ],
            globalContentRanges:
            [
                new DownscaleRange("anime", "high", MinInclusive: 26.0m, MaxInclusive: 40.0m),
                new DownscaleRange("anime", "default", MinExclusive: 40.0m, MaxInclusive: 52.0m),
                new DownscaleRange("anime", "low", MinExclusive: 52.0m, MaxInclusive: 75.0m),
                new DownscaleRange("mult", "high", MinInclusive: 24.0m, MaxInclusive: 38.0m),
                new DownscaleRange("mult", "default", MinExclusive: 38.0m, MaxInclusive: 50.0m),
                new DownscaleRange("mult", "low", MinExclusive: 50.0m, MaxInclusive: 72.0m),
                new DownscaleRange("film", "high", MinInclusive: 18.0m, MaxInclusive: 32.0m),
                new DownscaleRange("film", "default", MinExclusive: 32.0m, MaxInclusive: 44.0m),
                new DownscaleRange("film", "low", MinExclusive: 44.0m, MaxInclusive: 66.0m)
            ]);
    }
}
