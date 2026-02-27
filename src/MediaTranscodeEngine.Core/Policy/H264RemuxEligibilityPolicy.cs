namespace MediaTranscodeEngine.Core.Policy;

public sealed record H264RemuxEligibilityInput(
    string InputExtension,
    string? FormatName,
    string? VideoCodec,
    string? AudioCodec,
    string? RFrameRate,
    string? AvgFrameRate,
    bool Denoise,
    bool FixTimestamps,
    bool UseDownscale);

public sealed class H264RemuxEligibilityPolicy
{
    private static readonly HashSet<string> Mp4FamilyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".m4v"
    };

    private static readonly HashSet<string> Mp4FamilyFormatTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "mov",
        "mp4",
        "m4a",
        "3gp",
        "3g2",
        "mj2"
    };

    private static readonly HashSet<string> AudioCopyCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "aac",
        "mp3"
    };

    public bool CanRemux(H264RemuxEligibilityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!Mp4FamilyExtensions.Contains(input.InputExtension))
        {
            return false;
        }

        if (!IsMp4FamilyFormat(input.FormatName))
        {
            return false;
        }

        if (!"h264".Equals(input.VideoCodec, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(input.AudioCodec) &&
            !AudioCopyCodecs.Contains(input.AudioCodec))
        {
            return false;
        }

        if (IsVfrSuspected(input.RFrameRate, input.AvgFrameRate))
        {
            return false;
        }

        if (input.Denoise || input.FixTimestamps || input.UseDownscale)
        {
            return false;
        }

        return true;
    }

    private static bool IsMp4FamilyFormat(string? formatName)
    {
        if (string.IsNullOrWhiteSpace(formatName))
        {
            return false;
        }

        var tokens = formatName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (Mp4FamilyFormatTokens.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVfrSuspected(string? rFrameRate, string? avgFrameRate)
    {
        if (string.IsNullOrWhiteSpace(rFrameRate) || string.IsNullOrWhiteSpace(avgFrameRate))
        {
            return false;
        }

        if (rFrameRate == "0/0" || avgFrameRate == "0/0")
        {
            return false;
        }

        return !rFrameRate.Equals(avgFrameRate, StringComparison.OrdinalIgnoreCase);
    }
}
