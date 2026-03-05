using System.Globalization;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Parsing;

internal enum CliOptionValueKind
{
    Flag,
    String,
    Int,
    Double
}

internal readonly record struct CliParsedValue(
    string? StringValue = null,
    int? IntValue = null,
    double? DoubleValue = null);

internal sealed record CliOptionDefinition(
    string Name,
    CliOptionValueKind ValueKind,
    bool IsRepeatable,
    string HelpText,
    Action<CliMutableParseState, CliParsedValue> ApplyValue,
    string? InvalidValueError = null,
    string? Usage = null,
    IReadOnlyList<string>? AffectedTemplateFields = null);

internal sealed class CliMutableParseState
{
    public List<string> Inputs { get; } = [];

    public HashSet<string> ExplicitTemplateFields { get; } = new(StringComparer.Ordinal);

    public RawTranscodeRequest RequestTemplate { get; set; } =
        new(InputPath: "__input__");

    public void MarkExplicitFields(IReadOnlyList<string>? fieldNames)
    {
        if (fieldNames is null)
        {
            return;
        }

        foreach (var fieldName in fieldNames)
        {
            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                ExplicitTemplateFields.Add(fieldName);
            }
        }
    }
}

internal static class CliContracts
{
    public static readonly IReadOnlyDictionary<string, CliOptionDefinition> OptionsByName = CreateOptions();

    public static bool TryGetOption(string token, out CliOptionDefinition option)
    {
        return OptionsByName.TryGetValue(token, out option!);
    }

