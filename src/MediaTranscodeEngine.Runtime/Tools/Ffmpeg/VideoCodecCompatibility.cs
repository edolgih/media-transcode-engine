using MediaTranscodeEngine.Runtime.Plans;

namespace MediaTranscodeEngine.Runtime.Tools.Ffmpeg;

/*
Этот helper выбирает аргументы codec-compatibility для выходного видео.
Сейчас он рассчитывает профиль и level прежде всего для H.264.
*/
/// <summary>
/// Resolves codec compatibility arguments, such as H.264 profile and level, from output dimensions and frame rate.
/// </summary>
internal static class VideoCodecCompatibility
{
    private static readonly H264LevelLimit[] H264Levels =
    [
        new("3.0", MaxFrameSizeInMacroblocks: 1620, MaxMacroblocksPerSecond: 40500),
        new("3.1", MaxFrameSizeInMacroblocks: 3600, MaxMacroblocksPerSecond: 108000),
        new("3.2", MaxFrameSizeInMacroblocks: 5120, MaxMacroblocksPerSecond: 216000),
        new("4.0", MaxFrameSizeInMacroblocks: 8192, MaxMacroblocksPerSecond: 245760),
        new("4.2", MaxFrameSizeInMacroblocks: 8704, MaxMacroblocksPerSecond: 522240),
        new("5.0", MaxFrameSizeInMacroblocks: 22080, MaxMacroblocksPerSecond: 589824),
        new("5.1", MaxFrameSizeInMacroblocks: 36864, MaxMacroblocksPerSecond: 983040),
        new("5.2", MaxFrameSizeInMacroblocks: 36864, MaxMacroblocksPerSecond: 2073600)
    ];

    internal static string ResolveArguments(string codec, VideoCompatibilityProfile? profile, int width, int height, double framesPerSecond)
    {
        return codec switch
        {
            "h264" => $"-profile:v {ResolveRequiredProfile(profile, codec)} -level:v {ResolveH264Level(width, height, framesPerSecond)}",
            "h265" => string.Empty,
            _ => string.Empty
        };
    }

    private static string ResolveRequiredProfile(VideoCompatibilityProfile? profile, string codec)
    {
        return profile switch
        {
            VideoCompatibilityProfile.H264Main => "main",
            VideoCompatibilityProfile.H264High => "high",
            null => throw new InvalidOperationException($"Codec '{codec}' requires a compatibility profile."),
            _ => throw new NotSupportedException($"Compatibility profile '{profile.Value}' is not supported for codec '{codec}'.")
        };
    }

    private static string ResolveH264Level(int width, int height, double framesPerSecond)
    {
        if (width <= 0 || height <= 0 || framesPerSecond <= 0)
        {
            return "4.1";
        }

        var macroblockWidth = (width + 15) / 16;
        var macroblockHeight = (height + 15) / 16;
        var frameSizeInMacroblocks = macroblockWidth * macroblockHeight;
        var macroblocksPerSecond = frameSizeInMacroblocks * framesPerSecond;

        foreach (var level in H264Levels)
        {
            if (frameSizeInMacroblocks <= level.MaxFrameSizeInMacroblocks &&
                macroblocksPerSecond <= level.MaxMacroblocksPerSecond)
            {
                return level.Name;
            }
        }

        return H264Levels[^1].Name;
    }

    /*
    Это локальное ограничение одного H.264 level.
    По нему helper подбирает минимальный совместимый level по размеру кадра и macroblocks per second.
    */
    /// <summary>
    /// Stores one H.264 level limit used to select a compatible output level.
    /// </summary>
    private sealed record H264LevelLimit(string Name, int MaxFrameSizeInMacroblocks, double MaxMacroblocksPerSecond);
}
