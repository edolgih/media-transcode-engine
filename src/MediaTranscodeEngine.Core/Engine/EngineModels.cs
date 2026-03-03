namespace MediaTranscodeEngine.Core.Engine;

public sealed record ProbeFormat(
    double? DurationSeconds = null,
    double? BitrateBps = null,
    string? FormatName = null);

public sealed record ProbeStream(
    string CodecType,
    string CodecName,
    int? Width = null,
    int? Height = null,
    double? BitrateBps = null,
    string? RFrameRate = null,
    string? AvgFrameRate = null);

public sealed record ProbeResult(
    ProbeFormat? Format,
    IReadOnlyList<ProbeStream> Streams);