    private static IReadOnlyDictionary<string, CliOptionDefinition> CreateOptions()
    {
        return new Dictionary<string, CliOptionDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["--help"] = new CliOptionDefinition(
                Name: "--help",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Show help.",
                ApplyValue: static (_, _) => { },
                Usage: "--help, -h"),
            ["-h"] = new CliOptionDefinition(
                Name: "-h",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Show help.",
                ApplyValue: static (_, _) => { }),
            ["--input"] = new CliOptionDefinition(
                Name: "--input",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: true,
                HelpText: "Input file path.",
                ApplyValue: static (state, value) =>
                {
                    if (!string.IsNullOrWhiteSpace(value.StringValue))
                    {
                        state.Inputs.Add(value.StringValue);
                    }
                },
                Usage: "--input <path>"),
            ["--keep-source"] = new CliOptionDefinition(
                Name: "--keep-source",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Keep input source file; write output to a new file.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { KeepSource = true },
                AffectedTemplateFields: [nameof(RawTranscodeRequest.KeepSource)]),
            ["--scenario"] = new CliOptionDefinition(
                Name: "--scenario",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Scenario preset name.",
                ApplyValue: static (state, value) =>
                {
                    state.RequestTemplate = state.RequestTemplate with { Scenario = value.StringValue };
                },
                Usage: "--scenario <name>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.Scenario)]),
            ["--container"] = new CliOptionDefinition(
                Name: "--container",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Target container (mkv|mp4).",
                ApplyValue: static (state, value) =>
                {
                    var container = value.StringValue ?? string.Empty;
                    var preferH264 = !container.Equals(RequestContracts.General.MkvContainer, StringComparison.OrdinalIgnoreCase);
                    var hasExplicitTargetVideoCodec = state.ExplicitTemplateFields.Contains(nameof(RawTranscodeRequest.TargetVideoCodec));
                    var targetVideoCodec = state.RequestTemplate.TargetVideoCodec;
                    if (preferH264 && !hasExplicitTargetVideoCodec)
                    {
                        targetVideoCodec = RequestContracts.General.H264VideoCodec;
                    }

                    state.RequestTemplate = state.RequestTemplate with
                    {
                        TargetContainer = container,
                        TargetVideoCodec = targetVideoCodec,
                        PreferH264 = state.RequestTemplate.PreferH264 || preferH264
                    };
                },
                Usage: "--container <mkv|mp4>",
                AffectedTemplateFields:
                [
                    nameof(RawTranscodeRequest.TargetContainer),
                    nameof(RawTranscodeRequest.TargetVideoCodec),
                    nameof(RawTranscodeRequest.PreferH264)
                ]),
            ["--encoder-backend"] = new CliOptionDefinition(
                Name: "--encoder-backend",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Encoder backend (gpu|cpu).",
                ApplyValue: static (state, value) =>
                {
                    state.RequestTemplate = state.RequestTemplate with { EncoderBackend = value.StringValue ?? string.Empty };
                },
                Usage: "--encoder-backend <gpu|cpu>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.EncoderBackend)]),
            ["--compute"] = new CliOptionDefinition(
                Name: "--compute",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Alias of --encoder-backend.",
                ApplyValue: static (state, value) =>
                {
                    state.RequestTemplate = state.RequestTemplate with { EncoderBackend = value.StringValue ?? string.Empty };
                },
                Usage: "--compute <gpu|cpu>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.EncoderBackend)]),
            ["--preset"] = new CliOptionDefinition(
                Name: "--preset",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Video preset.",
                ApplyValue: static (state, value) =>
                {
                    state.RequestTemplate = state.RequestTemplate with { VideoPreset = value.StringValue ?? string.Empty };
                },
                Usage: "--preset <value>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.VideoPreset)]),
            ["--video-codec"] = new CliOptionDefinition(
                Name: "--video-codec",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Target video codec (copy|h264|h265).",
                ApplyValue: static (state, value) =>
                {
                    var targetVideoCodec = value.StringValue ?? string.Empty;
                    var preferH264 = targetVideoCodec.Equals(RequestContracts.General.H264VideoCodec, StringComparison.OrdinalIgnoreCase);
                    state.RequestTemplate = state.RequestTemplate with
                    {
                        TargetVideoCodec = targetVideoCodec,
                        PreferH264 = preferH264
                    };
                },
                Usage: "--video-codec <copy|h264|h265>",
                AffectedTemplateFields:
                [
                    nameof(RawTranscodeRequest.TargetVideoCodec),
                    nameof(RawTranscodeRequest.PreferH264)
                ]),
            ["--nvenc-preset"] = new CliOptionDefinition(
                Name: "--nvenc-preset",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Alias of --preset.",
                ApplyValue: static (state, value) =>
                {
                    state.RequestTemplate = state.RequestTemplate with { VideoPreset = value.StringValue ?? string.Empty };
                },
                Usage: "--nvenc-preset <value>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.VideoPreset)]),

            ["--info"] = new CliOptionDefinition(
                Name: "--info",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Info mode.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { Info = true },
                AffectedTemplateFields: [nameof(RawTranscodeRequest.Info)]),
            ["--overlay-bg"] = new CliOptionDefinition(
                Name: "--overlay-bg",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Enable overlay background mode.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { OverlayBg = true },
                AffectedTemplateFields: [nameof(RawTranscodeRequest.OverlayBg)]),
            ["--downscale"] = new CliOptionDefinition(
                Name: "--downscale",
                ValueKind: CliOptionValueKind.Int,
                IsRepeatable: false,
                HelpText: "Downscale target.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { Downscale = value.IntValue },
                InvalidValueError: "--downscale must be an integer.",
                Usage: "--downscale <int>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.Downscale)]),
            ["--downscale-algo"] = new CliOptionDefinition(
                Name: "--downscale-algo",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Downscale algorithm.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { DownscaleAlgo = value.StringValue },
                Usage: "--downscale-algo <value>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.DownscaleAlgo)]),
            ["--content-profile"] = new CliOptionDefinition(
                Name: "--content-profile",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Content profile.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { ContentProfile = value.StringValue ?? state.RequestTemplate.ContentProfile },
                Usage: "--content-profile <value>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.ContentProfile)]),
            ["--quality-profile"] = new CliOptionDefinition(
                Name: "--quality-profile",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Quality profile.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { QualityProfile = value.StringValue ?? state.RequestTemplate.QualityProfile },
                Usage: "--quality-profile <value>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.QualityProfile)]),
            ["--no-auto-sample"] = new CliOptionDefinition(
                Name: "--no-auto-sample",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Disable auto sample.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { NoAutoSample = true },
                AffectedTemplateFields: [nameof(RawTranscodeRequest.NoAutoSample)]),
            ["--auto-sample-mode"] = new CliOptionDefinition(
                Name: "--auto-sample-mode",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Auto sample mode.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { AutoSampleMode = value.StringValue ?? state.RequestTemplate.AutoSampleMode },
                Usage: "--auto-sample-mode <value>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.AutoSampleMode)]),
            ["--sync-audio"] = new CliOptionDefinition(
                Name: "--sync-audio",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Force audio sync path.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { SyncAudio = true },
                AffectedTemplateFields: [nameof(RawTranscodeRequest.SyncAudio)]),
            ["--force-video-encode"] = new CliOptionDefinition(
                Name: "--force-video-encode",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Force video encode.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { ForceVideoEncode = true },
                AffectedTemplateFields: [nameof(RawTranscodeRequest.ForceVideoEncode)]),
            ["--cq"] = new CliOptionDefinition(
                Name: "--cq",
                ValueKind: CliOptionValueKind.Int,
                IsRepeatable: false,
                HelpText: "CQ override.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { Cq = value.IntValue },
                InvalidValueError: "--cq must be an integer.",
                Usage: "--cq <int>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.Cq)]),
            ["--maxrate"] = new CliOptionDefinition(
                Name: "--maxrate",
                ValueKind: CliOptionValueKind.Double,
                IsRepeatable: false,
                HelpText: "Maxrate override.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { Maxrate = value.DoubleValue },
                InvalidValueError: "--maxrate must be a number.",
                Usage: "--maxrate <number>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.Maxrate)]),
            ["--bufsize"] = new CliOptionDefinition(
                Name: "--bufsize",
                ValueKind: CliOptionValueKind.Double,
                IsRepeatable: false,
                HelpText: "Bufsize override.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { Bufsize = value.DoubleValue },
                InvalidValueError: "--bufsize must be a number.",
                Usage: "--bufsize <number>",
                AffectedTemplateFields: [nameof(RawTranscodeRequest.Bufsize)]),

            ["--keep-fps"] = new CliOptionDefinition(
                Name: "--keep-fps",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Keep source FPS.",
                ApplyValue: static (state, _) =>
                {
                    var hasExplicitTargetVideoCodec = state.ExplicitTemplateFields.Contains(nameof(RawTranscodeRequest.TargetVideoCodec));
                    var targetVideoCodec = hasExplicitTargetVideoCodec
                        ? state.RequestTemplate.TargetVideoCodec
                        : RequestContracts.General.H264VideoCodec;
                    state.RequestTemplate = state.RequestTemplate with
                    {
                        KeepFps = true,
                        TargetVideoCodec = targetVideoCodec,
                        PreferH264 = true
                    };
                },
                AffectedTemplateFields:
                [
                    nameof(RawTranscodeRequest.KeepFps),
                    nameof(RawTranscodeRequest.TargetVideoCodec),
                    nameof(RawTranscodeRequest.PreferH264)
                ]),
            ["--use-aq"] = new CliOptionDefinition(
                Name: "--use-aq",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Enable AQ.",
                ApplyValue: static (state, _) =>
                {
                    var hasExplicitTargetVideoCodec = state.ExplicitTemplateFields.Contains(nameof(RawTranscodeRequest.TargetVideoCodec));
                    var targetVideoCodec = hasExplicitTargetVideoCodec
                        ? state.RequestTemplate.TargetVideoCodec
                        : RequestContracts.General.H264VideoCodec;
                    state.RequestTemplate = state.RequestTemplate with
                    {
                        UseAq = true,
                        TargetVideoCodec = targetVideoCodec,
                        PreferH264 = true
                    };
                },
                AffectedTemplateFields:
                [
                    nameof(RawTranscodeRequest.UseAq),
                    nameof(RawTranscodeRequest.TargetVideoCodec),
                    nameof(RawTranscodeRequest.PreferH264)
                ]),
            ["--aq-strength"] = new CliOptionDefinition(
                Name: "--aq-strength",
                ValueKind: CliOptionValueKind.Int,
                IsRepeatable: false,
                HelpText: "AQ strength.",
                ApplyValue: static (state, value) =>
                {
                    var hasExplicitTargetVideoCodec = state.ExplicitTemplateFields.Contains(nameof(RawTranscodeRequest.TargetVideoCodec));
                    var targetVideoCodec = hasExplicitTargetVideoCodec
                        ? state.RequestTemplate.TargetVideoCodec
                        : RequestContracts.General.H264VideoCodec;
                    state.RequestTemplate = state.RequestTemplate with
                    {
                        AqStrength = value.IntValue ?? RequestContracts.General.DefaultAqStrength,
                        TargetVideoCodec = targetVideoCodec,
                        PreferH264 = true
                    };
                },
                InvalidValueError: "--aq-strength must be an integer.",
                Usage: "--aq-strength <int>",
                AffectedTemplateFields:
                [
                    nameof(RawTranscodeRequest.AqStrength),
                    nameof(RawTranscodeRequest.TargetVideoCodec),
                    nameof(RawTranscodeRequest.PreferH264)
                ]),
            ["--denoise"] = new CliOptionDefinition(
                Name: "--denoise",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Enable denoise.",
                ApplyValue: static (state, _) =>
                {
                    var hasExplicitTargetVideoCodec = state.ExplicitTemplateFields.Contains(nameof(RawTranscodeRequest.TargetVideoCodec));
                    var targetVideoCodec = hasExplicitTargetVideoCodec
                        ? state.RequestTemplate.TargetVideoCodec
                        : RequestContracts.General.H264VideoCodec;
                    state.RequestTemplate = state.RequestTemplate with
                    {
                        Denoise = true,
                        TargetVideoCodec = targetVideoCodec,
                        PreferH264 = true
                    };
                },
                AffectedTemplateFields:
                [
                    nameof(RawTranscodeRequest.Denoise),
                    nameof(RawTranscodeRequest.TargetVideoCodec),
                    nameof(RawTranscodeRequest.PreferH264)
                ]),
            ["--fix-timestamps"] = new CliOptionDefinition(
                Name: "--fix-timestamps",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Force timestamp fixes.",
                ApplyValue: static (state, _) =>
                {
                    var hasExplicitTargetVideoCodec = state.ExplicitTemplateFields.Contains(nameof(RawTranscodeRequest.TargetVideoCodec));
                    var targetVideoCodec = hasExplicitTargetVideoCodec
                        ? state.RequestTemplate.TargetVideoCodec
                        : RequestContracts.General.H264VideoCodec;
                    state.RequestTemplate = state.RequestTemplate with
                    {
                        FixTimestamps = true,
                        TargetVideoCodec = targetVideoCodec,
                        PreferH264 = true
                    };
                },
                AffectedTemplateFields:
                [
                    nameof(RawTranscodeRequest.FixTimestamps),
                    nameof(RawTranscodeRequest.TargetVideoCodec),
                    nameof(RawTranscodeRequest.PreferH264)
                ]),
            ["--output-mkv"] = new CliOptionDefinition(
                Name: "--output-mkv",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Alias for h264 flow with mkv output container.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with
                {
                    TargetContainer = RequestContracts.General.MkvContainer,
                    TargetVideoCodec = RequestContracts.General.H264VideoCodec,
                    PreferH264 = true
                },
                AffectedTemplateFields:
                [
                    nameof(RawTranscodeRequest.TargetContainer),
                    nameof(RawTranscodeRequest.TargetVideoCodec),
                    nameof(RawTranscodeRequest.PreferH264)
                ])
        };
    }

    public static bool TryParseValue(
        CliOptionDefinition option,
        string token,
        out CliParsedValue parsedValue,
        out string? errorText)
    {
        parsedValue = default;
        errorText = null;

        switch (option.ValueKind)
        {
            case CliOptionValueKind.Flag:
                return true;
            case CliOptionValueKind.String:
                parsedValue = new CliParsedValue(StringValue: token);
                return true;
            case CliOptionValueKind.Int:
                if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    errorText = option.InvalidValueError ?? $"{option.Name} must be an integer.";
                    return false;
                }

                parsedValue = new CliParsedValue(IntValue: intValue);
                return true;
            case CliOptionValueKind.Double:
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    errorText = option.InvalidValueError ?? $"{option.Name} must be a number.";
                    return false;
                }

                parsedValue = new CliParsedValue(DoubleValue: doubleValue);
                return true;
            default:
                errorText = $"Unsupported value kind: {option.ValueKind}";
                return false;
        }
    }
}
