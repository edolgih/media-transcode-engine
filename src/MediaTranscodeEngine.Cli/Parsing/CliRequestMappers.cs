using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Parsing;

internal static class CliRequestMappers
{
    public static UnifiedTranscodeRequest BuildUnifiedRequest(RawUnifiedTranscodeRequest template, string inputPath)
    {
        return (template with { InputPath = inputPath }).ToDomain();
    }
}
