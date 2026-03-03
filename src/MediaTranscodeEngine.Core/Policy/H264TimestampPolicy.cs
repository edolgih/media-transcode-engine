namespace MediaTranscodeEngine.Core.Policy;

public sealed record H264TimestampInput(
    string InputPath,
    string? FormatName,
    bool ForceFixTimestamps);

public sealed class H264TimestampPolicy
{
    public bool ShouldFixTimestamps(H264TimestampInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return input.ForceFixTimestamps ||
               IsWmvOrAsfExtension(input.InputPath) ||
               ContainsFormatToken(input.FormatName, "asf");
    }

    private static bool IsWmvOrAsfExtension(string inputPath)
    {
        var extension = Path.GetExtension(inputPath);
        return extension.Equals(".wmv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".asf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsFormatToken(string? formatName, string expectedToken)
    {
        if (string.IsNullOrWhiteSpace(formatName))
        {
            return false;
        }

        var tokens = formatName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Any(token => token.Equals(expectedToken, StringComparison.OrdinalIgnoreCase));
    }
}
