namespace MediaTranscodeEngine.Core.Engine;

public sealed record H264TranscodeRequest(
    string InputPath,
    int? Downscale = null,
    bool KeepFps = false,
    string DownscaleAlgo = "bicubic",
    int? Cq = null,
    string NvencPreset = "p5",
    bool UseAq = false,
    int AqStrength = 4,
    bool Denoise = false,
    bool FixTimestamps = false,
    bool OutputMkv = false);
