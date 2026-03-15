namespace Transcode.Core.Videos;

/*
Это нормализованное описание входного видеофайла.
Сценарии и tool-адаптеры используют эти факты как общий источник данных о видео.
*/
/// <summary>
/// Represents a source video file with normalized facts that scenarios can use to make decisions.
/// </summary>
public sealed record SourceVideo
{
    /// <summary>
    /// Initializes a normalized source video description.
    /// </summary>
    /// <param name="filePath">Path to the source video file.</param>
    /// <param name="container">Normalized source container identifier.</param>
    /// <param name="videoCodec">Normalized source video codec identifier.</param>
    /// <param name="audioCodecs">Normalized source audio codec identifiers.</param>
    /// <param name="width">Source video width in pixels as reported by inspection.</param>
    /// <param name="height">Source video height in pixels as reported by inspection.</param>
    /// <param name="framesPerSecond">Source frame rate.</param>
    /// <param name="duration">Source duration.</param>
    /// <param name="bitrate">Optional normalized source bitrate in bits per second.</param>
    /// <param name="formatName">Optional raw format_name token from probe metadata.</param>
    /// <param name="rawFramesPerSecond">Optional frame rate parsed from r_frame_rate.</param>
    /// <param name="averageFramesPerSecond">Optional frame rate parsed from avg_frame_rate.</param>
    /// <param name="primaryAudioBitrate">Optional primary audio bitrate in bits per second.</param>
    /// <param name="primaryAudioSampleRate">Optional primary audio sample rate in hertz.</param>
    /// <param name="primaryAudioChannels">Optional primary audio channel count.</param>
    public SourceVideo(
        string filePath,
        string container,
        string videoCodec,
        IReadOnlyList<string> audioCodecs,
        int width,
        int height,
        double framesPerSecond,
        TimeSpan duration,
        long? bitrate = null,
        string? formatName = null,
        double? rawFramesPerSecond = null,
        double? averageFramesPerSecond = null,
        long? primaryAudioBitrate = null,
        int? primaryAudioSampleRate = null,
        int? primaryAudioChannels = null)
    {
        FilePath = NormalizeFilePath(filePath);
        Container = NormalizeToken(container, nameof(container));
        VideoCodec = NormalizeToken(videoCodec, nameof(videoCodec));
        AudioCodecs = NormalizeAudioCodecs(audioCodecs);
        Width = width >= 0
            ? width
            : throw new ArgumentOutOfRangeException(nameof(width), width, "Video width must not be negative.");
        Height = height >= 0
            ? height
            : throw new ArgumentOutOfRangeException(nameof(height), height, "Video height must not be negative.");
        FramesPerSecond = framesPerSecond > 0
            ? framesPerSecond
            : throw new ArgumentOutOfRangeException(nameof(framesPerSecond), framesPerSecond, "Frame rate must be greater than zero.");
        Duration = duration >= TimeSpan.Zero
            ? duration
            : throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration must not be negative.");
        Bitrate = bitrate is null || bitrate >= 0
            ? bitrate
            : throw new ArgumentOutOfRangeException(nameof(bitrate), bitrate, "Bitrate must not be negative.");
        FormatName = NormalizeOptionalText(formatName);
        RawFramesPerSecond = NormalizeOptionalPositiveDouble(rawFramesPerSecond, nameof(rawFramesPerSecond));
        AverageFramesPerSecond = NormalizeOptionalPositiveDouble(averageFramesPerSecond, nameof(averageFramesPerSecond));
        PrimaryAudioBitrate = primaryAudioBitrate is null || primaryAudioBitrate >= 0
            ? primaryAudioBitrate
            : throw new ArgumentOutOfRangeException(nameof(primaryAudioBitrate), primaryAudioBitrate, "Primary audio bitrate must not be negative.");
        PrimaryAudioSampleRate = NormalizeOptionalPositiveInt(primaryAudioSampleRate, nameof(primaryAudioSampleRate));
        PrimaryAudioChannels = NormalizeOptionalPositiveInt(primaryAudioChannels, nameof(primaryAudioChannels));
    }

