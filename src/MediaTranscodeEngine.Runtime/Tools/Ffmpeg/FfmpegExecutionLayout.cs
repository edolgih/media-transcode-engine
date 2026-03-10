using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tools.Ffmpeg;

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
