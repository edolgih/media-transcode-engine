namespace Transcode.Core.VideoSettings.Profiles;

internal static class VideoSettingsGlobalRanges
{
    public static IReadOnlyList<VideoSettingsQualityRange> CreateStandardQualityRanges() =>
    [
        new VideoSettingsQualityRange("high", MinInclusive: 25.0m, MaxInclusive: 40.0m),
        new VideoSettingsQualityRange("default", MinExclusive: 40.0m, MaxInclusive: 50.0m),
        new VideoSettingsQualityRange("low", MinExclusive: 50.0m)
    ];

    public static IReadOnlyList<VideoSettingsRange> CreateStandardContentRanges() =>
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
    ];
}
