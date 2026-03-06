namespace MediaTranscodeEngine.Runtime.Inspection;

/// <summary>
/// Describes a single raw stream returned by a probe tool.
/// </summary>
public sealed record VideoProbeStream(
    string streamType,
    string codec,
    int? width = null,
    int? height = null,
    double? framesPerSecond = null);
