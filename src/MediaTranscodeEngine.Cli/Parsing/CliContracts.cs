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
    IReadOnlySet<string> AppliesToScenarios,
    Action<CliMutableParseState, CliParsedValue> ApplyValue,
    string? InvalidValueError = null,
    string? Usage = null);

internal sealed record CliScenarioDefinition(
    string Name,
    string Description,
    IReadOnlySet<string> SupportedOptions);

internal sealed class CliMutableParseState
{
    public string ScenarioName { get; set; } = CliContracts.ToMkvGpuScenario;

    public List<string> Inputs { get; } = [];

    public RawTranscodeRequest ToMkvTemplate { get; set; } =
        new(InputPath: "__input__");

    public RawH264TranscodeRequest ToH264Template { get; set; } =
        new(InputPath: "__input__");
}

internal static class CliContracts
{
    public const string ToMkvGpuScenario = "tomkvgpu";
    public const string ToH264GpuScenario = "toh264gpu";

    private static readonly IReadOnlySet<string> CommonScenarios = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ToMkvGpuScenario,
        ToH264GpuScenario
    };

    public static readonly IReadOnlyDictionary<string, CliScenarioDefinition> ScenariosByName = CreateScenarios();
    public static readonly IReadOnlyDictionary<string, CliOptionDefinition> OptionsByName = CreateOptions();

    public static bool TryGetOption(string token, out CliOptionDefinition option)
    {
        return OptionsByName.TryGetValue(token, out option!);
    }

    private static IReadOnlyDictionary<string, CliScenarioDefinition> CreateScenarios()
    {
        var toMkvOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--help",
            "-h",
            "--input",
            "--scenario",
            "--info",
            "--overlay-bg",
            "--downscale",
            "--downscale-algo",
            "--content-profile",
            "--quality-profile",
            "--no-auto-sample",
            "--auto-sample-mode",
            "--sync-audio",
            "--force-video-encode",
            "--cq",
            "--maxrate",
            "--bufsize",
            "--nvenc-preset"
        };

        var toH264Options = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--help",
            "-h",
            "--input",
            "--scenario",
            "--downscale",
            "--downscale-algo",
            "--keep-fps",
            "--cq",
            "--nvenc-preset",
            "--use-aq",
            "--aq-strength",
            "--denoise",
            "--fix-timestamps",
            "--output-mkv"
        };

        return new Dictionary<string, CliScenarioDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [ToMkvGpuScenario] = new CliScenarioDefinition(
                Name: ToMkvGpuScenario,
                Description: "Legacy ToMkv GPU pipeline.",
                SupportedOptions: toMkvOptions),
            [ToH264GpuScenario] = new CliScenarioDefinition(
                Name: ToH264GpuScenario,
                Description: "H264 optimization pipeline.",
                SupportedOptions: toH264Options)
        };
    }

    private static IReadOnlyDictionary<string, CliOptionDefinition> CreateOptions()
    {
        var options = new Dictionary<string, CliOptionDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["--help"] = new CliOptionDefinition(
                Name: "--help",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Show help.",
                AppliesToScenarios: CommonScenarios,
                ApplyValue: static (_, _) => { },
                Usage: "--help, -h"),
            ["-h"] = new CliOptionDefinition(
                Name: "-h",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Show help.",
                AppliesToScenarios: CommonScenarios,
                ApplyValue: static (_, _) => { }),
            ["--input"] = new CliOptionDefinition(
                Name: "--input",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: true,
                HelpText: "Input file path.",
                AppliesToScenarios: CommonScenarios,
                ApplyValue: static (state, value) =>
                {
                    if (!string.IsNullOrWhiteSpace(value.StringValue))
                    {
                        state.Inputs.Add(value.StringValue);
                    }
                },
                Usage: "--input <path>"),
            ["--scenario"] = new CliOptionDefinition(
                Name: "--scenario",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Scenario name.",
                AppliesToScenarios: CommonScenarios,
                ApplyValue: static (state, value) =>
                {
                    if (!string.IsNullOrWhiteSpace(value.StringValue))
                    {
                        state.ScenarioName = value.StringValue.Trim();
                    }
                },
                Usage: "--scenario <name>"),

            ["--info"] = new CliOptionDefinition(
                Name: "--info",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Info mode.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToMkvGpuScenario },
                ApplyValue: static (state, _) => state.ToMkvTemplate = state.ToMkvTemplate with { Info = true }),
            ["--overlay-bg"] = new CliOptionDefinition(
                Name: "--overlay-bg",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Enable overlay background mode.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToMkvGpuScenario },
                ApplyValue: static (state, _) => state.ToMkvTemplate = state.ToMkvTemplate with { OverlayBg = true }),
            ["--downscale"] = new CliOptionDefinition(
                Name: "--downscale",
                ValueKind: CliOptionValueKind.Int,
                IsRepeatable: false,
                HelpText: "Downscale target.",
                AppliesToScenarios: CommonScenarios,
                ApplyValue: static (state, value) =>
                {
                    state.ToMkvTemplate = state.ToMkvTemplate with { Downscale = value.IntValue };
                    state.ToH264Template = state.ToH264Template with { Downscale = value.IntValue };
                },
                InvalidValueError: "--downscale must be an integer.",
                Usage: "--downscale <int>"),
            ["--downscale-algo"] = new CliOptionDefinition(
                Name: "--downscale-algo",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Downscale algorithm.",
                AppliesToScenarios: CommonScenarios,
                ApplyValue: static (state, value) =>
                {
                    state.ToMkvTemplate = state.ToMkvTemplate with { DownscaleAlgoOverride = value.StringValue };
                    state.ToH264Template = state.ToH264Template with { DownscaleAlgo = value.StringValue ?? RequestContracts.H264.DefaultDownscaleAlgorithm };
                },
                Usage: "--downscale-algo <value>"),
            ["--content-profile"] = new CliOptionDefinition(
                Name: "--content-profile",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Content profile.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToMkvGpuScenario },
                ApplyValue: static (state, value) => state.ToMkvTemplate = state.ToMkvTemplate with { ContentProfile = value.StringValue ?? state.ToMkvTemplate.ContentProfile },
                Usage: "--content-profile <value>"),
            ["--quality-profile"] = new CliOptionDefinition(
                Name: "--quality-profile",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Quality profile.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToMkvGpuScenario },
                ApplyValue: static (state, value) => state.ToMkvTemplate = state.ToMkvTemplate with { QualityProfile = value.StringValue ?? state.ToMkvTemplate.QualityProfile },
                Usage: "--quality-profile <value>"),
            ["--no-auto-sample"] = new CliOptionDefinition(
                Name: "--no-auto-sample",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Disable auto sample.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToMkvGpuScenario },
                ApplyValue: static (state, _) => state.ToMkvTemplate = state.ToMkvTemplate with { NoAutoSample = true }),
            ["--auto-sample-mode"] = new CliOptionDefinition(
                Name: "--auto-sample-mode",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Auto sample mode.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToMkvGpuScenario },
                ApplyValue: static (state, value) => state.ToMkvTemplate = state.ToMkvTemplate with { AutoSampleMode = value.StringValue ?? state.ToMkvTemplate.AutoSampleMode },
                Usage: "--auto-sample-mode <value>"),
            ["--sync-audio"] = new CliOptionDefinition(
                Name: "--sync-audio",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Force audio sync path.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToMkvGpuScenario },
                ApplyValue: static (state, _) => state.ToMkvTemplate = state.ToMkvTemplate with { SyncAudio = true }),
            ["--force-video-encode"] = new CliOptionDefinition(
                Name: "--force-video-encode",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Force video encode.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToMkvGpuScenario },
                ApplyValue: static (state, _) => state.ToMkvTemplate = state.ToMkvTemplate with { ForceVideoEncode = true }),
            ["--cq"] = new CliOptionDefinition(
                Name: "--cq",
                ValueKind: CliOptionValueKind.Int,
                IsRepeatable: false,
                HelpText: "CQ override.",
                AppliesToScenarios: CommonScenarios,
                ApplyValue: static (state, value) =>
                {
                    state.ToMkvTemplate = state.ToMkvTemplate with { Cq = value.IntValue };
                    state.ToH264Template = state.ToH264Template with { Cq = value.IntValue };
                },
                InvalidValueError: "--cq must be an integer.",
                Usage: "--cq <int>"),
            ["--maxrate"] = new CliOptionDefinition(
                Name: "--maxrate",
                ValueKind: CliOptionValueKind.Double,
                IsRepeatable: false,
                HelpText: "Maxrate override.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToMkvGpuScenario },
                ApplyValue: static (state, value) => state.ToMkvTemplate = state.ToMkvTemplate with { Maxrate = value.DoubleValue },
                InvalidValueError: "--maxrate must be a number.",
                Usage: "--maxrate <number>"),
            ["--bufsize"] = new CliOptionDefinition(
                Name: "--bufsize",
                ValueKind: CliOptionValueKind.Double,
                IsRepeatable: false,
                HelpText: "Bufsize override.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToMkvGpuScenario },
                ApplyValue: static (state, value) => state.ToMkvTemplate = state.ToMkvTemplate with { Bufsize = value.DoubleValue },
                InvalidValueError: "--bufsize must be a number.",
                Usage: "--bufsize <number>"),
            ["--nvenc-preset"] = new CliOptionDefinition(
                Name: "--nvenc-preset",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "NVENC preset.",
                AppliesToScenarios: CommonScenarios,
                ApplyValue: static (state, value) =>
                {
                    var preset = value.StringValue ?? string.Empty;
                    state.ToMkvTemplate = state.ToMkvTemplate with { NvencPreset = preset };
                    state.ToH264Template = state.ToH264Template with { NvencPreset = preset };
                },
                Usage: "--nvenc-preset <value>"),

            ["--keep-fps"] = new CliOptionDefinition(
                Name: "--keep-fps",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Keep source FPS.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToH264GpuScenario },
                ApplyValue: static (state, _) => state.ToH264Template = state.ToH264Template with { KeepFps = true }),
            ["--use-aq"] = new CliOptionDefinition(
                Name: "--use-aq",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Enable AQ.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToH264GpuScenario },
                ApplyValue: static (state, _) => state.ToH264Template = state.ToH264Template with { UseAq = true }),
            ["--aq-strength"] = new CliOptionDefinition(
                Name: "--aq-strength",
                ValueKind: CliOptionValueKind.Int,
                IsRepeatable: false,
                HelpText: "AQ strength.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToH264GpuScenario },
                ApplyValue: static (state, value) => state.ToH264Template = state.ToH264Template with { AqStrength = value.IntValue ?? RequestContracts.H264.DefaultAqStrength },
                InvalidValueError: "--aq-strength must be an integer.",
                Usage: "--aq-strength <int>"),
            ["--denoise"] = new CliOptionDefinition(
                Name: "--denoise",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Enable denoise.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToH264GpuScenario },
                ApplyValue: static (state, _) => state.ToH264Template = state.ToH264Template with { Denoise = true }),
            ["--fix-timestamps"] = new CliOptionDefinition(
                Name: "--fix-timestamps",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Force timestamp fixes.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToH264GpuScenario },
                ApplyValue: static (state, _) => state.ToH264Template = state.ToH264Template with { FixTimestamps = true }),
            ["--output-mkv"] = new CliOptionDefinition(
                Name: "--output-mkv",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Output mkv container.",
                AppliesToScenarios: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ToH264GpuScenario },
                ApplyValue: static (state, _) => state.ToH264Template = state.ToH264Template with { OutputMkv = true })
        };

        return options;
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
