using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;
using MediaTranscodeEngine.Runtime.Tools.Ffmpeg;
using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Cli.Scenarios;

/*
Это scenario-local parser для toh264gpu.
Он знает raw CLI option names и переводит их в runtime-request без argv-знания в Runtime.
*/
/// <summary>
/// Parses ToH264Gpu CLI tokens into a runtime request while keeping raw option names in the CLI layer.
/// </summary>
internal static class ToH264GpuCliRequestParser
{
    private const string KeepSourceOptionName = "--keep-source";
    private const string DownscaleOptionName = "--downscale";
    private const string KeepFpsOptionName = "--keep-fps";
    private const string ContentProfileOptionName = "--content-profile";
    private const string QualityProfileOptionName = "--quality-profile";
    private const string AutoSampleModeOptionName = "--autosample-mode";
    private const string DownscaleAlgoOptionName = "--downscale-algo";
    private const string CqOptionName = "--cq";
    private const string MaxrateOptionName = "--maxrate";
    private const string BufsizeOptionName = "--bufsize";
    private const string NvencPresetOptionName = "--nvenc-preset";
    private const string DenoiseOptionName = "--denoise";
    private const string SynchronizeAudioOptionName = "--sync-audio";
    private const string MkvOptionName = "--mkv";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out ToH264GpuRequest request,
        out string? errorText)
    {
        request = default!;
        errorText = null;

        var keepSource = false;
        int? downscaleTargetHeight = null;
        var keepFramesPerSecond = false;
        string? downscaleAlgorithm = null;
        int? cq = null;
        decimal? maxrate = null;
        decimal? bufsize = null;
        string? contentProfile = null;
        string? qualityProfile = null;
        string? autoSampleMode = null;
        string? nvencPreset = null;
        var denoise = false;
        var synchronizeAudio = false;
        var outputMkv = false;

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            if (string.Equals(token, KeepSourceOptionName, StringComparison.OrdinalIgnoreCase))
            {
                keepSource = true;
                continue;
            }

            if (string.Equals(token, DownscaleOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadInt(args, ref index, token, $"--downscale must be one of: {CliValueFormatter.FormatList(DownscaleRequest.SupportedTargetHeights)}.", out downscaleTargetHeight, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, KeepFpsOptionName, StringComparison.OrdinalIgnoreCase))
            {
                keepFramesPerSecond = true;
                continue;
            }

            if (string.Equals(token, ContentProfileOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out contentProfile, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, QualityProfileOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out qualityProfile, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, AutoSampleModeOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out autoSampleMode, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, DownscaleAlgoOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out downscaleAlgorithm, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, CqOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadInt(args, ref index, token, "--cq must be an integer from 1 to 51.", out cq, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, MaxrateOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadDecimal(args, ref index, token, "--maxrate must be a number.", out maxrate, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, BufsizeOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadDecimal(args, ref index, token, "--bufsize must be a number.", out bufsize, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, NvencPresetOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out nvencPreset, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, DenoiseOptionName, StringComparison.OrdinalIgnoreCase))
            {
                denoise = true;
                continue;
            }

            if (string.Equals(token, SynchronizeAudioOptionName, StringComparison.OrdinalIgnoreCase))
            {
                synchronizeAudio = true;
                continue;
            }

            if (string.Equals(token, MkvOptionName, StringComparison.OrdinalIgnoreCase))
            {
                outputMkv = true;
                continue;
            }

            errorText = $"Unexpected argument: {token}";
            return false;
        }

        if (!downscaleTargetHeight.HasValue && !string.IsNullOrWhiteSpace(downscaleAlgorithm))
        {
            errorText = "--downscale-algo requires --downscale.";
            return false;
        }

        try
        {
            var videoSettingsRequest = new VideoSettingsRequest(
                contentProfile: contentProfile,
                qualityProfile: qualityProfile,
                autoSampleMode: autoSampleMode,
                cq: cq,
                maxrate: maxrate,
                bufsize: bufsize);
            var downscaleRequest = downscaleTargetHeight.HasValue
                ? new DownscaleRequest(downscaleTargetHeight.Value, downscaleAlgorithm)
                : null;

            request = new ToH264GpuRequest(
                keepSource: keepSource,
                downscale: downscaleRequest,
                keepFramesPerSecond: keepFramesPerSecond,
                videoSettings: videoSettingsRequest.HasValue ? videoSettingsRequest : null,
                nvencPreset: nvencPreset,
                denoise: denoise,
                synchronizeAudio: synchronizeAudio,
                outputMkv: outputMkv);
            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            errorText = exception.ParamName switch
            {
                "targetHeight" => exception.ActualValue is int actualHeight && actualHeight > 0
                    ? $"--downscale must be one of: {CliValueFormatter.FormatList(DownscaleRequest.SupportedTargetHeights)}."
                    : "--downscale must be greater than zero.",
                "algorithm" => $"--downscale-algo must be one of: {CliValueFormatter.FormatList(DownscaleRequest.SupportedAlgorithms)}.",
                "cq" => "--cq must be an integer from 1 to 51.",
                "maxrate" => "--maxrate must be greater than zero.",
                "bufsize" => "--bufsize must be greater than zero.",
                "contentProfile" => $"--content-profile must be one of: {CliValueFormatter.FormatList(VideoSettingsRequest.SupportedContentProfiles)}.",
                "qualityProfile" => $"--quality-profile must be one of: {CliValueFormatter.FormatList(VideoSettingsRequest.SupportedQualityProfiles)}.",
                "autoSampleMode" => $"--autosample-mode must be one of: {CliValueFormatter.FormatList(VideoSettingsRequest.SupportedAutoSampleModes)}.",
                "nvencPreset" => $"--nvenc-preset must be one of: {CliValueFormatter.FormatList(NvencPresetOptions.SupportedPresets)}.",
                _ => exception.Message
            };
            return false;
        }
    }
}
