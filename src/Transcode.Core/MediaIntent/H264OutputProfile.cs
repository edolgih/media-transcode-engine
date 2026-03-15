namespace Transcode.Core.MediaIntent;

/*
Это перечень совместимых video-profile вариантов, которые может выбрать сценарий.
Сейчас он используется для совместимости H.264-выхода с целевыми устройствами.
*/
/// <summary>
/// Enumerates scenario-selected compatibility profiles for video encoders.
/// </summary>
public enum H264OutputProfile
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
