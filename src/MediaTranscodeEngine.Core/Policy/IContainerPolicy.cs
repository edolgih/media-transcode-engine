using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Policy;

public sealed record ContainerOutputPaths(
    string OutputPath,
    string TempOutputPath);

public interface IContainerPolicy
{
    string Container { get; }
    string OutputExtension { get; }
    string MuxArguments { get; }

    ContainerOutputPaths ResolveOutputPaths(
        string inputPath,
        bool keepSource,
        bool useDownscale,
        int? downscaleTarget,
        bool willEncode);

    string BuildPostOperation(
        string inputPath,
        string tempOutputPath,
        string outputPath,
        bool replaceInput);
}
