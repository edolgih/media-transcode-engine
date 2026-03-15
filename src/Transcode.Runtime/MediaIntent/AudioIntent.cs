namespace Transcode.Runtime.MediaIntent;

/// <summary>
/// Represents a normalized audio intent for a transcode plan.
/// </summary>
public abstract record AudioIntent;

/// <summary>
/// Represents an audio-copy intent with no encode-specific processing.
/// </summary>
public sealed record CopyAudioIntent : AudioIntent;

/// <summary>
/// Represents an explicit audio-encode intent and its repair mode.
/// </summary>
public record EncodeAudioIntent : AudioIntent;

/// <summary>
/// Represents an audio-encode intent that also requires timestamp repair.
/// </summary>
public record RepairAudioIntent : EncodeAudioIntent;

/// <summary>
/// Represents an audio-encode intent that also requires the sync-safe path.
/// </summary>
public sealed record SynchronizeAudioIntent : RepairAudioIntent;
