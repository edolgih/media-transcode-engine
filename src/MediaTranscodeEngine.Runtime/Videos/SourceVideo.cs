namespace MediaTranscodeEngine.Runtime.Videos;

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
    public SourceVideo(
        string filePath,
        string container,
        string videoCodec,
        IReadOnlyList<string>? audioCodecs,
        int width,
        int height,
        double framesPerSecond,
        TimeSpan duration)
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

    private static IReadOnlyList<string> NormalizeAudioCodecs(IReadOnlyList<string>? audioCodecs)
    {
        if (audioCodecs is null || audioCodecs.Count == 0)
        {
            return Array.Empty<string>();
        }

        return audioCodecs
            .Select(codec => NormalizeToken(codec, nameof(audioCodecs)))
            .ToArray();
    }
}
