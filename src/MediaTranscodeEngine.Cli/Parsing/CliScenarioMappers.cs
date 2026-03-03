using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Parsing;

internal static class CliScenarioMappers
{
    public static TranscodeRequest BuildToMkvRequest(RawTranscodeRequest template, string inputPath)
    {
        return (template with { InputPath = inputPath }).ToDomain();
    }

    public static H264TranscodeRequest BuildToH264Request(RawH264TranscodeRequest template, string inputPath)
    {
        return (template with { InputPath = inputPath }).ToDomain();
    }
}
