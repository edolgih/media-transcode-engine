using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.Plans;

/*
Это общий план транскодирования, который сценарий передает инструменту.
В нем лежит общий intent без привязки к конкретному CLI-вызову или процессу ffmpeg.
*/
/// <summary>
/// Describes a tool-agnostic transcode intent produced by a scenario for a specific source video.
/// </summary>
public sealed record TranscodePlan
{
    /// <summary>
    /// Initializes a tool-agnostic transcode plan.
    /// </summary>
    /// <param name="targetContainer">Target container identifier.</param>
    /// <param name="targetVideoCodec">Target video codec when video encoding is required.</param>
    /// <param name="preferredBackend">Preferred encoding backend, when applicable.</param>
    /// <param name="videoCompatibilityProfile">Scenario-selected compatibility profile when the target codec uses one.</param>
    /// <param name="targetHeight">Target video height in pixels.</param>
    /// <param name="targetFramesPerSecond">Target frame rate.</param>
    /// <param name="useFrameInterpolation">Whether frame interpolation is requested.</param>
    /// <param name="videoSettings">Optional reusable video-settings intent.</param>
    /// <param name="copyVideo">Whether the source video stream should be copied without re-encoding.</param>
    /// <param name="copyAudio">Whether compatible audio streams should be copied.</param>
    /// <param name="fixTimestamps">Whether timestamp normalization is requested.</param>
    /// <param name="keepSource">Whether the source file should be preserved.</param>
    /// <param name="encoderPreset">Optional encoder preset preference.</param>
    /// <param name="outputPath">Optional explicit output path.</param>
    /// <param name="applyOverlayBackground">Whether the plan requests background overlay during video encoding.</param>
    /// <param name="synchronizeAudio">Whether the plan requests the sync-safe audio path.</param>
    /// <param name="ffmpegOptions">Optional narrow ffmpeg-specific rendering hints.</param>
    public TranscodePlan(
        string targetContainer,
        string? targetVideoCodec,
        string? preferredBackend,
        VideoCompatibilityProfile? videoCompatibilityProfile,
        int? targetHeight,
        double? targetFramesPerSecond,
        bool useFrameInterpolation,
        VideoSettingsRequest? videoSettings,
        bool copyVideo,
        bool copyAudio,
        bool fixTimestamps,
        bool keepSource,
        string? encoderPreset = null,
        string? outputPath = null,
        bool applyOverlayBackground = false,
        bool synchronizeAudio = false,
        FfmpegOptions? ffmpegOptions = null)
    {
        TargetContainer = NormalizeRequiredToken(targetContainer, nameof(targetContainer));
        TargetHeight = NormalizeOptionalPositiveInt(targetHeight, nameof(targetHeight));
        TargetFramesPerSecond = NormalizeOptionalPositiveDouble(targetFramesPerSecond, nameof(targetFramesPerSecond));
        VideoSettings = NormalizeOptionalVideoSettings(videoSettings, TargetHeight);
        CopyVideo = copyVideo;
        CopyAudio = copyAudio;
        FixTimestamps = fixTimestamps;
        KeepSource = keepSource;
        UseFrameInterpolation = useFrameInterpolation;
        PreferredBackend = NormalizeOptionalToken(preferredBackend);
        VideoCompatibilityProfile = videoCompatibilityProfile;
        EncoderPreset = NormalizeOptionalToken(encoderPreset);
        OutputPath = NormalizeOptionalPath(outputPath);
        ApplyOverlayBackground = applyOverlayBackground;
        SynchronizeAudio = synchronizeAudio;
        FfmpegOptions = ffmpegOptions;

        if (CopyVideo)
        {
            if (VideoCompatibilityProfile is not null)
            {
                throw new ArgumentException("Video copy plan cannot request compatibility profile.", nameof(videoCompatibilityProfile));
            }

            if (TargetHeight.HasValue)
            {
                throw new ArgumentException("Video copy plan cannot request target height.", nameof(targetHeight));
            }

            if (TargetFramesPerSecond.HasValue)
            {
                throw new ArgumentException("Video copy plan cannot request target frame rate.", nameof(targetFramesPerSecond));
            }

            if (UseFrameInterpolation)
            {
                throw new ArgumentException("Video copy plan cannot request frame interpolation.", nameof(useFrameInterpolation));
            }

            TargetVideoCodec = null;
        }
        else
        {
            TargetVideoCodec = NormalizeRequiredToken(targetVideoCodec, nameof(targetVideoCodec));
            if (TargetVideoCodec.Equals("h264", StringComparison.OrdinalIgnoreCase) &&
                VideoCompatibilityProfile is null)
            {
                throw new ArgumentException("H.264 encode plan requires compatibility profile.", nameof(videoCompatibilityProfile));
            }
        }

        if (UseFrameInterpolation && !TargetFramesPerSecond.HasValue)
        {
            throw new ArgumentException("Frame interpolation requires a target frame rate.", nameof(targetFramesPerSecond));
        }
    }

