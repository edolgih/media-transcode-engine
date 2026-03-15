namespace Transcode.Core.Inspection;

/*
Это сырое описание одного потока из результата probe.
Оно хранит поля как можно ближе к исходным данным до дальнейшей нормализации.
*/
/// <summary>
/// Describes a single raw stream returned by a probe tool.
/// </summary>
public sealed record VideoProbeStream(
    string streamType,
    string codec,
    int? width = null,
    int? height = null,
    double? framesPerSecond = null,
    long? bitrate = null,
    double? rawFramesPerSecond = null,
    double? averageFramesPerSecond = null,
    int? sampleRate = null,
    int? channels = null);
