using System.Globalization;

namespace MediaTranscodeEngine.Cli.Parsing;

/*
Это общий helper для scenario-specific CLI parsing.
Он решает только transport-задачу чтения next token как string/int/decimal.
*/
/// <summary>
/// Reads typed option values from scenario-local CLI tokens without introducing domain validation.
/// </summary>
internal static class CliOptionReader
{
    public static bool TryReadRequiredValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        out string value,
        out string? errorText)
    {
        value = string.Empty;
        errorText = null;

        var valueIndex = index + 1;
        if (valueIndex >= args.Count)
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

    public static bool TryReadInt(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        string invalidValueError,
        out int? value,
        out string? errorText)
    {
        value = null;
        errorText = null;

        if (!TryReadRequiredValue(args, ref index, optionName, out var token, out errorText))
        {
            return false;
        }

        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            errorText = invalidValueError;
            return false;
        }

        value = parsedValue;
        return true;
    }

    public static bool TryReadDecimal(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        string invalidValueError,
        out decimal? value,
        out string? errorText)
    {
        value = null;
        errorText = null;

        if (!TryReadRequiredValue(args, ref index, optionName, out var token, out errorText))
        {
            return false;
        }

        if (!decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
        {
            errorText = invalidValueError;
            return false;
        }

        value = parsedValue;
        return true;
    }
}