    /// <summary>
    /// Gets the normalized target container identifier.
    /// </summary>
    public string TargetContainer { get; }

    /// <summary>
    /// Gets the normalized target video codec identifier when re-encoding is required.
    /// </summary>
    public string? TargetVideoCodec { get; }

    /// <summary>
    /// Gets the normalized preferred backend identifier when a backend is relevant.
    /// </summary>
    public string? PreferredBackend { get; }

    /// <summary>
    /// Gets the normalized compatibility profile selected by the scenario when the target codec uses one.
    /// </summary>
    public VideoCompatibilityProfile? VideoCompatibilityProfile { get; }

    /// <summary>
    /// Gets the target output height in pixels when resizing is required.
    /// </summary>
    public int? TargetHeight { get; }

    /// <summary>
    /// Gets the target frame rate when the scenario requests a frame rate change.
    /// </summary>
    public double? TargetFramesPerSecond { get; }

    /// <summary>
    /// Gets a value indicating whether frame interpolation is required.
    /// </summary>
    public bool UseFrameInterpolation { get; }

    /// <summary>
    /// Gets reusable video-settings directives when the plan needs profile-driven output settings.
    /// </summary>
    public VideoSettingsRequest? VideoSettings { get; }

    /// <summary>
    /// Gets a value indicating whether the source video stream should be copied.
    /// </summary>
    public bool CopyVideo { get; }

    /// <summary>
    /// Gets a value indicating whether compatible audio streams should be copied.
    /// </summary>
    public bool CopyAudio { get; }

    /// <summary>
    /// Gets a value indicating whether timestamp normalization is required.
    /// </summary>
    public bool FixTimestamps { get; }

    /// <summary>
    /// Gets a value indicating whether the source file should be preserved.
    /// </summary>
    public bool KeepSource { get; }

    /// <summary>
    /// Gets the preferred encoder preset when the scenario wants to influence encode speed/quality tradeoffs.
    /// </summary>
    public string? EncoderPreset { get; }

    /// <summary>
    /// Gets an explicit output path when the scenario chooses one.
    /// </summary>
    public string? OutputPath { get; }

    /// <summary>
    /// Gets a value indicating whether background overlay should be applied during video encoding.
    /// </summary>
    public bool ApplyOverlayBackground { get; }

    /// <summary>
    /// Gets a value indicating whether the scenario requests the sync-safe audio path.
    /// </summary>
    public bool SynchronizeAudio { get; }

    /// <summary>
    /// Gets optional ffmpeg-specific rendering hints.
    /// </summary>
    public FfmpegOptions? FfmpegOptions { get; }

    /// <summary>
    /// Gets a value indicating whether the plan requires video encoding.
    /// </summary>
    public bool RequiresVideoEncode => !CopyVideo;

    /// <summary>
    /// Gets a value indicating whether the plan requires audio encoding.
    /// </summary>
    public bool RequiresAudioEncode => !CopyAudio;

    /// <summary>
    /// Gets a value indicating whether the plan changes the output height.
    /// </summary>
    public bool ChangesResolution => TargetHeight.HasValue;

    /// <summary>
    /// Gets a value indicating whether the plan changes the frame rate.
    /// </summary>
    public bool ChangesFrameRate => TargetFramesPerSecond.HasValue;

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetFullPath(path.Trim());
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

    private static VideoSettingsRequest? NormalizeOptionalVideoSettings(VideoSettingsRequest? videoSettings, int? targetHeight)
    {
        if (videoSettings is null)
        {
            return null;
        }

        if (targetHeight.HasValue && videoSettings.TargetHeight != targetHeight)
        {
            throw new ArgumentException("Video settings target must match target height when both are provided.", nameof(videoSettings));
        }

        if (!targetHeight.HasValue && videoSettings.TargetHeight.HasValue)
        {
            throw new ArgumentException("Video settings target requires target height in the transcode plan.", nameof(videoSettings));
        }

        return videoSettings.HasValue ? videoSettings : null;
    }
}
