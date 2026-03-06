using MediaTranscodeEngine.Runtime.Inspection;

namespace MediaTranscodeEngine.Runtime.Videos;

/// <summary>
/// Builds a normalized source video from raw probe data returned by an external tool.
/// </summary>
public sealed class ProbedVideoInspector : VideoInspector
{
    private readonly IVideoProbe _videoProbe;

    /// <summary>
    /// Creates an inspector that reads raw metadata from the supplied probe.
    /// </summary>
    /// <param name="videoProbe">Probe used to read raw stream metadata.</param>
    public ProbedVideoInspector(IVideoProbe videoProbe)
    {
        _videoProbe = videoProbe ?? throw new ArgumentNullException(nameof(videoProbe));
    }

    /// <inheritdoc />
    protected override SourceVideo LoadCore(string filePath)
    {
        var snapshot = _videoProbe.Probe(filePath);
        if (snapshot is null)
        {
            throw new InvalidOperationException("Video probe returned no data.");
        }

        if (snapshot.streams.Count == 0)
        {
            throw new InvalidOperationException("Video probe did not return any streams.");
        }

        var videoStream = snapshot.streams.FirstOrDefault(stream =>
            stream.streamType.Equals("video", StringComparison.OrdinalIgnoreCase));

        if (videoStream is null)
        {
            throw new InvalidOperationException("Video probe did not return a video stream.");
        }

        if (!videoStream.width.HasValue || videoStream.width.Value <= 0)
        {
            throw new InvalidOperationException("Video probe did not return a valid video width.");
        }

        if (!videoStream.height.HasValue || videoStream.height.Value <= 0)
        {
            throw new InvalidOperationException("Video probe did not return a valid video height.");
        }

        if (!videoStream.framesPerSecond.HasValue || videoStream.framesPerSecond.Value <= 0)
        {
            throw new InvalidOperationException("Video probe did not return a valid frame rate.");
        }

        var container = string.IsNullOrWhiteSpace(snapshot.container)
            ? Path.GetExtension(filePath).TrimStart('.')
            : snapshot.container;

        var audioCodecs = snapshot.streams
            .Where(stream => stream.streamType.Equals("audio", StringComparison.OrdinalIgnoreCase))
            .Select(stream => stream.codec)
            .ToArray();

        return new SourceVideo(
            filePath: filePath,
            container: container,
            videoCodec: videoStream.codec,
            audioCodecs: audioCodecs,
            width: videoStream.width.Value,
            height: videoStream.height.Value,
            framesPerSecond: videoStream.framesPerSecond.Value,
            duration: snapshot.duration ?? TimeSpan.Zero);
    }
}
