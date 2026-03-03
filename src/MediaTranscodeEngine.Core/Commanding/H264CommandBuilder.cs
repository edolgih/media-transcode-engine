using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Commanding;

public sealed record H264RemuxCommandInput(
    string InputPath,
    string OutputPath,
    string TempOutputPath,
    IContainerPolicy ContainerPolicy,
    bool ReplaceInput = true);

public sealed record H264EncodeCommandInput(
    string InputPath,
    string OutputPath,
    string TempOutputPath,
    string NvencPreset,
    int Cq,
    string FpsToken,
    int Gop,
    IContainerPolicy ContainerPolicy,
    bool ApplyDownscale,
    int DownscaleTarget,
    string DownscaleAlgo,
    bool UseAq,
    int AqStrength,
    bool Denoise,
    bool FixTimestamps,
    bool CopyAudio,
    bool ReplaceInput = true);

public sealed class H264CommandBuilder
{
    public string BuildRemux(H264RemuxCommandInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.ContainerPolicy);

        var targetPath = input.ReplaceInput ? input.TempOutputPath : input.OutputPath;
        var replaceInputPart = input.ContainerPolicy.BuildPostOperation(
            inputPath: input.InputPath,
            tempOutputPath: input.TempOutputPath,
            outputPath: input.OutputPath,
            replaceInput: input.ReplaceInput);
        var muxPart = input.ContainerPolicy.MuxArguments;
        var parts = new[]
        {
            "ffmpeg",
            "-hide_banner",
            "-y",
            "-i",
            Quote(input.InputPath),
            "-map 0:v:0 -map 0:a:0? -sn",
            "-c copy",
            muxPart,
            Quote(targetPath),
            replaceInputPart
        };

        return string.Join(" ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    public string BuildEncode(H264EncodeCommandInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.ContainerPolicy);

        var targetPath = input.ReplaceInput ? input.TempOutputPath : input.OutputPath;
        var replaceInputPart = input.ContainerPolicy.BuildPostOperation(
            inputPath: input.InputPath,
            tempOutputPath: input.TempOutputPath,
            outputPath: input.OutputPath,
            replaceInput: input.ReplaceInput);
        var fflagsPart = input.FixTimestamps ? "-fflags +genpts+igndts" : string.Empty;
        var hwaccelPart = input.ApplyDownscale ? "-hwaccel cuda -hwaccel_output_format cuda" : string.Empty;
        var vfPart = BuildVfPart(input);
        var aqPart = input.UseAq
            ? $"-spatial_aq 1 -temporal_aq 1 -aq-strength {input.AqStrength} -rc-lookahead 32"
            : string.Empty;
        var pixFmtPart = input.ApplyDownscale ? string.Empty : "-pix_fmt yuv420p";
        var audioPart = input.CopyAudio ? "-c:a copy" : "-c:a aac -b:a 160k";
        var muxPart = input.ContainerPolicy.MuxArguments;

        var parts = new[]
        {
            "ffmpeg",
            "-hide_banner",
            "-y",
            fflagsPart,
            hwaccelPart,
            "-i",
            Quote(input.InputPath),
            "-map 0:v:0 -map 0:a:0? -sn",
            vfPart,
            $"-c:v h264_nvenc -preset {input.NvencPreset} -rc vbr_hq -cq {input.Cq} -b:v 0",
            aqPart,
            pixFmtPart,
            $"-r {input.FpsToken} -fps_mode:v cfr -g {input.Gop}",
            audioPart,
            muxPart,
            Quote(targetPath),
            replaceInputPart
        };

        return string.Join(" ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildVfPart(H264EncodeCommandInput input)
    {
        if (input.ApplyDownscale)
        {
            return $"-vf \"scale_cuda=-2:{input.DownscaleTarget}:interp_algo={input.DownscaleAlgo}:format=nv12\"";
        }

        if (input.Denoise)
        {
            return "-vf \"hqdn3d=1.2:1.2:6:6\"";
        }

        return string.Empty;
    }

    private static string Quote(string value) => $"\"{value}\"";
}
