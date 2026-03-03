namespace MediaTranscodeEngine.Core.Policy;

public sealed record H264AudioInput(
    string? AudioCodec,
    bool FixTimestamps);

public sealed class H264AudioPolicy
{
    private static readonly HashSet<string> CopyCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "aac",
        "mp3"
    };

    public bool CanCopyAudio(H264AudioInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.FixTimestamps)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(input.AudioCodec))
        {
            return false;
        }

        return CopyCodecs.Contains(input.AudioCodec);
    }
}
