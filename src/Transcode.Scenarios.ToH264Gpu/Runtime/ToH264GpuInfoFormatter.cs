using Transcode.Runtime.Failures;
using Transcode.Runtime.MediaIntent;
using Transcode.Runtime.Videos;

namespace Transcode.Scenarios.ToH264Gpu.Runtime;

/*
Это formatter для info-режима сценария toh264gpu.
Он дает короткую сводку по решению сценария и единообразные маркеры ошибок для CLI.
*/
/// <summary>
/// Formats a concise ToH264Gpu decision summary from an inspected source video and the resolved scenario decision.
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

        var marker = exception is RuntimeFailureException runtimeFailure && runtimeFailure.Code == RuntimeFailureCode.NoVideoStream
            ? "no video stream"
            : "ffprobe failed";
        return $"{Path.GetFileName(filePath.Trim())}: [{marker}]";
    }

    /// <summary>
    /// Builds a single-line summary of the actions requested by ToH264Gpu for the supplied video and decision.
    /// </summary>
    internal string Format(SourceVideo video, ToH264GpuDecision decision)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(decision);

        var parts = new List<string>();
        if (decision.CopyVideo)
        {
            parts.Add("remux-only");
        }
        else
        {
            parts.Add("encode h264");
        }

        if (!video.Container.Equals(decision.TargetContainer, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"container .{video.Container}->{decision.TargetContainer}");
        }

        if (decision.Video is EncodeVideoIntent { Downscale: { } downscale })
        {
            parts.Add($"downscale {downscale.TargetHeight}p");
        }

        if (decision.SynchronizeAudio)
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
