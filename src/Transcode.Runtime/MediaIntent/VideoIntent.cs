using Transcode.Runtime.VideoSettings;

namespace Transcode.Runtime.MediaIntent;

/// <summary>
/// Represents a normalized video intent for a transcode plan.
/// </summary>
public abstract record VideoIntent;

/// <summary>
/// Represents a video-copy intent with no encode-specific settings.
/// </summary>
public sealed record CopyVideoIntent : VideoIntent;

/// <summary>
/// Represents an explicit video-encode intent and its encode-specific settings.
/// </summary>
public sealed record EncodeVideoIntent(
    string TargetVideoCodec,
    string? PreferredBackend = null,
    H264OutputProfile? CompatibilityProfile = null,
    double? TargetFramesPerSecond = null,
    bool UseFrameInterpolation = false,
    VideoSettingsRequest? VideoSettings = null,
    DownscaleRequest? Downscale = null,
    string? EncoderPreset = null) : VideoIntent;
