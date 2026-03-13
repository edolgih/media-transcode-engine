using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Tools.Ffmpeg;
using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Cli.Scenarios;

/*
Это scenario-local parser для tomkvgpu.
Он знает raw CLI option names и переводит их в runtime-request без переноса argv-логики в Runtime.
*/
/// <summary>
/// Parses ToMkvGpu CLI tokens into a runtime request while keeping raw option names in the CLI layer.
/// </summary>
internal static class ToMkvGpuCliRequestParser
{
    private const string DownscaleOptionName = "--downscale";
    private const string KeepSourceOptionName = "--keep-source";
    private const string OverlayBackgroundOptionName = "--overlay-bg";
    private const string MaxFramesPerSecondOptionName = "--max-fps";
    private const string SynchronizeAudioOptionName = "--sync-audio";
    private const string ContentProfileOptionName = "--content-profile";
    private const string QualityProfileOptionName = "--quality-profile";
    private const string AutoSampleModeOptionName = "--autosample-mode";
    private const string DownscaleAlgorithmOptionName = "--downscale-algo";
    private const string CqOptionName = "--cq";
    private const string MaxrateOptionName = "--maxrate";
    private const string BufsizeOptionName = "--bufsize";
    private const string NvencPresetOptionName = "--nvenc-preset";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out ToMkvGpuRequest request,
        out string? errorText)
    {
        request = default!;
        errorText = null;

        var overlayBackground = false;
        var synchronizeAudio = false;
        var keepSource = false;
        int? downscaleTargetHeight = null;
        int? maxFramesPerSecond = null;
        int? cq = null;
        decimal? maxrate = null;
        decimal? bufsize = null;
        string? contentProfile = null;
        string? qualityProfile = null;
        string? autoSampleMode = null;
        string? algorithm = null;
        string? nvencPreset = null;

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            if (string.Equals(token, KeepSourceOptionName, StringComparison.OrdinalIgnoreCase))
            {
                keepSource = true;
                continue;
            }

            if (string.Equals(token, OverlayBackgroundOptionName, StringComparison.OrdinalIgnoreCase))
            {
                overlayBackground = true;
                continue;
            }

            if (string.Equals(token, SynchronizeAudioOptionName, StringComparison.OrdinalIgnoreCase))
            {
                synchronizeAudio = true;
                continue;
            }

            if (string.Equals(token, DownscaleOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadInt(args, ref index, token, "--downscale must be an integer.", out downscaleTargetHeight, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, MaxFramesPerSecondOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadInt(args, ref index, token, "--max-fps must be an integer.", out maxFramesPerSecond, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, CqOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadInt(args, ref index, token, "--cq must be an integer.", out cq, out errorText))
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

            if (string.Equals(token, DownscaleAlgorithmOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out algorithm, out errorText))
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

            errorText = token.StartsWith("-", StringComparison.Ordinal)
                ? $"Unknown option: {token}"
                : $"Unexpected argument: {token}";
            return false;
        }

        if (downscaleTargetHeight is null && !string.IsNullOrWhiteSpace(algorithm))
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
                ? new DownscaleRequest(downscaleTargetHeight.Value, algorithm)
                : null;

            request = new ToMkvGpuRequest(
                overlayBackground: overlayBackground,
                synchronizeAudio: synchronizeAudio,
                keepSource: keepSource,
                videoSettings: videoSettingsRequest.HasValue ? videoSettingsRequest : null,
                downscale: downscaleRequest,
                nvencPreset: nvencPreset,
                maxFramesPerSecond: maxFramesPerSecond);
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
                "cq" => "--cq must be greater than zero.",
                "maxrate" => "--maxrate must be greater than zero.",
                "bufsize" => "--bufsize must be greater than zero.",
                "maxFramesPerSecond" => $"--max-fps must be one of: {CliValueFormatter.FormatList(ToMkvGpuRequest.SupportedMaxFramesPerSecond)}.",
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
