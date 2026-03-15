namespace Transcode.Core.Inspection;

/*
Это снимок сырых метаданных, полученных от probe-инструмента.
Он хранится до этапа нормализации в SourceVideo.
*/
/// <summary>
/// Holds raw metadata returned by a probe tool before it is normalized into a source video.
/// </summary>
public sealed record VideoProbeSnapshot(
    string? container,
    IReadOnlyList<VideoProbeStream> streams,
    TimeSpan? duration,
    long? formatBitrate = null,
    string? formatName = null);
