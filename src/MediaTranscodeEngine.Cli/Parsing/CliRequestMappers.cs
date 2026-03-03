using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Parsing;

internal static class CliRequestMappers
{
    public static TranscodeRequest BuildRequest(RawTranscodeRequest template, string inputPath)
    {
        return (template with { InputPath = inputPath }).ToDomain();
    }
}
