namespace Transcode.Core.Tools.Ffmpeg;

/*
Этот helper отвечает за файловую раскладку ffmpeg-выполнения.
Он вычисляет финальный и временный output path и добавляет post-steps для delete/rename.
*/
/// <summary>
/// Provides shared path and post-operation helpers for ffmpeg-based scenario renderers.
/// </summary>
internal static class FfmpegExecutionLayout
{
    public static string ResolveFinalOutputPath(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return Path.GetFullPath(outputPath.Trim());
    }

    public static string ResolveWorkingOutputPath(string sourceFilePath, string sourceFileNameWithoutExtension, bool keepSource, string finalOutputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileNameWithoutExtension);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalOutputPath);

        if (keepSource)
        {
            return finalOutputPath;
        }

        if (finalOutputPath.Equals(sourceFilePath, StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(finalOutputPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = ".";
            }

            return Path.Combine(directory, $"{sourceFileNameWithoutExtension}_temp{Path.GetExtension(finalOutputPath)}");
        }

        return finalOutputPath;
    }

    public static void AppendPostOperations(
        List<string> commands,
        string sourceFilePath,
        bool keepSource,
        string workingOutputPath,
        string finalOutputPath)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingOutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalOutputPath);

        if (keepSource)
        {
            return;
        }

        if (finalOutputPath.Equals(sourceFilePath, StringComparison.OrdinalIgnoreCase))
        {
            commands.Add($"del {Quote(sourceFilePath)}");
            commands.Add($"ren {Quote(workingOutputPath)} {Quote(Path.GetFileName(finalOutputPath))}");
            return;
        }

        commands.Add($"del {Quote(sourceFilePath)}");
    }

    public static string Quote(string value)
    {
        return $"\"{value}\"";
    }
}
