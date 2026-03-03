namespace MediaTranscodeEngine.Core.Engine;

public static class RequestContracts
{
    public static class Transcode
    {
        public const string DefaultContentProfile = "film";
        public const string DefaultQualityProfile = "default";
        public const string DefaultAutoSampleMode = "accurate";
        public const string DefaultNvencPreset = "p6";
        public const string DefaultDownscaleAlgorithm = "bicubic";

        public static readonly IReadOnlyCollection<string> ContentProfiles = new[] { "anime", "mult", "film" };
        public static readonly IReadOnlyCollection<string> QualityProfiles = new[] { "high", "default", "low" };
        public static readonly IReadOnlyCollection<string> AutoSampleModes = new[] { "accurate", "fast", "hybrid" };
        public static readonly IReadOnlyCollection<string> NvencPresets = new[] { "p1", "p2", "p3", "p4", "p5", "p6", "p7" };
        public static readonly IReadOnlyCollection<string> DownscaleAlgorithms = new[] { "bicubic", "lanczos", "bilinear" };
        public static readonly IReadOnlyCollection<int> DownscaleTargets = new[] { 576, 720 };
    }

    public static class H264
    {
        public const string DefaultDownscaleAlgorithm = "bicubic";
        public const string DefaultNvencPreset = "p5";
        public const int DefaultAqStrength = 4;

        public static readonly IReadOnlyCollection<string> DownscaleAlgorithms = new[] { "bicubic", "lanczos", "bilinear" };
        public static readonly IReadOnlyCollection<string> NvencPresets = new[] { "p1", "p2", "p3", "p4", "p5", "p6", "p7" };
        public static readonly IReadOnlyCollection<int> DownscaleTargets = new[] { 576, 720 };
    }
}
