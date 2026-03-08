using System.Globalization;

namespace MediaTranscodeEngine.Cli.Parsing;

internal enum CliOptionValueKind
{
    Flag,
    String,
    Int,
    Decimal
}

internal readonly record struct CliParsedValue(
    string? StringValue = null,
    int? IntValue = null,
    decimal? DecimalValue = null);

internal sealed record CliOptionDefinition(
    string Name,
    CliOptionValueKind ValueKind,
    bool IsRepeatable,
    string HelpText,
    Action<CliMutableParseState, CliParsedValue> ApplyValue,
    string? InvalidValueError = null,
    string? Usage = null);

internal sealed record CliRequestTemplate(
    string Scenario,
    bool Info,
    bool KeepSource,
    bool OverlayBackground,
    int? DownscaleTarget,
    int? MaxFramesPerSecond,
    bool SynchronizeAudio,
    string? ContentProfile,
    string? QualityProfile,
    bool NoAutoSample,
    string? AutoSampleMode,
    string? DownscaleAlgorithm,
    int? Cq,
    decimal? Maxrate,
    decimal? Bufsize,
    string? NvencPreset);

internal sealed class CliMutableParseState
{
    public List<string> Inputs { get; } = [];

    public CliRequestTemplate RequestTemplate { get; set; } =
        new(
            Scenario: CliContracts.SupportedScenario,
            Info: false,
            KeepSource: false,
            OverlayBackground: false,
            DownscaleTarget: null,
            MaxFramesPerSecond: null,
            SynchronizeAudio: false,
            ContentProfile: null,
            QualityProfile: null,
            NoAutoSample: false,
            AutoSampleMode: null,
            DownscaleAlgorithm: null,
            Cq: null,
            Maxrate: null,
            Bufsize: null,
            NvencPreset: null);
}

internal static class CliContracts
{
    public const string SupportedScenario = "tomkvgpu";

    public static readonly IReadOnlyDictionary<string, CliOptionDefinition> OptionsByName = CreateOptions();

    public static bool TryGetOption(string token, out CliOptionDefinition option)
    {
        return OptionsByName.TryGetValue(token, out option!);
    }

    public static bool IsSupportedScenario(string? scenarioName)
    {
        return string.Equals(scenarioName, SupportedScenario, StringComparison.OrdinalIgnoreCase);
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
            ["--scenario"] = new CliOptionDefinition(
                Name: "--scenario",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Scenario name. Only tomkvgpu is supported.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with
                {
                    Scenario = value.StringValue?.Trim() ?? string.Empty
                },
                Usage: "--scenario tomkvgpu"),
            ["--info"] = new CliOptionDefinition(
                Name: "--info",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Show per-file runtime decision markers.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { Info = true }),
            ["--keep-source"] = new CliOptionDefinition(
                Name: "--keep-source",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Keep source file and write output to a new path.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { KeepSource = true }),
            ["--overlay-bg"] = new CliOptionDefinition(
                Name: "--overlay-bg",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Apply overlay background path during encode.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { OverlayBackground = true }),
            ["--downscale"] = new CliOptionDefinition(
                Name: "--downscale",
                ValueKind: CliOptionValueKind.Int,
                IsRepeatable: false,
                HelpText: "Downscale target height.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { DownscaleTarget = value.IntValue },
                InvalidValueError: "--downscale must be an integer.",
                Usage: "--downscale <int>"),
            ["--max-fps"] = new CliOptionDefinition(
                Name: "--max-fps",
                ValueKind: CliOptionValueKind.Int,
                IsRepeatable: false,
                HelpText: "Optional frame-rate cap. Supported values: 50, 40, 30, 24.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { MaxFramesPerSecond = value.IntValue },
                InvalidValueError: "--max-fps must be an integer.",
                Usage: "--max-fps <50|40|30|24>"),
            ["--sync-audio"] = new CliOptionDefinition(
                Name: "--sync-audio",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Force sync-safe audio path.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { SynchronizeAudio = true }),
            ["--content-profile"] = new CliOptionDefinition(
                Name: "--content-profile",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "576 profile content kind.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { ContentProfile = value.StringValue },
                Usage: "--content-profile <anime|mult|film>"),
            ["--quality-profile"] = new CliOptionDefinition(
                Name: "--quality-profile",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "576 profile quality kind.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { QualityProfile = value.StringValue },
                Usage: "--quality-profile <high|default|low>"),
            ["--no-autosample"] = new CliOptionDefinition(
                Name: "--no-autosample",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Disable 576 autosample adjustments.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { NoAutoSample = true }),
            ["--autosample-mode"] = new CliOptionDefinition(
                Name: "--autosample-mode",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "576 autosample mode.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { AutoSampleMode = value.StringValue },
                Usage: "--autosample-mode <accurate|fast|hybrid>"),
            ["--downscale-algo"] = new CliOptionDefinition(
                Name: "--downscale-algo",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Explicit downscale algorithm override.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { DownscaleAlgorithm = value.StringValue },
                Usage: "--downscale-algo <bilinear|bicubic|lanczos>"),
            ["--cq"] = new CliOptionDefinition(
                Name: "--cq",
                ValueKind: CliOptionValueKind.Int,
                IsRepeatable: false,
                HelpText: "Explicit NVENC CQ override.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { Cq = value.IntValue },
                InvalidValueError: "--cq must be an integer.",
                Usage: "--cq <int>"),
            ["--maxrate"] = new CliOptionDefinition(
                Name: "--maxrate",
                ValueKind: CliOptionValueKind.Decimal,
                IsRepeatable: false,
                HelpText: "Explicit VBV maxrate in Mbit/s.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { Maxrate = value.DecimalValue },
                InvalidValueError: "--maxrate must be a number.",
                Usage: "--maxrate <number>"),
            ["--bufsize"] = new CliOptionDefinition(
                Name: "--bufsize",
                ValueKind: CliOptionValueKind.Decimal,
                IsRepeatable: false,
                HelpText: "Explicit VBV bufsize in Mbit/s.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { Bufsize = value.DecimalValue },
                InvalidValueError: "--bufsize must be a number.",
                Usage: "--bufsize <number>"),
            ["--nvenc-preset"] = new CliOptionDefinition(
                Name: "--nvenc-preset",
                ValueKind: CliOptionValueKind.String,
                IsRepeatable: false,
                HelpText: "Explicit NVENC preset override.",
                ApplyValue: static (state, value) => state.RequestTemplate = state.RequestTemplate with { NvencPreset = value.StringValue },
                Usage: "--nvenc-preset <preset>")
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
            case CliOptionValueKind.Decimal:
                if (!decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    errorText = option.InvalidValueError ?? $"{option.Name} must be a number.";
                    return false;
                }

                parsedValue = new CliParsedValue(DecimalValue: decimalValue);
                return true;
            default:
                errorText = $"Unsupported value kind: {option.ValueKind}";
                return false;
        }
    }
}
