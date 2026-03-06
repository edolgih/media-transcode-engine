namespace MediaTranscodeEngine.Runtime.Inspection;

/// <summary>
/// Reads raw stream metadata for a source video from an external probe tool.
/// </summary>
public interface IVideoProbe
{
    /// <summary>
    /// Probes a video file and returns its raw stream metadata.
    /// </summary>
    /// <param name="filePath">Normalized full path to the source video file.</param>
    /// <returns>Raw probe data for the file.</returns>
    VideoProbeSnapshot Probe(string filePath);
}
