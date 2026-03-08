using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

namespace MediaTranscodeEngine.Cli.Parsing;

internal sealed record CliParseResult(
    IReadOnlyList<string> Inputs,
    CliRequestTemplate RequestTemplate);

internal static class CliArgumentParser
{
    private const string LegacyToMkvCommandToken = "tomkvgpu";
    private const string LegacyToH264CommandToken = "toh264gpu";

    public static bool TryParse(
        string[] args,
        out CliParseResult parsed,
        out string? errorText)
    {
        parsed = default!;
        errorText = null;

        var state = new CliMutableParseState();
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (IsLegacyCommandToken(token))
            {
                errorText = "Do not use legacy scenario command tokens. Use --scenario tomkvgpu.";
                return false;
            }

            if (!CliContracts.TryGetOption(token, out var option))
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

        if (!CliContracts.IsSupportedScenario(state.RequestTemplate.Scenario))
        {
            errorText = $"Unsupported scenario: {state.RequestTemplate.Scenario}. Only '{CliContracts.SupportedScenario}' is available.";
            return false;
        }

        if (state.RequestTemplate.MaxFramesPerSecond.HasValue &&
            !ToMkvGpuRequest.IsSupportedMaxFramesPerSecond(state.RequestTemplate.MaxFramesPerSecond.Value))
        {
            errorText = $"--max-fps must be one of: {ToMkvGpuRequest.SupportedMaxFramesPerSecondDisplay}.";
            return false;
        }

        parsed = new CliParseResult(
            Inputs: state.Inputs,
            RequestTemplate: state.RequestTemplate);
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

    private static bool IsLegacyCommandToken(string token)
    {
        return token.Equals(LegacyToMkvCommandToken, StringComparison.OrdinalIgnoreCase) ||
               token.Equals(LegacyToH264CommandToken, StringComparison.OrdinalIgnoreCase);
    }
}
