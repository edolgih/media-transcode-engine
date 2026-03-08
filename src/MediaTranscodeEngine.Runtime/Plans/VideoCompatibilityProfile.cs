namespace MediaTranscodeEngine.Runtime.Plans;

/// <summary>
/// Enumerates scenario-selected compatibility profiles for video encoders.
/// </summary>
public enum VideoCompatibilityProfile
{
    /// <summary>
    /// Uses the H.264 Main profile.
    /// </summary>
    H264Main = 1,

    /// <summary>
    /// Uses the H.264 High profile.
    /// </summary>
    H264High = 2
}
