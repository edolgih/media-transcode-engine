namespace MediaTranscodeEngine.Cli.Processing;

internal interface ITranscodeProcessor
{
    string Process(CliTranscodeRequest request);
}
