using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tools.Ffmpeg;

/*
Этот helper отвечает за файловую раскладку ffmpeg-выполнения.
Он вычисляет финальный и временный output path и добавляет post-steps для delete/rename.
*/
/// <summary>
/// Provides shared path and post-operation helpers for ffmpeg-based tool adapters.
/// </summary>
internal static class FfmpegExecutionLayout
{
    public static string ResolveFinalOutputPath(SourceVideo video, TranscodePlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.OutputPath))
        {
            return plan.OutputPath;
        }

        var directory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}.{plan.TargetContainer}");
    }

    public static string ResolveWorkingOutputPath(SourceVideo video, TranscodePlan plan, string finalOutputPath)
    {
        if (plan.KeepSource)
        {
            return finalOutputPath;
        }

        if (finalOutputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(finalOutputPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = ".";
            }

            return Path.Combine(directory, $"{video.FileNameWithoutExtension}_temp{Path.GetExtension(finalOutputPath)}");
        }

        return finalOutputPath;
    }

    public static void AppendPostOperations(
        List<string> commands,
        SourceVideo video,
        TranscodePlan plan,
        string workingOutputPath,
        string finalOutputPath)
    {
        if (plan.KeepSource)
        {
            return;
        }

        if (finalOutputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            commands.Add($"del {Quote(video.FilePath)}");
            commands.Add($"ren {Quote(workingOutputPath)} {Quote(Path.GetFileName(finalOutputPath))}");
            return;
        }

        commands.Add($"del {Quote(video.FilePath)}");
    }

    public static string Quote(string value)
    {
        return $"\"{value}\"";
    }
}
