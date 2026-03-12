using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/*
Этот форматтер собирает короткую сводку по решению tomkvgpu.
Он нужен для info-режима и failure-маркеров, близких к старому поведению.
*/
/// <summary>
/// Formats a concise ToMkvGpu decision summary from an inspected source video and the scenario plan.
/// </summary>
public sealed class ToMkvGpuInfoFormatter
{
    /// <summary>
    /// Builds the standard info marker used when probe data could not be loaded.
    /// </summary>
    /// <param name="filePath">Path to the source file that could not be probed.</param>
    /// <returns>A single-line ffprobe failure marker.</returns>
    public string FormatProbeFailure(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return $"{Path.GetFileName(filePath.Trim())}: [ffprobe failed]";
    }

    /// <summary>
    /// Builds a single-line failure summary for known inspection or scenario failures.
    /// </summary>
    /// <param name="filePath">Path to the source file that failed.</param>
    /// <param name="exception">Failure raised while inspecting or planning the file.</param>
    /// <returns>A single-line info marker for the failure.</returns>
    public string FormatFailure(string filePath, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(exception);

        var marker = ResolveFailureMarker(exception);
        return $"{Path.GetFileName(filePath.Trim())}: [{marker}]";
    }

    /// <summary>
    /// Builds a single-line summary of the actions requested by ToMkvGpu for the supplied video and plan.
    /// </summary>
    /// <param name="video">Inspected source video facts.</param>
    /// <param name="plan">Resolved ToMkvGpu plan.</param>
    /// <returns>A summary line or an empty string when no action is required.</returns>
    public string Format(SourceVideo video, TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        var parts = new List<string>();

        if (!video.Container.Equals(plan.TargetContainer, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"container .{video.Container}→{plan.TargetContainer}");
        }

        if (!plan.CopyVideo)
        {
            parts.Add($"vcodec {video.VideoCodec}");
        }

        if (plan.TargetFramesPerSecond.HasValue)
        {
            parts.Add($"fps {plan.TargetFramesPerSecond.Value:0.###}");
        }

        if (HasNonAacAudio(video))
        {
            parts.Add("audio non-AAC");
        }

        if (plan.SynchronizeAudio && video.HasAudio)
        {
            parts.Add("sync audio");
        }

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        return $"{video.FileName}: [{string.Join("] [", parts)}]";
    }

    private static bool HasNonAacAudio(SourceVideo video)
    {
        return video.AudioCodecs.Any(codec => !codec.Equals("aac", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveFailureMarker(Exception exception)
    {
        var message = exception.Message;
        if (message.Contains("valid video width", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("valid video height", StringComparison.OrdinalIgnoreCase))
        {
            return "unknown dimensions";
        }

        if (message.Contains("video stream", StringComparison.OrdinalIgnoreCase))
        {
            return "no video stream";
        }

        if (message.Contains("source bucket missing", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("source bucket invalid", StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        return "ffprobe failed";
    }
}
