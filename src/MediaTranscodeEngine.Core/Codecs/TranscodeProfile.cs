namespace MediaTranscodeEngine.Core.Codecs;

public sealed class TranscodeProfile
{
    private readonly HashSet<string> _supportedContainers;

    public TranscodeProfile(
        string codecId,
        string encoderBackend,
        string strategyKey,
        IEnumerable<string> supportedContainers)
    {
        CodecId = NormalizeToken(codecId, nameof(codecId));
        EncoderBackend = NormalizeToken(encoderBackend, nameof(encoderBackend));
        StrategyKey = NormalizeValue(strategyKey, nameof(strategyKey));
        _supportedContainers = new HashSet<string>(
            supportedContainers
                .Where(static token => !string.IsNullOrWhiteSpace(token))
                .Select(static token => token.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
        if (_supportedContainers.Count == 0)
        {
            throw new ArgumentException("At least one container must be provided.", nameof(supportedContainers));
        }
    }

    public string CodecId { get; }

    public string EncoderBackend { get; }

    public string StrategyKey { get; }

    public IReadOnlyCollection<string> SupportedContainers => _supportedContainers;

    public bool SupportsContainer(string targetContainer)
    {
        if (string.IsNullOrWhiteSpace(targetContainer))
        {
            return false;
        }

        return _supportedContainers.Contains(targetContainer.Trim());
    }

    private static string NormalizeToken(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Token is required.", paramName);
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeValue(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        return value.Trim();
    }
}
