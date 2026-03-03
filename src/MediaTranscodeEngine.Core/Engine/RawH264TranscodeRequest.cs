namespace MediaTranscodeEngine.Core.Engine;

public sealed record RawH264TranscodeRequest(
    string InputPath,
    int? Downscale = null,
    bool KeepFps = false,
    string DownscaleAlgo = RequestContracts.H264.DefaultDownscaleAlgorithm,
    int? Cq = null,
    string NvencPreset = RequestContracts.H264.DefaultNvencPreset,
    bool UseAq = false,
    int AqStrength = RequestContracts.H264.DefaultAqStrength,
    bool Denoise = false,
    bool FixTimestamps = false,
    bool OutputMkv = false)
{
    public H264TranscodeRequest ToDomain()
    {
        return H264TranscodeRequest.Create(
            InputPath: InputPath,
            Downscale: Downscale,
            KeepFps: KeepFps,
            DownscaleAlgo: DownscaleAlgo,
            Cq: Cq,
            NvencPreset: NvencPreset,
            UseAq: UseAq,
            AqStrength: AqStrength,
            Denoise: Denoise,
            FixTimestamps: FixTimestamps,
            OutputMkv: OutputMkv);
    }
}
