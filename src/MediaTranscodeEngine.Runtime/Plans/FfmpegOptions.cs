namespace MediaTranscodeEngine.Runtime.Plans;

/// <summary>
/// Carries optional ffmpeg-specific rendering hints for scenarios that need a narrower command shape than the shared defaults.
/// </summary>
public sealed class FfmpegOptions
{
    /// <summary>
    /// Initializes ffmpeg-specific rendering hints.
    /// </summary>
    public FfmpegOptions(
        bool optimizeForFastStart = false,
        bool mapPrimaryAudioOnly = false,
        bool? useHardwareDecode = null,
        bool? enableAdaptiveQuantization = null,
        int? aqStrength = null,
        int? rcLookahead = null,
        int? videoBitrateKbps = null,
        int? videoMaxrateKbps = null,
        int? videoBufferSizeKbps = null,
        int? videoCq = null,
        string? videoFilter = null,
        string? pixelFormat = null,
        int? audioBitrateKbps = null,
        int? audioSampleRate = null,
        int? audioChannels = null,
        string? audioFilter = null)
    {
        OptimizeForFastStart = optimizeForFastStart;
        MapPrimaryAudioOnly = mapPrimaryAudioOnly;
        UseHardwareDecode = useHardwareDecode;
        EnableAdaptiveQuantization = enableAdaptiveQuantization;
        AqStrength = NormalizeOptionalPositiveInt(aqStrength, nameof(aqStrength));
        RcLookahead = NormalizeOptionalPositiveInt(rcLookahead, nameof(rcLookahead));
        VideoBitrateKbps = NormalizeOptionalPositiveInt(videoBitrateKbps, nameof(videoBitrateKbps));
        VideoMaxrateKbps = NormalizeOptionalPositiveInt(videoMaxrateKbps, nameof(videoMaxrateKbps));
        VideoBufferSizeKbps = NormalizeOptionalPositiveInt(videoBufferSizeKbps, nameof(videoBufferSizeKbps));
        VideoCq = NormalizeOptionalPositiveInt(videoCq, nameof(videoCq));
        VideoFilter = NormalizeOptionalText(videoFilter);
        PixelFormat = NormalizeOptionalText(pixelFormat);
        AudioBitrateKbps = NormalizeOptionalPositiveInt(audioBitrateKbps, nameof(audioBitrateKbps));
        AudioSampleRate = NormalizeOptionalPositiveInt(audioSampleRate, nameof(audioSampleRate));
        AudioChannels = NormalizeOptionalPositiveInt(audioChannels, nameof(audioChannels));
        AudioFilter = NormalizeOptionalText(audioFilter);
    }

    /// <summary>
    /// Gets a value indicating whether the output container should be optimized for progressive playback.
    /// </summary>
    public bool OptimizeForFastStart { get; }

    /// <summary>
    /// Gets a value indicating whether only the primary audio stream should be mapped.
    /// </summary>
    public bool MapPrimaryAudioOnly { get; }

    /// <summary>
    /// Gets an explicit hardware-decode preference when the scenario wants to override the shared default.
    /// </summary>
    public bool? UseHardwareDecode { get; }

    /// <summary>
    /// Gets an explicit AQ preference when the scenario wants to override the shared default.
    /// </summary>
    public bool? EnableAdaptiveQuantization { get; }

    /// <summary>
    /// Gets the AQ strength override.
    /// </summary>
    public int? AqStrength { get; }

    /// <summary>
    /// Gets the lookahead window override.
    /// </summary>
    public int? RcLookahead { get; }

    /// <summary>
    /// Gets the target video bitrate in kilobits per second when VBR mode is requested.
    /// </summary>
    public int? VideoBitrateKbps { get; }

    /// <summary>
    /// Gets the target video maxrate in kilobits per second.
    /// </summary>
    public int? VideoMaxrateKbps { get; }

    /// <summary>
    /// Gets the target video buffer size in kilobits per second.
    /// </summary>
    public int? VideoBufferSizeKbps { get; }

    /// <summary>
    /// Gets the explicit CQ value when CQ-driven mode is requested.
    /// </summary>
    public int? VideoCq { get; }

    /// <summary>
    /// Gets the plain ffmpeg video filter expression when one is required.
    /// </summary>
    public string? VideoFilter { get; }

    /// <summary>
    /// Gets the explicit pixel format token when one is required.
    /// </summary>
    public string? PixelFormat { get; }

    /// <summary>
    /// Gets the target audio bitrate in kilobits per second when audio must be encoded.
    /// </summary>
    public int? AudioBitrateKbps { get; }

    /// <summary>
    /// Gets the explicit audio sample rate when one is required.
    /// </summary>
    public int? AudioSampleRate { get; }

    /// <summary>
    /// Gets the explicit audio channel count when one is required.
    /// </summary>
    public int? AudioChannels { get; }

    /// <summary>
    /// Gets the plain ffmpeg audio filter expression when one is required.
    /// </summary>
    public string? AudioFilter { get; }

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

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