    /// <summary>
    /// Gets the normalized full path to the source video file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the normalized source container identifier.
    /// </summary>
    public string Container { get; }

    /// <summary>
    /// Gets the normalized source video codec identifier.
    /// </summary>
    public string VideoCodec { get; }

    /// <summary>
    /// Gets the normalized list of source audio codec identifiers.
    /// </summary>
    public IReadOnlyList<string> AudioCodecs { get; }

    /// <summary>
    /// Gets the source video width in pixels as reported by inspection.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the source video height in pixels as reported by inspection.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the source frame rate.
    /// </summary>
    public double FramesPerSecond { get; }

    /// <summary>
    /// Gets the source duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the normalized source bitrate in bits per second when inspection could resolve it.
    /// </summary>
    public long? Bitrate { get; }

    /// <summary>
    /// Gets the raw format_name token from the probe metadata when available.
    /// </summary>
    public string? FormatName { get; }

    /// <summary>
    /// Gets the frame rate parsed from r_frame_rate when available.
    /// </summary>
    public double? RawFramesPerSecond { get; }

    /// <summary>
    /// Gets the frame rate parsed from avg_frame_rate when available.
    /// </summary>
    public double? AverageFramesPerSecond { get; }

    /// <summary>
    /// Gets the bitrate of the primary audio stream in bits per second when available.
    /// </summary>
    public long? PrimaryAudioBitrate { get; }

    /// <summary>
    /// Gets the sample rate of the primary audio stream in hertz when available.
    /// </summary>
    public int? PrimaryAudioSampleRate { get; }

    /// <summary>
    /// Gets the channel count of the primary audio stream when available.
    /// </summary>
    public int? PrimaryAudioChannels { get; }

    /// <summary>
    /// Gets the source file name without directory segments.
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the source file name without the file extension.
    /// </summary>
    public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>
    /// Gets the normalized source file extension.
    /// </summary>
    public string FileExtension => Path.GetExtension(FilePath).Trim().ToLowerInvariant();

    /// <summary>
    /// Gets a value indicating whether the source contains at least one audio stream.
    /// </summary>
    public bool HasAudio => AudioCodecs.Count > 0;

    /// <summary>
    /// Gets the first normalized audio codec, when present.
    /// </summary>
    public string? PrimaryAudioCodec => AudioCodecs.Count > 0
        ? AudioCodecs[0]
        : null;

    /// <summary>
    /// Gets a value indicating whether the source has a reliable raw-vs-average frame-rate mismatch signal.
    /// </summary>
    public bool HasFrameRateMismatch =>
        RawFramesPerSecond.HasValue &&
        AverageFramesPerSecond.HasValue &&
        Math.Abs(RawFramesPerSecond.Value - AverageFramesPerSecond.Value) > 0.0001;

    private static string NormalizeFilePath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Path.GetFullPath(filePath.Trim());
    }

    private static string NormalizeToken(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static double? NormalizeOptionalPositiveDouble(double? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value > 0
            ? value.Value
            : throw new ArgumentOutOfRangeException(paramName, value.Value, "Value must be greater than zero.");
    }

    private static int? NormalizeOptionalPositiveInt(int? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value > 0
            ? value.Value
            : throw new ArgumentOutOfRangeException(paramName, value.Value, "Value must be greater than zero.");
    }

    private static IReadOnlyList<string> NormalizeAudioCodecs(IReadOnlyList<string> audioCodecs)
    {
        ArgumentNullException.ThrowIfNull(audioCodecs);

        if (audioCodecs.Count == 0)
        {
            return Array.Empty<string>();
        }

        return audioCodecs
            .Select(codec => NormalizeToken(codec, nameof(audioCodecs)))
            .ToArray();
    }
}
