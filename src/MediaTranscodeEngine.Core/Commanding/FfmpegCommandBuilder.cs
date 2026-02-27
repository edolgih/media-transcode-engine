using System.Globalization;

namespace MediaTranscodeEngine.Core.Commanding;

public sealed record FfmpegCommandInput(
    string InputPath,
    string OutputPath,
    string PostOperation,
    bool NeedVideoEncode,
    bool NeedAudioEncode,
    bool NeedContainer,
    bool ForceSyncAudio,
    bool ApplyDownscale,
    int DownscaleTarget,
    bool OverlayBg,
    int? SourceWidth,
    int? SourceHeight,
    int Cq,
    double Maxrate,
    double Bufsize,
    string DownscaleAlgo,
    double? SourceFps = null,
    string NvencPreset = "p6");

public sealed class FfmpegCommandBuilder
{
    public string Build(FfmpegCommandInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var sanitize = BuildSanitize(input);
        var hwaccelPart = input.ApplyDownscale ? "-hwaccel cuda -hwaccel_output_format cuda" : string.Empty;
        var videoPart = BuildVideoPart(input);
        var audioPart = BuildAudioPart(input);

        var parts = new[]
        {
            "ffmpeg",
            "-hide_banner",
            sanitize,
            hwaccelPart,
            "-i",
            Quote(input.InputPath),
            videoPart,
            audioPart,
            "-sn",
            "-max_muxing_queue_size 4096",
            Quote(input.OutputPath),
            input.PostOperation
        };

        return string.Join(" ", parts.Where(static p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string BuildVideoPart(FfmpegCommandInput input)
    {
        var levelPart = ResolveLevelPart(input);

        if (!input.NeedVideoEncode)
        {
            return "-map 0:v:0 -c:v copy";
        }

        const string aqPart = "-spatial_aq 1 -temporal_aq 1 -rc-lookahead 32";
        const string fpsPart = "-fps_mode:v cfr";

        if (input.ApplyDownscale && !input.OverlayBg)
        {
            return string.Join(" ", new[]
            {
                "-map 0:v:0",
                fpsPart,
                $"-vf \"scale_cuda=-2:{input.DownscaleTarget}:interp_algo={input.DownscaleAlgo}:format=nv12\"",
                $"-c:v h264_nvenc -preset {input.NvencPreset} -rc vbr_hq -cq {input.Cq} -b:v 0 -maxrate {ToRateToken(input.Maxrate)} -bufsize {ToRateToken(input.Bufsize)} {aqPart}",
                $"-profile:v high {levelPart} -g 48"
            });
        }

        if (input.OverlayBg)
        {
            var (outW, outH) = ResolveOverlayDimensions(input);
            var filterComplex = BuildOverlayFilterComplex(input, outW, outH);
            var maxrate = input.ApplyDownscale ? ToRateToken(input.Maxrate) : "4M";
            var bufsize = input.ApplyDownscale ? ToRateToken(input.Bufsize) : "8M";

            return string.Join(" ", new[]
            {
                $"-filter_complex \"{filterComplex}\"",
                "-map \"[v]\"",
                fpsPart,
                $"-c:v h264_nvenc -preset {input.NvencPreset} -rc vbr_hq -cq {input.Cq} -b:v 0 -maxrate {maxrate} -bufsize {bufsize} {aqPart}",
                $"-pix_fmt yuv420p -profile:v high {levelPart} -g 48"
            });
        }

        return string.Join(" ", new[]
        {
            "-map 0:v:0",
            fpsPart,
            $"-c:v h264_nvenc -preset {input.NvencPreset} -rc vbr_hq -cq {input.Cq} -b:v 0 -maxrate 4M -bufsize 8M {aqPart}",
            $"-pix_fmt yuv420p -profile:v high {levelPart} -g 48"
        });
    }

    private static string BuildAudioPart(FfmpegCommandInput input)
    {
        if (!input.NeedAudioEncode)
        {
            return "-map 0:a? -c:a copy";
        }

        return "-map 0:a? -c:a aac -ar 48000 -ac 2 -b:a 192k -af \"aresample=async=1:first_pts=0\"";
    }

    private static string BuildSanitize(FfmpegCommandInput input)
    {
        if (input.NeedVideoEncode)
        {
            return "-fflags +genpts+igndts -avoid_negative_ts make_zero";
        }

        if (input.NeedAudioEncode || input.NeedContainer)
        {
            if (input.NeedContainer || input.ForceSyncAudio)
            {
                return "-fflags +genpts -avoid_negative_ts make_zero";
            }

            return "-avoid_negative_ts make_zero";
        }

        return string.Empty;
    }

    private static string BuildOverlayFilterComplex(FfmpegCommandInput input, int outW, int outH)
    {
        if (input.ApplyDownscale)
        {
            return string.Concat(
                $"[0:v]split=2[bg0][fg0];",
                $"[bg0]scale_cuda={outW}:-2:interp_algo={input.DownscaleAlgo}:format=nv12,hwdownload,format=nv12,crop={outW}:{outH},hwupload_cuda[bg];",
                $"[fg0]scale_cuda=-2:{outH}:interp_algo={input.DownscaleAlgo}:format=nv12[fg];",
                "[bg][fg]overlay_cuda=(W-w)/2:0[v]");
        }

        return string.Concat(
            $"[0:v]scale={outW}:-1,crop={outW}:{outH}[bg];",
            $"[0:v]scale=-1:{outH}[fg];",
            "[bg][fg]overlay=(W-w)/2:0[v]");
    }

    private static (int Width, int Height) ResolveOverlayDimensions(FfmpegCommandInput input)
    {
        var outW = input.SourceWidth ?? 0;
        var outH = input.SourceHeight ?? 0;

        if (outW <= 0 || outH <= 0)
        {
            outW = 1920;
            outH = 1080;
        }

        if (outW < outH)
        {
            (outW, outH) = (outH, outW);
        }

        if (input.ApplyDownscale && outH > 0)
        {
            var ratio = (double)input.DownscaleTarget / outH;
            outW = (int)Math.Round(outW * ratio);
            outH = input.DownscaleTarget;
        }

        if (outW % 2 != 0)
        {
            outW++;
        }

        if (outH % 2 != 0)
        {
            outH++;
        }

        return (outW, outH);
    }

    private static string ToRateToken(double value)
    {
        var rounded = Math.Round(value, 1);
        var integer = Math.Round(rounded);
        if (Math.Abs(rounded - integer) < 0.000001)
        {
            return $"{(int)integer}M";
        }

        return $"{rounded.ToString("0.0", CultureInfo.InvariantCulture)}M";
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string ResolveLevelPart(FfmpegCommandInput input)
    {
        if (input.SourceFps is not > 30.0)
        {
            return "-level:v 4.1";
        }

        var (outputWidth, outputHeight) = ResolveOutputDimensionsForLevel(input);
        var nearFhdClass = outputHeight > 720 || outputWidth >= 1920;
        if (nearFhdClass)
        {
            return "-level:v 4.2";
        }

        return "-level:v 4.1";
    }

    private static (int Width, int Height) ResolveOutputDimensionsForLevel(FfmpegCommandInput input)
    {
        if (input.ApplyDownscale)
        {
            if (input.OverlayBg)
            {
                return ResolveOverlayDimensions(input);
            }

            return (0, input.DownscaleTarget);
        }

        if (input.OverlayBg)
        {
            return ResolveOverlayDimensions(input);
        }

        return (input.SourceWidth ?? 0, input.SourceHeight ?? 0);
    }
}
