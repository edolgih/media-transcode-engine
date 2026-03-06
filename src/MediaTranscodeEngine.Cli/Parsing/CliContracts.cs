using System.Globalization;

namespace MediaTranscodeEngine.Cli.Parsing;

internal enum CliOptionValueKind
{
    Flag,
    String,
    Int
}

internal readonly record struct CliParsedValue(
    string? StringValue = null,
    int? IntValue = null);

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
    bool SynchronizeAudio);

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
            SynchronizeAudio: false);
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
            ["--sync-audio"] = new CliOptionDefinition(
                Name: "--sync-audio",
                ValueKind: CliOptionValueKind.Flag,
                IsRepeatable: false,
                HelpText: "Force sync-safe audio path.",
                ApplyValue: static (state, _) => state.RequestTemplate = state.RequestTemplate with { SynchronizeAudio = true })
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
            default:
                errorText = $"Unsupported value kind: {option.ValueKind}";
                return false;
        }
    }
}
