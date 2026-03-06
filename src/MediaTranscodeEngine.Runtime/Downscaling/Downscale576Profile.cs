namespace MediaTranscodeEngine.Runtime.Downscaling;

internal static class Downscale576Profile
{
    public static DownscaleProfile Create()
    {
        return new DownscaleProfile(
            targetHeight: 576,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new DownscaleRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
            autoSampling: new DownscaleAutoSampling(
                EnabledByDefault: true,
                ModeDefault: "accurate",
                MaxIterations: 8,
                HybridAccurateIterations: 2,
                LongMinDuration: TimeSpan.FromMinutes(8),
                LongWindowCount: 3,
                LongWindowDuration: TimeSpan.FromSeconds(120),
                MediumMinDuration: TimeSpan.FromMinutes(3),
                MediumWindowCount: 2,
                MediumWindowDuration: TimeSpan.FromSeconds(120),
                ShortWindowCount: 1,
                ShortWindowDuration: TimeSpan.FromSeconds(90)),
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "hd_720",
                    MinHeight: 650,
                    MaxHeight: 899,
                    Ranges:
                    [
                        new DownscaleRange("anime", "high", MinInclusive: 18.0m, MaxInclusive: 32.0m),
                        new DownscaleRange("anime", "default", MinInclusive: 32.0m, MaxInclusive: 46.0m),
                        new DownscaleRange("anime", "low", MinInclusive: 46.0m, MaxInclusive: 65.0m),
                        new DownscaleRange("mult", "high", MinInclusive: 15.0m, MaxInclusive: 28.0m),
                        new DownscaleRange("mult", "default", MinInclusive: 28.0m, MaxInclusive: 42.0m),
                        new DownscaleRange("mult", "low", MinInclusive: 42.0m, MaxInclusive: 60.0m),
                        new DownscaleRange("film", "high", MinInclusive: 10.0m, MaxInclusive: 25.0m),
                        new DownscaleRange("film", "default", MinInclusive: 25.0m, MaxInclusive: 40.0m),
                        new DownscaleRange("film", "low", MinInclusive: 40.0m, MaxInclusive: 55.0m)
                    ]),
                new SourceHeightBucket(
                    "fhd_1080",
                    MinHeight: 1000,
                    MaxHeight: 1300,
                    Ranges:
                    [
                        new DownscaleRange("anime", "high", MinInclusive: 30.0m, MaxInclusive: 45.0m),
                        new DownscaleRange("anime", "default", MinInclusive: 45.0m, MaxInclusive: 60.0m),
                        new DownscaleRange("anime", "low", MinInclusive: 60.0m, MaxInclusive: 80.0m),
                        new DownscaleRange("mult", "high", MinInclusive: 28.0m, MaxInclusive: 42.0m),
                        new DownscaleRange("mult", "default", MinInclusive: 42.0m, MaxInclusive: 57.0m),
                        new DownscaleRange("mult", "low", MinInclusive: 57.0m, MaxInclusive: 77.0m),
                        new DownscaleRange("film", "high", MinInclusive: 20.0m, MaxInclusive: 35.0m),
                        new DownscaleRange("film", "default", MinInclusive: 35.0m, MaxInclusive: 50.0m),
                        new DownscaleRange("film", "low", MinInclusive: 50.0m, MaxInclusive: 70.0m)
                    ])
            ],
            defaults:
            [
                new DownscaleDefaults("anime", "high", Cq: 22, Maxrate: 3.3m, Bufsize: 6.5m, Algorithm: "bilinear", CqMin: 19, CqMax: 24, MaxrateMin: 2.4m, MaxrateMax: 4.2m),
                new DownscaleDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: "bilinear", CqMin: 20, CqMax: 26, MaxrateMin: 2.0m, MaxrateMax: 3.0m),
                new DownscaleDefaults("anime", "low", Cq: 29, Maxrate: 2.1m, Bufsize: 4.1m, Algorithm: "bilinear", CqMin: 24, CqMax: 35, MaxrateMin: 1.0m, MaxrateMax: 3.2m),
                new DownscaleDefaults("mult", "high", Cq: 24, Maxrate: 2.7m, Bufsize: 5.3m, Algorithm: "bilinear", CqMin: 21, CqMax: 26, MaxrateMin: 2.4m, MaxrateMax: 3.2m),
                new DownscaleDefaults("mult", "default", Cq: 26, Maxrate: 2.4m, Bufsize: 4.8m, Algorithm: "bilinear", CqMin: 23, CqMax: 29, MaxrateMin: 2.0m, MaxrateMax: 2.8m),
                new DownscaleDefaults("mult", "low", Cq: 29, Maxrate: 1.7m, Bufsize: 3.5m, Algorithm: "bilinear", CqMin: 26, CqMax: 31, MaxrateMin: 1.6m, MaxrateMax: 2.0m),
                new DownscaleDefaults("film", "high", Cq: 24, Maxrate: 3.7m, Bufsize: 7.4m, Algorithm: "bilinear", CqMin: 16, CqMax: 33, MaxrateMin: 2.0m, MaxrateMax: 8.0m),
                new DownscaleDefaults("film", "default", Cq: 26, Maxrate: 3.4m, Bufsize: 6.9m, Algorithm: "bilinear", CqMin: 18, CqMax: 35, MaxrateMin: 1.6m, MaxrateMax: 8.0m),
                new DownscaleDefaults("film", "low", Cq: 30, Maxrate: 2.2m, Bufsize: 4.5m, Algorithm: "bilinear", CqMin: 20, CqMax: 38, MaxrateMin: 1.2m, MaxrateMax: 4.0m)
            ]);
    }
}
