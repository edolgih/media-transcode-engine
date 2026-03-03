namespace MediaTranscodeEngine.Cli;

public sealed class RuntimeValues
{
    public string? ProfilesYamlPath { get; init; }
    public string? FfprobePath { get; init; }
    public string? FfmpegPath { get; init; }
    public int ProcessTimeoutMs { get; init; }
    public int SampleEncodeInactivityTimeoutMs { get; init; }
    public int SampleDurationSeconds { get; init; }
    public int SampleEncodeMaxRetries { get; init; }
    public string? AutoSampleNvencPreset { get; init; }
}
