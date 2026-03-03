using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Policy;

public sealed class Mp4ContainerPolicy : IContainerPolicy
{
    public string Container => RequestContracts.General.Mp4Container;
    public string OutputExtension => ".mp4";
    public string MuxArguments => "-movflags +faststart";

    public ContainerOutputPaths ResolveOutputPaths(
        string inputPath,
        bool keepSource,
        bool useDownscale,
        int? downscaleTarget,
        bool willEncode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        if (keepSource)
        {
            var downscaleSuffix = useDownscale && downscaleTarget.HasValue
                ? $"{downscaleTarget.Value}p"
                : null;
            var codecSuffix = willEncode ? "h264" : null;
            var outputPath = OutputPathBuilder.BuildKeepSourceOutputPath(
                inputPath,
                outputExtension: OutputExtension,
                downscaleSuffix,
                codecSuffix);
            return new ContainerOutputPaths(outputPath, outputPath);
        }

        var directory = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        return new ContainerOutputPaths(
            OutputPath: Path.Combine(directory, $"{baseName}{OutputExtension}"),
            TempOutputPath: Path.Combine(directory, $"{baseName} (h264){OutputExtension}"));
    }

    public string BuildPostOperation(
        string inputPath,
        string tempOutputPath,
        string outputPath,
        bool replaceInput)
    {
        if (!replaceInput)
        {
            return string.Empty;
        }

        return $"&& del {Quote(inputPath)} && move /Y {Quote(tempOutputPath)} {Quote(outputPath)}";
    }

    private static string Quote(string value) => $"\"{value}\"";
}
