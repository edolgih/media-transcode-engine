using MediaTranscodeEngine.Runtime.Inspection;

namespace MediaTranscodeEngine.Runtime.Videos;

/// <summary>
/// Loads a source video from a file path and normalizes the metadata required by scenarios.
/// </summary>
public sealed class VideoInspector
{
    private readonly IVideoProbe _videoProbe;

    /// <summary>
    /// Creates an inspector that reads raw metadata from the supplied probe.
    /// </summary>
    /// <param name="videoProbe">Probe used to read raw stream metadata.</param>
    public VideoInspector(IVideoProbe videoProbe)
    {
        _videoProbe = videoProbe ?? throw new ArgumentNullException(nameof(videoProbe));
    }

    /// <summary>
    /// Reads a video file and returns its normalized metadata representation.
    /// </summary>
    /// <param name="filePath">Path to the source video file.</param>
    /// <returns>A normalized source video description.</returns>
    public SourceVideo Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = Path.GetFullPath(filePath.Trim());
        var snapshot = _videoProbe.Probe(normalizedPath);
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

        if (!videoStream.width.HasValue)
        {
            throw new InvalidOperationException("Video probe did not return a valid video width.");
        }

        if (!videoStream.height.HasValue)
        {
            throw new InvalidOperationException("Video probe did not return a valid video height.");
        }

        if (!videoStream.framesPerSecond.HasValue || videoStream.framesPerSecond.Value <= 0)
        {
            throw new InvalidOperationException("Video probe did not return a valid frame rate.");
        }

        var container = string.IsNullOrWhiteSpace(snapshot.container)
            ? Path.GetExtension(normalizedPath).TrimStart('.')
            : snapshot.container;

        var audioCodecs = snapshot.streams
            .Where(stream => stream.streamType.Equals("audio", StringComparison.OrdinalIgnoreCase))
            .Select(stream => stream.codec)
            .ToArray();
        var primaryAudioStream = snapshot.streams
            .FirstOrDefault(stream => stream.streamType.Equals("audio", StringComparison.OrdinalIgnoreCase));
        var bitrate = ResolveBitrate(snapshot);

        return new SourceVideo(
            filePath: normalizedPath,
            container: container,
            videoCodec: videoStream.codec,
            audioCodecs: audioCodecs,
            width: videoStream.width.Value,
            height: videoStream.height.Value,
            framesPerSecond: videoStream.framesPerSecond.Value,
            duration: snapshot.duration ?? TimeSpan.Zero,
            bitrate: bitrate,
            formatName: snapshot.formatName,
            rawFramesPerSecond: videoStream.rawFramesPerSecond,
            averageFramesPerSecond: videoStream.averageFramesPerSecond,
            primaryAudioBitrate: primaryAudioStream?.bitrate,
            primaryAudioSampleRate: primaryAudioStream?.sampleRate,
            primaryAudioChannels: primaryAudioStream?.channels);
    }

    private static long? ResolveBitrate(VideoProbeSnapshot snapshot)
    {
        if (snapshot.formatBitrate.HasValue && snapshot.formatBitrate.Value > 0)
        {
            return snapshot.formatBitrate.Value;
        }

        long sum = 0;
        foreach (var stream in snapshot.streams)
        {
            if (stream.bitrate.HasValue && stream.bitrate.Value > 0)
            {
                sum += stream.bitrate.Value;
            }
        }

        return sum > 0 ? sum : null;
    }
}
