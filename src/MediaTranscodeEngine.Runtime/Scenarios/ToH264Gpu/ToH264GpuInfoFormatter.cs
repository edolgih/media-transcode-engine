using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;

/*
Это formatter для info-режима сценария toh264gpu.
Он дает короткую сводку по решению сценария и единообразные маркеры ошибок для CLI.
*/
/// <summary>
/// Formats a concise ToH264Gpu decision summary from an inspected source video and the scenario plan.
/// </summary>
public sealed class ToH264GpuInfoFormatter
{
    /// <summary>
    /// Builds a single-line failure summary for known inspection or scenario failures.
    /// </summary>
    public string FormatFailure(string filePath, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(exception);

        var marker = exception.Message.Contains("video stream", StringComparison.OrdinalIgnoreCase)
            ? "no video stream"
            : "ffprobe failed";
        return $"{Path.GetFileName(filePath.Trim())}: [{marker}]";
    }

    /// <summary>
    /// Builds a single-line summary of the actions requested by ToH264Gpu for the supplied video and plan.
    /// </summary>
    public string Format(SourceVideo video, TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        var parts = new List<string>();
        if (plan.CopyVideo)
        {
            parts.Add("remux-only");
        }
        else
        {
            parts.Add("encode h264");
        }

        if (!video.Container.Equals(plan.TargetContainer, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"container .{video.Container}->{plan.TargetContainer}");
        }

        if (plan.TargetHeight.HasValue)
        {
            parts.Add($"downscale {plan.TargetHeight.Value}p");
        }

        if (plan.SynchronizeAudio)
        {
            parts.Add("sync audio");
        }

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        return $"{video.FileName}: [{string.Join("] [", parts)}]";
    }
}
