using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Parsing;

internal sealed record CliParseResult(
    string ScenarioName,
    IReadOnlyList<string> Inputs,
    RawTranscodeRequest ToMkvRequestTemplate,
    RawH264TranscodeRequest ToH264RequestTemplate);

internal static class CliArgumentParser
{
    private const string LegacyCommandToken = "tomkvgpu";

    public static bool TryParse(
        string[] args,
        out CliParseResult parsed,
        out string? errorText)
    {
        parsed = default!;
        errorText = null;

        if (!TryResolveScenario(args, out var scenarioName, out errorText))
        {
            return false;
        }

        var state = new CliMutableParseState();
        state.ScenarioName = scenarioName;
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (string.Equals(token, LegacyCommandToken, StringComparison.OrdinalIgnoreCase))
            {
                errorText = "Do not use 'tomkvgpu' command token. Use CLI switches directly.";
                return false;
            }

            if (!CliContracts.TryGetOption(token, out var option))
            {
                errorText = token.StartsWith("-", StringComparison.Ordinal)
                    ? $"Unknown option: {token}"
                    : $"Unexpected argument: {token}";
                return false;
            }

            if (token.Equals("--scenario", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref i, token, out _, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (!option.AppliesToScenarios.Contains(scenarioName))
            {
                errorText = token.StartsWith("-", StringComparison.Ordinal)
                    ? $"Unknown option: {token}"
                    : $"Unexpected argument: {token}";
                return false;
            }

            CliParsedValue value = default;
            if (option.ValueKind is not CliOptionValueKind.Flag)
            {
                if (!TryReadValue(args, ref i, token, out var valueToken, out errorText))
                {
                    return false;
                }

                if (!CliContracts.TryParseValue(option, valueToken, out value, out errorText))
                {
                    return false;
                }
            }

            option.ApplyValue(state, value);
        }

        parsed = new CliParseResult(
            ScenarioName: scenarioName,
            Inputs: state.Inputs,
            ToMkvRequestTemplate: state.ToMkvTemplate,
            ToH264RequestTemplate: state.ToH264Template);
        return true;
    }

    private static bool TryResolveScenario(
        string[] args,
        out string scenarioName,
        out string? errorText)
    {
        scenarioName = CliContracts.ToMkvGpuScenario;
        errorText = null;

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.Equals("--scenario", StringComparison.OrdinalIgnoreCase))
            {
                if (CliContracts.TryGetOption(token, out var option) &&
                    option.ValueKind is not CliOptionValueKind.Flag)
                {
                    i++;
                }

                continue;
            }

            if (!TryReadValue(args, ref i, token, out var value, out errorText))
            {
                return false;
            }

            if (!CliContracts.ScenariosByName.ContainsKey(value))
            {
                errorText = $"Unknown scenario: {value}";
                return false;
            }

            scenarioName = value;
        }

        return true;
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string optionName,
        out string value,
        out string? errorText)
    {
        value = string.Empty;
        errorText = null;

        var valueIndex = index + 1;
        if (valueIndex >= args.Length)
        {
            errorText = $"{optionName} requires a value.";
            return false;
        }

        var token = args[valueIndex];
        if (token.StartsWith("-", StringComparison.Ordinal))
        {
            errorText = $"{optionName} requires a value.";
            return false;
        }

        value = token;
        index = valueIndex;
        return true;
    }
}
